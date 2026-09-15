public sealed class ActionPlanAssignmentRequest
{
    public Character Owner;
    public BodyPart OwnerPart;
    public Skill Skill;
    public int ActionIndex;

    public Character Target;
    public BodyPart TargetPart;
    public TargetSelectionRule TargetRule;
    public ActionSlot TargetSlot;
    public string PlanningChoiceId;
}

public sealed class ActionPlanAssignmentResult
{
    public bool Success;
    public string FailureReason;
    public ActionSlot PreviousSlot;
    public ActionSlot Slot;
}

public sealed class PrestigePlanningExecutionResult
{
    public bool Success;
    public string FailureReason;
    public BattleAction Action;
}

/// <summary>
/// Planning ActionSlot의 생성/교체/재지정/삭제를 소유하는 command service.
/// UI는 입력 상태만 결정하고 ActionManager mutation 세부사항을 직접 수행하지 않는다.
/// </summary>
public sealed class BattleActionPlanCommandService
{
    private readonly ActionManager actionManager;
    private readonly SpeedManager speedManager;
    private readonly ActionPlanValidator validator;
    private readonly PlanningActionCancellationService cancellationService;

    public BattleActionPlanCommandService(
        ActionManager actionManager,
        SpeedManager speedManager)
    {
        this.actionManager = actionManager;
        this.speedManager = speedManager;
        validator = new ActionPlanValidator(actionManager);
        cancellationService =
            new PlanningActionCancellationService(
                actionManager);
    }

    public ActionPlanAssignmentResult TryAssign(
        ActionPlanAssignmentRequest request)
    {
        ActionPlanAssignmentResult result = new();

        if (actionManager == null || speedManager == null)
            return Fail(result, "Planning service가 준비되지 않았습니다.");

        ActionPlanValidationResult validation =
            validator.ValidateAssignment(request);

        if (!validation.Success)
            return Fail(result, validation.Reason);

        result.PreviousSlot =
            actionManager.FindSlot(
                request.Owner,
                request.OwnerPart,
                request.ActionIndex);

        // 이미 계획 단계에서 실행된 도사림은 그 부위의 이번 턴 행동을 소비한 상태다.
        // 교체/취소로 두 번째 행동을 꽂아 즉시 효과를 중복 실행할 수 없게 잠근다.
        if (result.PreviousSlot?.PlanningEffectCommitted == true)
            return Fail(result, "이미 계획 단계에서 실행된 행동 슬롯은 이번 턴에 교체할 수 없습니다.");

        ActionSlot slot = new()
        {
            Owner = request.Owner,
            Part = request.OwnerPart,
            Skill = request.Skill,
            TargetCharacter = request.Target,
            TargetPart = request.TargetPart,
            Speed = speedManager.GetSpeed(
                request.Owner,
                request.OwnerPart),
            ActionIndex = request.ActionIndex,
            Phase = request.Skill.DefaultPhase,
            TargetSlot = request.TargetSlot,
            PlanningChoiceId = request.PlanningChoiceId
        };

        ActionPlanningSkillContext planningContext =
            new(
                request.Owner,
                request.OwnerPart,
                request.Skill,
                request.ActionIndex,
                actionManager.Slots,
                request.PlanningChoiceId);

        string choiceBlockReason =
            ActionPlanningMechanicPolicy.GetSkillSelectionBlockReason(planningContext);
        if (!string.IsNullOrWhiteSpace(choiceBlockReason))
            return Fail(result, choiceBlockReason);

        ActionPlanningMechanicPolicy.ConfigurePlannedSlot(
            planningContext,
            slot);

        if (!actionManager.TryAddOrReplaceSlot(slot))
        {
            return Fail(result, "에너지 예산 또는 슬롯 계약을 만족하지 못했습니다.");
        }

        if (result.PreviousSlot != null)
            ActionPlanningMechanicPolicy.RollbackPlannedSlot(request.Owner, result.PreviousSlot);

        if (!ActionPlanningMechanicPolicy.TryCommitPlannedSlot(
                request.Owner, slot, out string commitFailure))
        {
            actionManager.RemoveSlot(request.Owner, request.OwnerPart, request.ActionIndex);
            if (result.PreviousSlot != null)
            {
                actionManager.TryAddOrReplaceSlot(result.PreviousSlot);
                ActionPlanningMechanicPolicy.TryCommitPlannedSlot(request.Owner, result.PreviousSlot, out _);
            }
            return Fail(result, string.IsNullOrWhiteSpace(commitFailure) ? "Planning 즉시 효과 적용 실패" : commitFailure);
        }

        // C-04: 공격/도사림 비용은 계획 확정 시 실제 차감한다.
        // 이미 낸 비용은 이후 슬롯 상실/취소에도 환불하지 않는다.
        BattleAction planningAction = new BattleAction { Slot = slot };
        if (!slot.ResourceCostCommitted &&
            !slot.Skill.TryConsumeResource(request.Owner, planningAction))
        {
            ActionPlanningMechanicPolicy.RollbackPlannedSlot(request.Owner, slot);
            actionManager.RemoveSlot(request.Owner, request.OwnerPart, request.ActionIndex);
            return Fail(result, "계획 확정 시 자원 비용을 지불할 수 없습니다.");
        }
        slot.ResourceCostCommitted = true;
        slot.CommittedEnergyCost =
            System.Math.Max(
                0,
                slot.Skill.EnergyCost);

        // C-03: 도사림은 누르는 순간 사용시 효과까지 실행하고 Resolution에서 다시 실행하지 않는다.
        if (slot.Skill.ActionType == ActionType.Preparation)
        {
            request.Owner.BattleEvent?.RaiseActionStart(planningAction);
            slot.Skill.Execute(planningAction);
            request.Owner.BattleEvent?.RaiseActionEnd(planningAction);
            slot.PlanningEffectCommitted = true;
            slot.SkipResolution = true;
        }

        result.Success = true;
        result.Slot = slot;
        return result;
    }

    /// <summary>
    /// C-02/C-03: 위세는 BodyPart ActionSlot을 만들지 않는 슬롯리스 계획 명령이다.
    /// 호출 즉시 비용과 OnExecute 효과를 commit하며 같은 턴 일반 슬롯 수를 소비하지 않는다.
    /// </summary>
    public PrestigePlanningExecutionResult TryExecutePrestige(
        Character owner,
        Skill skill,
        Character target = null,
        BodyPart targetPart = null,
        string planningChoiceId = null)
    {
        PrestigePlanningExecutionResult result = new();
        if (owner == null || skill == null || skill.ActionType != ActionType.Prestige)
        {
            result.FailureReason = "슬롯리스 위세 계획 정보가 올바르지 않습니다.";
            return result;
        }

        CharacterCombatRulesRuntime rules = owner.CombatRulesRuntime;
        if (rules?.Loadout?.IsEquipped(skill.Definition) != true)
        {
            result.FailureReason = "현재 런에 선택된 위세가 아닙니다.";
            return result;
        }

        if (skill.PrestigeUsePolicy == PrestigeUsePolicy.OncePerTurn &&
            skill.UseCountThisTurn > 0)
        {
            result.FailureReason = "이번 턴에는 이미 위세를 사용했습니다.";
            return result;
        }

        if (!skill.CanUseByResource(owner))
        {
            result.FailureReason = "위세 발동 자원이 부족합니다.";
            return result;
        }

        ActionSlot transient = new ActionSlot
        {
            Owner = owner,
            Part = null,
            Skill = skill,
            ActionIndex = -1,
            Phase = ActionPhase.PRETURN,
            TargetCharacter = target ?? owner,
            TargetPart = targetPart,
            PlanningChoiceId = planningChoiceId,
            SkipResolution = true
        };
        BattleAction action = new BattleAction { Slot = transient };

        if (!skill.TryConsumeResource(owner, action))
        {
            result.FailureReason = "위세 비용 commit에 실패했습니다.";
            return result;
        }
        transient.ResourceCostCommitted = true;
        transient.CommittedEnergyCost =
            System.Math.Max(
                0,
                skill.EnergyCost);

        owner.BattleEvent?.RaiseActionStart(action);
        skill.Execute(action);
        owner.BattleEvent?.RaiseActionEnd(action);
        transient.PlanningEffectCommitted = true;

        result.Success = true;
        result.Action = action;
        return result;
    }

    public bool TryRetarget(
        ActionSlot sourceSlot,
        ActionSlot targetSlot,
        out ActionSlot liveSource)
    {
        BodyPart resolvedTargetPart =
            ResolveFallbackTargetPart(
                targetSlot,
                sourceSlot?.TargetPart);

        return TryRetarget(
            sourceSlot,
            targetSlot,
            resolvedTargetPart,
            out liveSource);
    }

    /// <summary>
    /// exact TargetSlot과 실제 피해 TargetPart를 별도로 재지정한다.
    /// Stage 1 Boss처럼 행동 슬롯 Part가 null인 경우에도 유효한 BodyPart를 지정할 수 있다.
    /// </summary>
    public bool TryRetarget(
        ActionSlot sourceSlot,
        ActionSlot targetSlot,
        BodyPart targetPart,
        out ActionSlot liveSource)
    {
        liveSource = null;

        if (actionManager == null ||
            sourceSlot == null ||
            targetSlot?.Owner == null)
        {
            return false;
        }

        if (!BattleTargetValidator.IsValid(
                targetSlot.Owner,
                targetPart,
                TargetSelectionRule.StandardAttack))
        {
            return false;
        }

        liveSource =
            actionManager.FindSlot(
                sourceSlot.Owner,
                sourceSlot.Part,
                sourceSlot.ActionIndex);

        if (liveSource == null ||
            liveSource.ActionId != sourceSlot.ActionId ||
            liveSource.PlanningEffectCommitted)
        {
            liveSource = null;
            return false;
        }

        liveSource.TargetCharacter = targetSlot.Owner;
        liveSource.TargetPart = targetPart;
        liveSource.TargetSlot = targetSlot;
        liveSource.SecondaryTargetPart = null;
        return true;
    }

    private static BodyPart ResolveFallbackTargetPart(
        ActionSlot targetSlot,
        BodyPart preferredPart)
    {
        if (targetSlot?.Owner == null)
            return null;

        TargetSelectionRule rule =
            TargetSelectionRule.StandardAttack;

        if (BattleTargetValidator.IsValid(
                targetSlot.Owner,
                preferredPart,
                rule))
        {
            return preferredPart;
        }

        if (BattleTargetValidator.IsValid(
                targetSlot.Owner,
                targetSlot.Part,
                rule))
        {
            return targetSlot.Part;
        }

        var points =
            BattleTargetValidator.GetTargetPoints(
                targetSlot.Owner,
                rule);

        if (points == null)
            return null;

        foreach (TargetPoint point in points)
        {
            if (point.IsValid &&
                point.Character == targetSlot.Owner)
            {
                return point.Part;
            }
        }

        return null;
    }

    /// <summary>
    /// UI/AutoPlan에서 사용자가 명시적으로 Planning 행동을 취소한다.
    /// 즉시 도사림 효과와 Planning commit을 rollback하고, 이 슬롯이 실제로 낸
    /// 에너지 비용만 환불한다. 전투 중 슬롯 소실은 이 API를 사용하지 않는다.
    /// </summary>
    public bool Cancel(
        Character owner,
        BodyPart part,
        int actionIndex,
        out string failureReason)
    {
        return cancellationService.Cancel(
            owner,
            part,
            actionIndex,
            out failureReason);
    }

    public int CancelOwner(
        Character owner,
        out string failureReason)
    {
        return cancellationService.CancelOwner(
            owner,
            out failureReason);
    }

    public bool Remove(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        if (actionManager == null)
            return false;

        ActionSlot slot = actionManager.FindSlot(owner, part, actionIndex);
        if (slot?.PlanningEffectCommitted == true)
            return false;

        if (slot != null)
            ActionPlanningMechanicPolicy.RollbackPlannedSlot(owner, slot);

        return actionManager.RemoveSlot(owner, part, actionIndex);
    }

    public void ResetOwner(Character owner)
    {
        if (owner == null || actionManager == null)
            return;

        cancellationService.CancelOwner(
            owner,
            out string failureReason);

        if (!string.IsNullOrWhiteSpace(
                failureReason))
        {
            UnityEngine.Debug.LogWarning(
                $"[Planning Reset] {failureReason}");
        }
    }

    public ActionPlanValidationResult ValidateSkillSelection(
        Character owner,
        BodyPart part,
        Skill skill,
        int actionIndex)
    {
        return validator.ValidateSkillSelection(
            owner,
            part,
            skill,
            actionIndex);
    }

    public bool HasPrestigeSlotSelected(
        Character owner,
        BodyPart ignorePart = null,
        int ignoreActionIndex = -1)
    {
        return validator.HasPrestigeSlotSelected(
            owner,
            ignorePart,
            ignoreActionIndex);
    }

    private static ActionPlanAssignmentResult Fail(
        ActionPlanAssignmentResult result,
        string reason)
    {
        result.Success = false;
        result.FailureReason = reason;
        return result;
    }
}