using UnityEngine;

public class DamageManager
{
    private readonly BattleContext battleContext;
    private readonly MomentumManager momentumManager;

    private readonly DamageCalculator calculator;
    private readonly DamageEventDispatcher eventDispatcher;

    public DamageManager(
        BattleContext battleContext,
        MomentumManager momentumManager)
    {
        this.battleContext = battleContext;
        this.momentumManager = momentumManager;

        calculator =
            new DamageCalculator(
                momentumManager,
                battleContext?.Rules);

        eventDispatcher =
            new DamageEventDispatcher(
                battleContext);
    }

    //--------------------------------
    // 기존 호출부 호환 API
    //--------------------------------

    public int ApplyDamage(
        BattleAction action)
    {
        DamageContext context =
            ApplyDamageContext(action);

        return context?.GetDisplayDamage() ?? 0;
    }

    //--------------------------------
    // BattleAction 기반 표준 API
    //--------------------------------

    public DamageContext ApplyDamageContext(
        BattleAction action,
        bool isClashDamage = false,
        bool targetLostClash = false)
    {
        if (!IsValidAction(action))
            return null;

        DamageType damageType =
            ResolveActionDamageType(action);

        DamageRequest request =
            DamageRequest.FromAction(
                action,
                damageType,
                GetSkillMultiplier(action),
                ShouldBreakPart(action),
                isClashDamage,
                targetLostClash);

        return ApplyDamageContext(request);
    }

    public DamageContext ApplyDamageContext(
        BattleAction action,
        int rawPower,
        bool isClashDamage,
        bool targetLostClash,
        bool applyMomentum = true)
    {
        if (!IsValidAction(action))
            return null;

        DamageRequest request = DamageRequest.FromAction(
            action,
            ResolveActionDamageType(action),
            GetSkillMultiplier(action),
            ShouldBreakPart(action),
            isClashDamage,
            targetLostClash);

        // 합에서 공격 굴림이 승리해 이 API에 도달했다면,
        // 순수 굴림값이 0 이하라도 실제 피해는 최소 1에서 시작한다.
        // 판정값 보정은 rawPower에 섞지 않는다.
        int resolvedRawPower = Mathf.Max(1, rawPower);
        request.Damage = resolvedRawPower;
        request.RawPower = resolvedRawPower;
        request.ApplyMomentum = applyMomentum;
        return ApplyDamageContext(request);
    }

    public DamageContext ApplyAttackWeightDamageContext(
        BattleAction action,
        AttackWeightTarget weightedTarget,
        int rawPower,
        float secondaryDamageMultiplier,
        bool applyMomentum = true)
    {
        if (action == null ||
            action.Owner == null ||
            action.Owner.IsDead ||
            action.Skill == null ||
            weightedTarget == null ||
            weightedTarget.IsPrimary ||
            weightedTarget.TargetCharacter == null ||
            weightedTarget.TargetCharacter.IsDead)
        {
            return null;
        }

        BodyPart targetPart =
            weightedTarget.TargetPart;

        if (targetPart != null &&
            targetPart.Owner != null &&
            targetPart.Owner !=
            weightedTarget.TargetCharacter)
        {
            return null;
        }

        float skillMultiplier =
            GetSkillMultiplier(action) *
            Mathf.Max(
                0f,
                secondaryDamageMultiplier);

        if (skillMultiplier <= 0f)
            return null;

        int resolvedRawPower =
            Mathf.Max(
                1,
                rawPower);

        DamageRequest request =
            DamageRequest.FromAction(
                action,
                ResolveActionDamageType(
                    action,
                    targetPart),
                skillMultiplier,
                ShouldBreakPart(
                    action,
                    targetPart),
                isClashDamage: false,
                targetLostClash: false);

        request.TargetCharacter =
            weightedTarget.TargetCharacter;

        request.TargetPart =
            targetPart;

        request.Damage =
            resolvedRawPower;

        request.RawPower =
            resolvedRawPower;

        request.IsClashDamage =
            false;

        request.TargetLostClash =
            false;

        request.IsPrestigeClash =
            false;

        request.ApplyMomentum =
            applyMomentum;

        return ApplyDamageContext(
            request);
    }

    //--------------------------------
    // 상태이상·반격·자해·처형까지 사용하는 범용 API
    //--------------------------------

    public DamageContext ApplyDamageContext(
        DamageRequest request)
    {
        if (!IsValidRequest(request))
            return null;

        DamageContext context =
            new DamageContext(request);

        CaptureBeforeSnapshot(context);

        calculator.Calculate(context);

        Character target =
            context.Target;

        target?.BeginDamageResolution(context);

        try
        {
            ApplyResolvedDamage(context);
        }
        finally
        {
            target?.EndDamageResolution(context);
        }

        CaptureAfterSnapshot(context);

        context.RecordStage(
            DamageStage.Applied,
            context.AppliedDamage);

        context.Result =
            DamageResult.FromContext(context);

        // 이벤트 구독자가 SourceAction에서 현재 피해를 읽을 수 있게
        // 공통 결과를 먼저 연결한다.
        if (context.Action != null)
        {
            context.Action.SetDamageContext(
                context);
        }

        context.EventResult =
            eventDispatcher.DispatchResolved(
                context);

        // 상세 이벤트 결과까지 Action에 보존한다.
        if (context.Action != null)
        {
            context.Action.SetDamageContext(
                context);
        }

        return context;
    }

    private void ApplyResolvedDamage(
        DamageContext context)
    {
        if (context?.Target == null)
            return;

        RuntimeStatus runtime =
            context.Target.RuntimeStatus;

        // 계산 단계에서는 값만 보존하고,
        // 실제 적용 단계에서 한 번만 방어도를 차감한다.
        if (runtime != null &&
            context.GuardAbsorbed > 0)
        {
            runtime.currentBlock =
                context.GuardAfter;
        }

        if (context.FinalDamage <= 0)
        {
            context.WasApplied =
                context.GuardAbsorbed > 0 ||
                context.ProtectionAbsorbed > 0;

            return;
        }

        BattleEffectResolver resolver =
            battleContext?.EffectResolver;

        if (resolver != null)
        {
            context.WasApplied =
                resolver.ApplyDamage(
                    EffectRequest.Damage(context));

            return;
        }

        context.WasApplied =
            ApplyDamageFallback(context);
    }

    private bool ApplyDamageFallback(
        DamageContext context)
    {
        if (context?.Target == null ||
            context.FinalDamage <= 0)
        {
            return false;
        }

        switch (context.DamageType)
        {
            case DamageType.True:
                context.Target.TakeTrueDamage(
                    context.FinalDamage,
                    context.Request.SourceEffect);
                return true;

            case DamageType.StatusPart:
                if (context.TargetPart == null)
                    return false;

                context.Target.TakeStatusPartDamage(
                    context.TargetPart,
                    context.FinalDamage,
                    context.Request.SourceEffect);
                return true;

            case DamageType.Direct:
            case DamageType.SelfCost:
            case DamageType.Execution:
                context.Target.TakeDirectDamage(
                    context.FinalDamage,
                    context.Attacker,
                    context.Action);
                return true;
        }

        if (context.TargetPart != null)
        {
            context.Target.TakeDamage(
                context.TargetPart,
                context.FinalDamage,
                context.CanBreakPart);

            return true;
        }

        context.Target.TakeDirectDamage(
            context.FinalDamage,
            context.Attacker,
            context.Action);

        return true;
    }

    private void CaptureBeforeSnapshot(
        DamageContext context)
    {
        if (context?.Target == null)
            return;

        context.TargetWasDeadBefore =
            context.Target.IsDead;

        context.TargetHpBefore =
            context.Target.CurrentHP;

        context.HasTargetPartSnapshot =
            context.TargetPart != null;

        if (!context.HasTargetPartSnapshot)
        {
            context.WasDirectHPDamage = true;
            return;
        }

        context.TargetPartHpBefore =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    context.TargetPart.PartHP));

        context.TargetPartStateBefore =
            context.TargetPart.State;

        context.WasDirectHPDamage =
            context.TargetPartStateBefore !=
            BodyPartState.Normal;
    }

    private void CaptureAfterSnapshot(
        DamageContext context)
    {
        if (context?.Target == null)
            return;

        context.TargetHpAfter =
            context.Target.CurrentHP;

        context.AppliedHpDamage =
            Mathf.Max(
                0,
                context.TargetHpBefore -
                context.TargetHpAfter);

        context.FinalHpDamage =
            context.AppliedHpDamage;

        context.TargetWasDeadAfter =
            context.Target.IsDead;

        context.WasKilled =
            !context.TargetWasDeadBefore &&
            context.TargetWasDeadAfter;

        if (context.HasTargetPartSnapshot &&
            context.TargetPart != null)
        {
            context.TargetPartHpAfter =
                Mathf.Max(
                    0,
                    Mathf.RoundToInt(
                        context.TargetPart.PartHP));

            context.TargetPartStateAfter =
                context.TargetPart.State;

            context.AppliedPartDamage =
                Mathf.Max(
                    0,
                    context.TargetPartHpBefore -
                    context.TargetPartHpAfter);

            context.PartHpDamage =
                context.AppliedPartDamage;

            context.WasWeakened =
                context.TargetPartStateBefore !=
                    BodyPartState.Weakened &&
                context.TargetPartStateAfter ==
                    BodyPartState.Weakened;

            context.WasBroken =
                context.TargetPartStateBefore !=
                    BodyPartState.Broken &&
                context.TargetPartStateAfter ==
                    BodyPartState.Broken;
        }

        bool directRoute =
            !context.HasTargetPartSnapshot ||
            context.WasDirectHPDamage ||
            context.AppliedPartDamage <= 0;

        context.DirectHpDamage =
            directRoute
                ? context.AppliedHpDamage
                : 0;

        int observedDamage =
            context.DirectHpDamage > 0
                ? context.DirectHpDamage
                : Mathf.Max(
                    context.AppliedHpDamage,
                    context.AppliedPartDamage);

        // 파괴 정산 등 부가 HP 변화가 섞여도
        // 이 요청의 표시 피해가 FinalDamage보다 커지지 않게 한다.
        context.AppliedDamage =
            Mathf.Min(
                context.FinalDamage,
                observedDamage);

        if (context.FinalDamage > 0 &&
            context.WasApplied &&
            context.AppliedDamage <= 0)
        {
            context.AppliedDamage =
                context.FinalDamage;
        }
    }

    private bool IsValidAction(
        BattleAction action)
    {
        if (action == null ||
            action.Owner == null ||
            action.Target == null ||
            action.Skill == null)
        {
            return false;
        }

        if (action.Owner.IsDead ||
            action.Target.IsDead)
        {
            return false;
        }

        if (action.OwnerPart != null &&
            action.OwnerPart.IsBroken)
        {
            return false;
        }

        return true;
    }

    private bool IsValidRequest(
        DamageRequest request)
    {
        if (request.TargetCharacter == null)
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        if (request.Damage < 0 ||
            request.RawPower < 0)
        {
            return false;
        }

        if (request.SourceAction?.OwnerPart != null &&
            request.SourceAction.OwnerPart.IsBroken)
        {
            return false;
        }

        return true;
    }

    private DamageType ResolveActionDamageType(
        BattleAction action)
    {
        return ResolveActionDamageType(
            action,
            action?.TargetPart);
    }

    private DamageType ResolveActionDamageType(
        BattleAction action,
        BodyPart targetPart)
    {
        if (action == null)
            return DamageType.SkillPart;

        if (action.ActionType == ActionType.Prestige)
            return DamageType.Prestige;

        return targetPart == null
            ? DamageType.Direct
            : DamageType.SkillPart;
    }

    private float GetSkillMultiplier(
        BattleAction action)
    {
        if (action == null)
            return 0f;

        return action.ActionType switch
        {
            ActionType.Prestige => 0f,
            ActionType.Preparation => 0f,
            ActionType.NormalAttack => 1f,
            ActionType.Duel => 1f,
            _ => 1f
        };
    }

    private bool ShouldBreakPart(
        BattleAction action)
    {
        return ShouldBreakPart(
            action,
            action?.TargetPart);
    }

    private bool ShouldBreakPart(
        BattleAction action,
        BodyPart targetPart)
    {
        if (targetPart == null ||
            !targetPart.IsWeakened)
        {
            return false;
        }

        // 적은 파괴 스킬 권한을 갖지 않는다. 다만 짓누름(+70 이상)에서는
        // 약화 부위를 기세만으로 파괴할 수 있다.
        if (action?.Owner is Enemy)
        {
            return
                momentumManager?
                    .CanStandardBreakPart(
                        action.Owner) == true;
        }

        return action?.Skill?.CanBreakPart == true;
    }
}