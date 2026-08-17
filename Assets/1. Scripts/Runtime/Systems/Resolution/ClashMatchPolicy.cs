using System.Collections.Generic;

/// <summary>
/// COMBAT 행동끼리 합이 가능한지와,
/// 특정 공격이 어느 상대 행동과 합할지를 결정한다.
///
/// 2026-08-17 Focused Encounter / 가로채기 규칙:
/// - 정확한 적 ActionSlot을 지정할 수 있다.
/// - 적 행동이 노린 정확한 부위의 행동 슬롯은 속도와 무관하게 그 행동에 대응 합을 걸 수 있다.
/// - 원래 대상이 아닌 캐릭터가 공격을 가로채려면 자신의 속도가 적 행동보다 반드시 높아야 한다.
/// - 같은 속도는 가로채기로 인정하지 않는다.
/// - 가로채기 조건을 만족하지 않아도 공격 자체는 취소하지 않는다. 두 행동은 각각 일방 공격으로 남을 수 있다.
/// - TargetSlot이 지정된 행동은 그 슬롯 외의 행동과 자동으로 합하지 않는다.
/// - 적이 나를 공격하고 있다는 이유만으로 내가 다른 적에게 지정한 행동을 자동으로 낚아채 합으로 바꾸지 않는다.
/// - 여러 유효 행동이 같은 적 슬롯을 노리면 ActionPhaseSorter의 실행 우선순위가 높은 행동이 먼저 슬롯을 점유한다.
/// </summary>
public sealed class ClashMatchPolicy
{
    private readonly ActionPhaseSorter phaseSorter;

    public ClashMatchPolicy(
        ActionPhaseSorter phaseSorter)
    {
        this.phaseSorter =
            phaseSorter ??
            new ActionPhaseSorter();
    }

    public bool CanEnterClash(
        ActionSlot slot)
    {
        if (slot == null ||
            slot.Owner == null ||
            slot.TargetCharacter == null ||
            slot.Skill == null)
        {
            return false;
        }

        if (slot.Phase !=
            ActionPhase.COMBAT)
        {
            return false;
        }

        if (!slot.Skill.CanClash)
            return false;

        if (slot.Owner.IsDead ||
            slot.TargetCharacter.IsDead)
        {
            return false;
        }

        // 독립 행동/일반몹 행동을 위해 Part == null 자체는 허용한다.
        if (slot.Part != null &&
            slot.Part.IsBroken)
        {
            return false;
        }

        // TargetPart가 Broken이어도 공격 및 합 후보가 될 수 있다.
        return true;
    }

    /// <summary>
    /// Focused Encounter식 가로채기 가능 여부.
    ///
    /// Project Abyss는 한 Character 안의 BodyPart가 각각 행동 슬롯이므로
    /// '원래 공격 대상' 예외도 Character 단위가 아니라 정확한 BodyPart 단위로 판정한다.
    ///
    /// - incoming이 challenger의 정확한 행동 부위를 노림: 속도와 무관하게 대응 합 가능
    /// - 다른 부위를 노림: 제3자 가로채기로 취급하며 challengerSpeed > incoming.Speed 필요
    /// - 동속/느림: 가로채기 실패 -> 서로 일방공격
    /// </summary>
    public static bool CanRedirectOrOppose(
        Character challengerOwner,
        BodyPart challengerPart,
        int challengerSpeed,
        ActionSlot incoming,
        out bool isRedirect)
    {
        isRedirect = false;

        if (challengerOwner == null ||
            incoming == null ||
            incoming.Owner == null ||
            incoming.TargetCharacter == null ||
            incoming.Skill == null ||
            incoming.Phase != ActionPhase.COMBAT ||
            !incoming.Skill.CanClash ||
            incoming.Owner.IsDead ||
            incoming.TargetCharacter.IsDead)
        {
            return false;
        }

        if (incoming.Part != null &&
            incoming.Part.IsBroken)
        {
            return false;
        }

        // 림버스의 "원래 그 슬롯을 노리던 공격에 대한 대응" 예외.
        // 플레이어 전체 Character만 같다고 원래 대상으로 취급하면
        // 머리를 노린 적 공격을 느린 오른팔이 받아 합하는 잘못된 가로채기가 발생한다.
        if (IsOriginalTargetSlot(
                challengerOwner,
                challengerPart,
                incoming))
        {
            return true;
        }

        // 다른 부위의 공격을 가져오는 것은 가로채기다.
        // 반드시 더 빨라야 하며 동속은 실패한다.
        if (challengerSpeed > incoming.Speed)
        {
            isRedirect = true;
            return true;
        }

        return false;
    }


    /// <summary>
    /// challenger가 incoming 행동을 실제 합 상대로 가져갈 수 있는지 검사한다.
    /// 대상 의도(TargetSlot 또는 TargetCharacter/TargetPart)와 속도 가로채기 규칙을 함께 본다.
    /// </summary>
    public bool CanChallenge(
        ActionSlot challenger,
        ActionSlot incoming)
    {
        if (!CanEnterClash(challenger) ||
            !CanEnterClash(incoming) ||
            challenger == incoming)
        {
            return false;
        }

        if (!TargetsActionSource(
                challenger,
                incoming))
        {
            return false;
        }

        return CanRedirectOrOppose(
            challenger.Owner,
            challenger.Part,
            challenger.Speed,
            incoming,
            out _);
    }

    public ActionSlot FindBestMatch(
        ActionSlot source,
        IReadOnlyList<ActionSlot> combatSlots,
        HashSet<ActionSlot> usedSlots)
    {
        if (!CanEnterClash(source) ||
            combatSlots == null)
        {
            return null;
        }

        // 1) 정확한 상대 슬롯을 지정한 행동은 TargetSlot이 절대 우선한다.
        // 합이 성립하지 않더라도 다른 적 행동이 이 source를 공격한다는 이유로
        // 자동 재매칭하지 않는다. 지정한 대상에 대한 일방 공격으로 남는다.
        if (source.TargetSlot != null)
        {
            if (source.TargetSlot == source ||
                (usedSlots != null &&
                 usedSlots.Contains(source.TargetSlot)))
            {
                return null;
            }

            return CanChallenge(
                    source,
                    source.TargetSlot)
                ? source.TargetSlot
                : null;
        }

        // 2) 반대편 행동이 이 source를 정확히 지정한 경우.
        // 느린 제3자가 TargetSlot만 꽂았다고 해서 역방향에서 합이 성립하면 안 된다.
        foreach (ActionSlot candidate in combatSlots)
        {
            if (candidate == null || candidate == source)
                continue;

            if (usedSlots != null && usedSlots.Contains(candidate))
                continue;

            if (IsSameSlot(
                    candidate.TargetSlot,
                    source) &&
                CanChallenge(
                    candidate,
                    source))
            {
                return candidate;
            }
        }

        // 3) Focused Encounter에서는 합 상대를 TargetPart로 추론하지 않는다.
        // 정확한 ActionSlot(TargetSlot)을 지정하지 않은 행동은 자동 합 후보가 아니다.
        return null;
    }

    private bool TargetsActionSource(
        ActionSlot challenger,
        ActionSlot incoming)
    {
        if (challenger == null ||
            incoming == null ||
            challenger.TargetCharacter != incoming.Owner)
        {
            return false;
        }

        // 정확한 슬롯을 명시했다면 그 슬롯 외의 다른 행동과는 합하지 않는다.
        if (challenger.TargetSlot != null)
        {
            return IsSameSlot(
                challenger.TargetSlot,
                incoming);
        }

        // Focused Encounter의 합은 정확한 ActionSlot 지정으로만 성립한다.
        return false;
    }

    private static bool IsOriginalTargetSlot(
        Character challengerOwner,
        BodyPart challengerPart,
        ActionSlot incoming)
    {
        if (challengerOwner == null ||
            incoming == null ||
            incoming.TargetCharacter != challengerOwner)
        {
            return false;
        }

        // 단일 HP 대상처럼 부위 개념이 없는 전투원.
        if (incoming.TargetPart == null)
            return challengerPart == null;

        // BodyPart 전투원은 정확히 적이 노린 그 부위의 행동만
        // 속도 무관 대응 예외를 받는다.
        return IsSamePart(
            challengerPart,
            incoming.TargetPart);
    }

    private static bool IsSameSlot(
        ActionSlot a,
        ActionSlot b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        return a.ActionId > 0 &&
               b.ActionId > 0 &&
               a.ActionId == b.ActionId &&
               a.Owner == b.Owner;
    }

    private static bool IsSamePart(
        BodyPart a,
        BodyPart b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        return a.Owner == b.Owner &&
               a.Type == b.Type;
    }
}