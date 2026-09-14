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

/// <summary>
/// Planning ActionSlot의 생성/교체/재지정/삭제를 소유하는 command service.
/// UI는 입력 상태만 결정하고 ActionManager mutation 세부사항을 직접 수행하지 않는다.
/// </summary>
public sealed class BattleActionPlanCommandService
{
    private readonly ActionManager actionManager;
    private readonly SpeedManager speedManager;
    private readonly ActionPlanValidator validator;

    public BattleActionPlanCommandService(
        ActionManager actionManager,
        SpeedManager speedManager)
    {
        this.actionManager = actionManager;
        this.speedManager = speedManager;
        validator = new ActionPlanValidator(actionManager);
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

        result.Success = true;
        result.Slot = slot;
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
            liveSource.ActionId != sourceSlot.ActionId)
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

    public bool Remove(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        if (actionManager == null)
            return false;

        ActionSlot slot = actionManager.FindSlot(owner, part, actionIndex);
        if (slot != null)
            ActionPlanningMechanicPolicy.RollbackPlannedSlot(owner, slot);

        return actionManager.RemoveSlot(owner, part, actionIndex);
    }

    public void ResetOwner(Character owner)
    {
        if (owner == null || actionManager == null)
            return;

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot?.Owner == owner)
                ActionPlanningMechanicPolicy.RollbackPlannedSlot(owner, slot);
        }
        actionManager.RemoveSlotsByOwner(owner);
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