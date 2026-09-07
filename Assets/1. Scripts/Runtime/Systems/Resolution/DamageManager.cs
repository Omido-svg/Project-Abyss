using UnityEngine;

public class DamageManager
{
    private readonly BattleContext battleContext;
    private readonly MomentumManager momentumManager;

    private readonly DamagePipeline pipeline;
    private readonly DamageEventDispatcher eventDispatcher;

    public DamageManager(
        BattleContext battleContext,
        MomentumManager momentumManager)
    {
        this.battleContext = battleContext;
        this.momentumManager = momentumManager;

        pipeline =
            new DamagePipeline(
                momentumManager);

        eventDispatcher =
            new DamageEventDispatcher(
                battleContext);
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
            ShouldBreakPart(action),
            isClashDamage,
            targetLostClash);

        // 판정 보정은 피해 기준 위력에 섞지 않습니다.
        // 유효한 공격의 시작 위력은 최소 1입니다.
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
            (action.ActionType != ActionType.NormalAttack &&
             action.ActionType != ActionType.Duel) ||
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

        float damageCoefficient =
            Mathf.Max(
                0f,
                secondaryDamageMultiplier);

        if (damageCoefficient <= 0f)
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
                ShouldBreakPart(
                    action,
                    targetPart),
                isClashDamage: false,
                targetLostClash: false);

        request.DamageCoefficient =
            damageCoefficient;

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

        pipeline.Calculate(context);

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
        // 실제 적용 단계에서 한 번만 가드 수치를 차감합니다.
        if (runtime != null &&
            context.GuardAbsorbed > 0)
        {
            runtime.currentBlock =
                context.GuardAfter;
        }

        if (context.FinalDamage <= 0)
        {
            context.WasApplied =
                context.GuardAbsorbed > 0;

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

        // 약화는 아직 부위 피해 경로다. 파괴된 부위만 직접 HP 경로다.
        context.WasDirectHPDamage =
            context.TargetPartStateBefore ==
            BodyPartState.Broken;
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

        // 표준 Action 기반 피해는 실제 공격 행동만 사용한다.
        // 도사림/위세가 피해를 주려면 SkillEffect에서 명시적 DamageRequest를 만든다.
        if (action.ActionType != ActionType.NormalAttack &&
            action.ActionType != ActionType.Duel)
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

        if (request.TargetPart != null &&
            request.TargetPart.Owner != null &&
            request.TargetPart.Owner !=
            request.TargetCharacter)
        {
            Debug.LogError(
                "[DamageManager] 피해 대상 캐릭터와 대상 부위의 Owner가 다릅니다. " +
                $"Source={GetCharacterName(request.SourceCharacter)}, " +
                $"Target={GetCharacterName(request.TargetCharacter)}, " +
                $"PartOwner={GetCharacterName(request.TargetPart.Owner)}, " +
                $"Part={request.TargetPart.Type}");

            return false;
        }

        BattleAction sourceAction =
            request.SourceAction;

        if (request.IsClashDamage &&
            sourceAction != null &&
            (request.TargetCharacter != sourceAction.Target ||
             request.TargetPart != sourceAction.TargetPart))
        {
            Debug.LogError(
                "[DamageManager] 합 피해 요청이 BattleAction의 해석 타깃과 다릅니다. " +
                $"ActionId={sourceAction.ActionId}, " +
                $"Owner={GetCharacterName(sourceAction.Owner)}, " +
                $"ActionTarget={GetCharacterName(sourceAction.Target)}/" +
                $"{GetPartName(sourceAction.TargetPart)}, " +
                $"RequestTarget={GetCharacterName(request.TargetCharacter)}/" +
                $"{GetPartName(request.TargetPart)}");

            return false;
        }

        if (request.Damage < 0 ||
            request.RawPower < 0)
        {
            return false;
        }

        if (sourceAction?.OwnerPart != null &&
            sourceAction.OwnerPart.IsBroken)
        {
            return false;
        }

        return true;
    }

    private static string GetCharacterName(
        Character character)
    {
        return character?.Data?.CharacterName ??
               character?.name ??
               "NULL";
    }

    private static string GetPartName(
        BodyPart part)
    {
        return part == null
            ? "CHARACTER"
            : part.Type.ToString();
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

        // Stage 1 보스 확정 규칙: Boss는 기세와 무관하게 부위 파괴 권한을 항상 가진다.
        if (action?.Owner?.Data?.CombatantTier == CombatantTier.Boss)
            return true;

        // 그 외 적은 기존 규칙대로 짓누름에서만 표준 파괴 권한을 얻는다.
        if (action?.Owner is Enemy)
        {
            return momentumManager?.CanStandardBreakPart(action.Owner) == true;
        }

        return action?.Skill?.CanBreakPart == true;
    }
}