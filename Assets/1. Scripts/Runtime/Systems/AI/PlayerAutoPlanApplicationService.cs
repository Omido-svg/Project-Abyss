using System.Collections.Generic;

/// <summary>
/// 자동계획 계산과 실제 ActionManager mutation을 분리한다.
/// 계획 전체의 검증/commit/rollback은 ActionManager의 aggregate API에 위임한다.
/// </summary>
public sealed class PlayerAutoPlanApplicationService
{
    public int Apply(
        ActionManager actionManager,
        Character owner,
        IReadOnlyList<ActionSlot> plannedSlots)
    {
        if (actionManager == null ||
            owner == null ||
            plannedSlots == null)
        {
            return 0;
        }

        ActionPlanValidator validator =
            new ActionPlanValidator(
                actionManager);

        ActionPlanValidationResult validation =
            validator.ValidateReplacementPlan(
                owner,
                plannedSlots);

        if (!validation.Success)
            return 0;

        return actionManager.TryReplaceOwnerPlan(
            owner,
            plannedSlots);
    }
}