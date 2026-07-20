using System.Collections.Generic;
using UnityEngine;

public class OlafMadnessMechanic : CombatMechanic
{
    private const int MaxMadnessValue = 5;

    private const int DuelLoseMadnessGain = 2;
    private const int PartBreakMadnessGain = 1;

    private const int ExchangeBleedAmount = 1;
    private const int DuelPartBreakBleedThreshold = 5;

    private const int BleedExplosionDamagePerStack = 5;
    private const int DefaultPrestigeDamagePerMadness = 5;

    private int madness;
    private int suppressPartBreakMadnessDepth;

    // 위세 효과가 최대 광기를 먼저 소모하더라도
    // 같은 행동의 처치 보상 자격은 Kill 이벤트까지 보존한다.
    private readonly HashSet<long>
        killRewardEligibleActionIds = new();

    public int CurrentMadness => madness;
    public int MaxMadness => MaxMadnessValue;

    public override string MechanicName =>
        "광전사의 광기";

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnClashWin += OnClashWin,
            () => battleEvent.OnClashWin -= OnClashWin,
            "OnClashWin");

        SubscribeToBattleEvent(
            () => battleEvent.OnDamageResolved += OnDamageResolved,
            () => battleEvent.OnDamageResolved -= OnDamageResolved,
            "OnDamageResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnClashLose += OnClashLose,
            () => battleEvent.OnClashLose -= OnClashLose,
            "OnClashLose");

        SubscribeToBattleEvent(
            () => battleEvent.OnBodyPartBreakResolved += OnBodyPartBreakResolved,
            () => battleEvent.OnBodyPartBreakResolved -= OnBodyPartBreakResolved,
            "OnBodyPartBreakResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnKillResolved += OnKillResolved,
            () => battleEvent.OnKillResolved -= OnKillResolved,
            "OnKillResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnActionEnd += OnActionEnd,
            () => battleEvent.OnActionEnd -= OnActionEnd,
            "OnActionEnd");
    }

    public override void OnUnregister()
    {
        killRewardEligibleActionIds.Clear();
        suppressPartBreakMadnessDepth = 0;
        madness = 0;
    }

    private void OnClashWin(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
        if (winnerAction?.Owner != owner ||
            winnerAction.Skill == null ||
            winnerAction.ActionType != ActionType.Duel ||
            loserAction?.ActionType != ActionType.Duel)
        {
            return;
        }

        ApplyDuelWinEffect(
            winnerAction);
    }

    private void ApplyDuelWinEffect(
        BattleAction action)
    {
        if (action?.Target == null ||
            action.TargetPart == null ||
            action.TargetPart.IsBroken ||
            owner?.BattleContext?.EffectResolver == null)
        {
            return;
        }

        Bleeding bleeding =
            action.Target.GetPartStatus<Bleeding>(
                action.TargetPart);

        int stacks = Mathf.Max(0, bleeding?.Stack ?? 0);
        if (stacks < DuelPartBreakBleedThreshold)
            return;

        EffectRequest request = EffectRequest.ForceBreak(
            owner,
            action.Target,
            action.TargetPart);
        request.SourceAction = action;

        bool broke = owner.BattleContext.EffectResolver
            .ForceBreakPart(request);

        if (!broke)
            return;

        RemoveBleeding(
            action.Target,
            action.TargetPart,
            bleeding);

        Debug.Log(
            $"{owner.Data?.CharacterName} 고유 파괴 루트 / " +
            $"출혈 {stacks} + 결투 승리로 " +
            $"{action.Target.Data?.CharacterName} {action.TargetPart.Type} 파괴");
    }

    private void OnDamageResolved(DamageContext context)
    {
        if (context?.Attacker != owner ||
            context.Action == null ||
            !context.IsClashDamage ||
            context.Target == null ||
            context.Target.IsDead)
        {
            return;
        }

        if (context.Action.ActionType != ActionType.NormalAttack &&
            context.Action.ActionType != ActionType.Duel)
        {
            return;
        }

        // 합에서 이긴 교환 한 번마다 출혈 1스택.
        // 피해가 방어도에 전부 흡수되어도 교환 승리 자체가 연료다.
        ApplyBleeding(
            context.Target,
            context.TargetPart,
            ExchangeBleedAmount);
    }

    private void OnClashLose(
        BattleAction loserAction,
        BattleAction winnerAction)
    {
        if (loserAction?.Owner != owner ||
            loserAction.Skill == null ||
            loserAction.ActionType != ActionType.Duel)
        {
            return;
        }

        AddMadness(
            DuelLoseMadnessGain);

        Debug.Log(
            $"{owner.Data.CharacterName} 결투 패배 : " +
            $"광기 {DuelLoseMadnessGain} 증가");
    }

    private void OnBodyPartBreakResolved(
        BodyPartBreakEventContext context)
    {
        if (context?.Target != owner ||
            context.Part == null)
        {
            return;
        }

        if (suppressPartBreakMadnessDepth > 0)
        {
            Debug.Log(
                $"{owner.Data.CharacterName} 억제 구간의 부위 파괴 : " +
                "광기 증가 무시");
            return;
        }

        AddMadness(
            PartBreakMadnessGain);
    }

    private void OnKillResolved(
        KillEventContext context)
    {
        if (context?.Killer != owner ||
            context.Victim == null)
        {
            return;
        }

        bool preservedEligibility =
            ConsumeKillRewardEligibility(
                context.SourceAction);

        if (!IsMaxMadness() &&
            !preservedEligibility)
        {
            return;
        }

        int recoveredCount =
            RecoverBrokenOrWeakenedParts(
                2);

        ResetMadness();

        Debug.Log(
            $"{owner.Data.CharacterName} 광기 5스택 처치 보상 : " +
            $"부위 {recoveredCount}개 회복");
    }

    private void OnActionEnd(
        BattleAction action)
    {
        if (action == null)
            return;

        // 처치가 없었던 행동의 임시 자격은 행동 종료 시 폐기한다.
        killRewardEligibleActionIds.Remove(
            action.ActionId);
    }

    private void ApplyBleeding(
        Character target,
        BodyPart targetPart,
        int amount)
    {
        if (target == null ||
            amount <= 0)
        {
            return;
        }

        BattleEffectResolver resolver =
            owner.BattleContext?.EffectResolver;

        if (resolver == null)
            return;

        Bleeding bleeding =
            new Bleeding(amount);

        if (targetPart != null &&
            !targetPart.IsBroken)
        {
            resolver.ApplyBodyPartStatus(
                EffectRequest.BodyPartStatus(
                    owner,
                    target,
                    targetPart,
                    bleeding));
        }
        else
        {
            resolver.ApplyCharacterStatus(
                EffectRequest.CharacterStatus(
                    owner,
                    target,
                    bleeding));
        }
    }

    private void TryExplodeBleedingByDuel(
        BattleAction action)
    {
        if (action?.Target == null)
            return;

        Bleeding bleeding =
            action.TargetPart != null
                ? action.Target.GetPartStatus<Bleeding>(
                    action.TargetPart)
                : action.Target.GetStatus<Bleeding>();

        if (bleeding == null ||
            !bleeding.CanExplode)
        {
            return;
        }

        int explosionDamage =
            Mathf.Max(0, bleeding.Stack) *
            BleedExplosionDamagePerStack;

        DamageType damageType =
            action.TargetPart == null
                ? DamageType.BleedExplosion
                : DamageType.StatusPart;

        DamageRequest request =
            DamageRequest.Custom(
                damageType,
                owner,
                action.Target,
                action.TargetPart,
                explosionDamage,
                1f,
                false,
                false,
                false,
                false,
                false,
                action,
                bleeding);

        OlafCombatPipeline.ApplyDamage(
            owner,
            request);

        RemoveBleeding(
            action.Target,
            action.TargetPart,
            bleeding);

        Debug.Log(
            $"{owner.Data.CharacterName} 출혈 폭발 : " +
            $"{action.Target.Data.CharacterName}에 " +
            $"{explosionDamage} 피해 / 부위 파괴 불가");
    }

    private void RemoveBleeding(
        Character target,
        BodyPart targetPart,
        Bleeding bleeding)
    {
        if (target == null ||
            bleeding == null)
        {
            return;
        }

        if (targetPart != null)
        {
            owner.BattleContext?.EffectResolver
                ?.RemoveBodyPartStatus(
                    EffectRequest.RemoveBodyPartStatus(
                        owner,
                        target,
                        targetPart,
                        bleeding));
            return;
        }

        target.RemoveStatus(
            bleeding,
            StatusEffectRemoveReason.Manual);
    }

    private int RecoverBrokenOrWeakenedParts(
        int maximumCount)
    {
        if (owner?.BodyParts == null ||
            maximumCount <= 0 ||
            owner.BattleContext?.EffectResolver == null)
        {
            return 0;
        }

        List<BodyPart> candidates = new();

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part != null &&
                (part.IsBroken || part.IsWeakened))
            {
                candidates.Add(part);
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    candidates.Count);

            (candidates[i], candidates[randomIndex]) =
                (candidates[randomIndex], candidates[i]);
        }

        int recoveredCount = 0;
        int attemptCount =
            Mathf.Min(
                maximumCount,
                candidates.Count);

        for (int i = 0; i < attemptCount; i++)
        {
            BodyPart part =
                candidates[i];

            bool recovered =
                owner.BattleContext.EffectResolver
                    .RecoverPart(
                        EffectRequest.RecoverPart(
                            owner,
                            owner,
                            part));

            if (!recovered)
                continue;

            recoveredCount++;

            Debug.Log(
                $"{owner.Data.CharacterName} 광기 효과 : " +
                $"{part.Type} 부위 회복");
        }

        return recoveredCount;
    }

    public int GetNormalAttackBleedAmount() => ExchangeBleedAmount;

    public int GetDuelWinBleedAmount() => 0;

    public int GetDuelPushBonus() => 0;

    public int ConsumeMadnessForPrestigeDamage()
    {
        return ConsumeMadnessForPrestigeDamage(
            null,
            DefaultPrestigeDamagePerMadness,
            true);
    }

    public int ConsumeMadnessForPrestigeDamage(
        BattleAction sourceAction,
        int damagePerMadness,
        bool consumeMadness)
    {
        int stack =
            Mathf.Max(0, madness);

        int damage =
            stack *
            Mathf.Max(0, damagePerMadness);

        if (!consumeMadness ||
            stack <= 0)
        {
            return damage;
        }

        if (stack >= MaxMadness &&
            sourceAction != null)
        {
            killRewardEligibleActionIds.Add(
                sourceAction.ActionId);
        }

        ResetMadness();
        return damage;
    }

    public void ClearMadness()
    {
        ResetMadness();
    }

    public void AddMadness(int amount)
    {
        if (amount <= 0)
            return;

        madness =
            Mathf.Clamp(
                madness + amount,
                0,
                MaxMadness);

        Debug.Log(
            $"{owner.Data.CharacterName} 광기 증가 : " +
            $"{madness}/{MaxMadness}");
    }

    public void ResetMadness()
    {
        madness = 0;

        Debug.Log(
            $"{owner.Data.CharacterName} 광기 초기화 : " +
            $"{madness}/{MaxMadness}");
    }

    public bool IsMaxMadness()
    {
        return madness >= MaxMadness;
    }

    public void SetMadnessToMax()
    {
        madness = MaxMadness;

        Debug.Log(
            $"{owner.Data.CharacterName} 광기 최대치 : " +
            $"{madness}/{MaxMadness}");
    }

    public void SetMadnessForDebug(
        int value)
    {
        madness =
            Mathf.Clamp(
                value,
                0,
                MaxMadness);
    }

    public void BeginSuppressPartBreakMadness()
    {
        suppressPartBreakMadnessDepth++;
    }

    public void EndSuppressPartBreakMadness()
    {
        suppressPartBreakMadnessDepth =
            Mathf.Max(
                0,
                suppressPartBreakMadnessDepth - 1);
    }

    private bool ConsumeKillRewardEligibility(
        BattleAction sourceAction)
    {
        if (sourceAction == null)
            return false;

        return killRewardEligibleActionIds.Remove(
            sourceAction.ActionId);
    }

    private bool IsImmortalFuryActive()
    {
        OlafImmortalFuryMechanic fury =
            owner?.GetMechanic<OlafImmortalFuryMechanic>();

        return fury != null &&
               fury.IsActive;
    }
}
