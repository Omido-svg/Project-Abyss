using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 결과는 이미 최종 상태까지 계산되어 있지만 Timeline은 과거 시점부터 재생된다.
/// 이 read model은 재생 중 화면이 읽을 HP/부위 상태/사망/흐트러짐을 별도로 진행한다.
/// 게임 규칙과 Character/BodyPart 실제 상태는 절대 변경하지 않는다.
/// </summary>
internal sealed class BattlePresentationState
{
    private readonly Dictionary<Character, CharacterDisplayState>
        characters = new();

    private readonly Dictionary<BodyPart, BodyPartDisplayState>
        parts = new();

    private readonly Dictionary<Character, StaggerDisplayState>
        stagger = new();

    public IEnumerable<CharacterDisplayState> Characters =>
        characters.Values;

    public IEnumerable<BodyPartDisplayState> Parts =>
        parts.Values;

    public IEnumerable<StaggerDisplayState> StaggerStates =>
        stagger.Values;

    public static BattlePresentationState Create(
        BattleVisualRequest root)
    {
        BattlePresentationState state =
            new BattlePresentationState();

        state.CaptureRoot(root);
        return state;
    }

    public bool TryGetCharacter(
        Character character,
        out CharacterDisplayState state)
    {
        if (character == null)
        {
            state = null;
            return false;
        }

        return characters.TryGetValue(
            character,
            out state);
    }

    public bool TryGetPart(
        BodyPart part,
        out BodyPartDisplayState state)
    {
        if (part == null)
        {
            state = null;
            return false;
        }

        return parts.TryGetValue(
            part,
            out state);
    }

    public bool TryGetStagger(
        Character character,
        out StaggerDisplayState state)
    {
        if (character == null)
        {
            state = null;
            return false;
        }

        return stagger.TryGetValue(
            character,
            out state);
    }

    public void ApplyHit(
        BattleVisualRequest request,
        int hitIndex)
    {
        if (request == null)
            return;

        if (request.TargetImpacts != null)
        {
            foreach (TargetImpactPresentation impact
                     in request.TargetImpacts)
            {
                ApplyImpactHit(
                    impact,
                    hitIndex);
            }
        }

        if (hitIndex == 0 &&
            request.Target != null &&
            request.StaggerDamage > 0 &&
            stagger.TryGetValue(
                request.Target,
                out StaggerDisplayState staggerState))
        {
            staggerState.CurrentGauge =
                Mathf.Max(
                    0,
                    request.StaggerGaugeAfter);

            staggerState.IsVulnerable =
                staggerState.CurrentGauge <= 0;
        }
    }

    private void CaptureRoot(
        BattleVisualRequest root)
    {
        if (root == null)
            return;

        if (root.HasClashSequence)
        {
            foreach (BattleClashVisualExchange exchange
                     in root.ClashExchanges)
            {
                CaptureRequest(
                    exchange?.AttackRequest);
            }

            return;
        }

        CaptureRequest(root);
    }

    private void CaptureRequest(
        BattleVisualRequest request)
    {
        if (request == null)
            return;

        if (request.TargetImpacts != null)
        {
            foreach (TargetImpactPresentation impact
                     in request.TargetImpacts)
            {
                CaptureImpact(impact);
            }
        }

        if (request.Target == null ||
            request.StaggerDamage <= 0)
        {
            return;
        }

        if (!stagger.TryGetValue(
                request.Target,
                out StaggerDisplayState existing))
        {
            stagger.Add(
                request.Target,
                new StaggerDisplayState(
                    request.Target,
                    request.StaggerGaugeBefore,
                    request.StaggerGaugeAfter));
            return;
        }

        existing.FinalGauge =
            Mathf.Max(
                0,
                request.StaggerGaugeAfter);
    }

    private void CaptureImpact(
        TargetImpactPresentation impact)
    {
        if (impact?.Target == null)
            return;

        if (!characters.TryGetValue(
                impact.Target,
                out CharacterDisplayState characterState))
        {
            int currentHp =
                impact.HasCharacterHpSnapshot
                    ? impact.CharacterHpBefore
                    : Mathf.Max(
                        0,
                        impact.Target.CurrentHP);

            int finalHp =
                impact.HasCharacterHpSnapshot
                    ? impact.CharacterHpAfter
                    : Mathf.Max(
                        0,
                        impact.Target.CurrentHP);

            characterState =
                new CharacterDisplayState(
                    impact.Target,
                    currentHp,
                    finalHp,
                    impact.CharacterMaxHp,
                    impact.CharacterWasDeadBefore,
                    impact.CharacterWasDeadAfter,
                    impact.TargetPart == null);

            characters.Add(
                impact.Target,
                characterState);
        }
        else
        {
            if (impact.HasCharacterHpSnapshot)
            {
                characterState.FinalHp =
                    Mathf.Max(
                        0,
                        impact.CharacterHpAfter);
            }

            characterState.FinalDead =
                impact.CharacterWasDeadAfter;

            characterState.HasCharacterLevelImpact |=
                impact.TargetPart == null;
        }

        if (impact.TargetPart == null ||
            !impact.HasPartSnapshot)
        {
            return;
        }

        if (!parts.TryGetValue(
                impact.TargetPart,
                out BodyPartDisplayState partState))
        {
            partState =
                new BodyPartDisplayState(
                    impact.Target,
                    impact.TargetPart,
                    impact.PartHpBefore,
                    impact.PartHpAfter,
                    impact.PartStateBefore,
                    impact.PartStateAfter);

            parts.Add(
                impact.TargetPart,
                partState);
        }
        else
        {
            partState.FinalHp =
                Mathf.Max(
                    0,
                    impact.PartHpAfter);

            partState.FinalState =
                impact.PartStateAfter;
        }
    }

    private void ApplyImpactHit(
        TargetImpactPresentation impact,
        int hitIndex)
    {
        if (impact?.Target == null)
            return;

        if (characters.TryGetValue(
                impact.Target,
                out CharacterDisplayState characterState))
        {
            int hpDamage =
                impact.GetCharacterHpDamageForHitIndex(
                    hitIndex);

            if (hpDamage > 0)
            {
                characterState.CurrentHp =
                    Mathf.Max(
                        impact.CharacterHpAfter,
                        characterState.CurrentHp - hpDamage);
            }

            if (impact.IsFinalHit(hitIndex))
            {
                if (impact.HasCharacterHpSnapshot)
                {
                    characterState.CurrentHp =
                        Mathf.Max(
                            0,
                            impact.CharacterHpAfter);
                }

                characterState.IsDead =
                    impact.CharacterWasDeadAfter;
            }
        }

        if (impact.TargetPart == null ||
            !parts.TryGetValue(
                impact.TargetPart,
                out BodyPartDisplayState partState))
        {
            return;
        }

        int partDamage =
            impact.GetPartHpDamageForHitIndex(
                hitIndex);

        if (partDamage > 0)
        {
            partState.CurrentHp =
                Mathf.Max(
                    impact.PartHpAfter,
                    partState.CurrentHp - partDamage);
        }

        if (!impact.IsFinalHit(hitIndex))
            return;

        if (impact.HasPartSnapshot)
        {
            partState.CurrentHp =
                Mathf.Max(
                    0,
                    impact.PartHpAfter);
        }

        partState.State =
            impact.PartStateAfter;
    }
}

internal sealed class CharacterDisplayState
{
    public CharacterDisplayState(
        Character character,
        int currentHp,
        int finalHp,
        int maxHp,
        bool isDead,
        bool finalDead,
        bool hasCharacterLevelImpact)
    {
        Character = character;
        CurrentHp = Mathf.Max(0, currentHp);
        FinalHp = Mathf.Max(0, finalHp);
        MaxHp = Mathf.Max(1, maxHp);
        IsDead = isDead;
        FinalDead = finalDead;
        HasCharacterLevelImpact = hasCharacterLevelImpact;
    }

    public Character Character { get; }
    public int CurrentHp { get; set; }
    public int FinalHp { get; set; }
    public int MaxHp { get; }
    public bool IsDead { get; set; }
    public bool FinalDead { get; set; }
    public bool HasCharacterLevelImpact { get; set; }
}

internal sealed class BodyPartDisplayState
{
    public BodyPartDisplayState(
        Character character,
        BodyPart part,
        int currentHp,
        int finalHp,
        BodyPartState state,
        BodyPartState finalState)
    {
        Character = character;
        Part = part;
        CurrentHp = Mathf.Max(0, currentHp);
        FinalHp = Mathf.Max(0, finalHp);
        State = state;
        FinalState = finalState;
    }

    public Character Character { get; }
    public BodyPart Part { get; }
    public int CurrentHp { get; set; }
    public int FinalHp { get; set; }
    public BodyPartState State { get; set; }
    public BodyPartState FinalState { get; set; }
}

internal sealed class StaggerDisplayState
{
    public StaggerDisplayState(
        Character character,
        int currentGauge,
        int finalGauge)
    {
        Character = character;
        CurrentGauge = Mathf.Max(0, currentGauge);
        FinalGauge = Mathf.Max(0, finalGauge);
        IsVulnerable = CurrentGauge <= 0;
    }

    public Character Character { get; }
    public int CurrentGauge { get; set; }
    public int FinalGauge { get; set; }
    public bool IsVulnerable { get; set; }
}

/// <summary>
/// UI/View가 현재 playback의 read model을 찾는 얇은 presentation-only registry.
/// 전투 규칙에서 사용하지 않는다.
/// </summary>
internal static class BattlePresentationStateRegistry
{
    private static BattlePresentationState active;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        active = null;
    }

    public static void Bind(
        BattlePresentationState state)
    {
        active = state;
    }

    public static void Clear(
        BattlePresentationState state)
    {
        if (ReferenceEquals(active, state))
            active = null;
    }

    public static bool TryGetPartState(
        BodyPart part,
        out BodyPartState state)
    {
        if (active != null &&
            active.TryGetPart(
                part,
                out BodyPartDisplayState display))
        {
            state = display.State;
            return true;
        }

        state = default;
        return false;
    }

    public static bool TryGetHp(
        Character character,
        BodyPart part,
        out int hp)
    {
        if (active == null)
        {
            hp = 0;
            return false;
        }

        if (part != null &&
            active.TryGetPart(
                part,
                out BodyPartDisplayState partState))
        {
            hp = partState.CurrentHp;
            return true;
        }

        if (part == null &&
            active.TryGetCharacter(
                character,
                out CharacterDisplayState characterState) &&
            characterState.HasCharacterLevelImpact)
        {
            hp = characterState.CurrentHp;
            return true;
        }

        hp = 0;
        return false;
    }

    public static bool TryGetDeathState(
        Character character,
        out bool isDead)
    {
        if (active != null &&
            active.TryGetCharacter(
                character,
                out CharacterDisplayState state))
        {
            isDead = state.IsDead;
            return true;
        }

        isDead = false;
        return false;
    }

    public static bool TryGetStagger(
        Character character,
        out int gauge,
        out bool vulnerable)
    {
        if (active != null &&
            active.TryGetStagger(
                character,
                out StaggerDisplayState state))
        {
            gauge = state.CurrentGauge;
            vulnerable = state.IsVulnerable;
            return true;
        }

        gauge = 0;
        vulnerable = false;
        return false;
    }
}
