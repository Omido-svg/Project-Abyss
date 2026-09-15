using System.Collections.Generic;

/// <summary>
/// 자동계획 계산과 실제 ActionManager mutation을 분리한다.
/// 0915 Planning 계약에 맞춰 계획 전체를 등록한 뒤 비용/즉시효과를 commit한다.
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

        // 자동계획 UI는 적용 전에 기존 player plan을 명시적 Cancel한다.
        // 여기까지 owner slot이 남아 있다면 raw replacement로 비용/즉시효과를 유실하지 않도록 fail-closed.
        if (actionManager.CountSlots(owner) > 0)
            return 0;

        PlanningActionCancellationService cancellation =
            new PlanningActionCancellationService(
                actionManager);

        // 이미 즉시 실행된 도사림이 있으면 자동계획 전체 교체로 그 행동 소비를 지울 수 없다.
        foreach (ActionSlot existing in actionManager.Slots)
        {
            if (existing?.Owner == owner && existing.PlanningEffectCommitted)
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

        int applied =
            actionManager.TryReplaceOwnerPlan(
                owner,
                plannedSlots);

        if (applied <= 0)
            return 0;

        // TryReplaceOwnerPlan 이후 실제 live slot을 대상으로 planning hook과 비용을 commit한다.
        // 정본 C-04에 따라 이미 지불한 비용은 이후 계획 소실 시 환불하지 않는다.
        List<ActionSlot> committed = new();

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot == null || slot.Owner != owner || slot.Skill == null)
                continue;

            if (!ActionPlanningMechanicPolicy.TryCommitPlannedSlot(
                    owner,
                    slot,
                    out _))
            {
                // 자동 계획은 원자적으로 계산되지만 비용/캐릭터 고유 hook이 런타임 상태 변화로
                // 실패할 수 있다. 이미 commit된 비용/효과는 정본상 환불하지 않고 계획만 제거한다.
                cancellation.CancelOwner(
                    owner,
                    out _);
                return 0;
            }

            BattleAction action = new BattleAction { Slot = slot };
            if (!slot.ResourceCostCommitted &&
                !slot.Skill.TryConsumeResource(owner, action))
            {
                cancellation.CancelOwner(
                    owner,
                    out _);
                return 0;
            }

            slot.ResourceCostCommitted = true;
            slot.CommittedEnergyCost =
                System.Math.Max(
                    0,
                    slot.Skill.EnergyCost);
            committed.Add(slot);
        }

        // 도사림은 계획 단계에서 즉시 실행하고 Resolution queue에서 제외한다.
        foreach (ActionSlot slot in committed)
        {
            if (slot.Skill.ActionType != ActionType.Preparation)
                continue;

            BattleAction action = new BattleAction { Slot = slot };
            owner.BattleEvent?.RaiseActionStart(action);
            slot.Skill.Execute(action);
            owner.BattleEvent?.RaiseActionEnd(action);
            slot.PlanningEffectCommitted = true;
            slot.SkipResolution = true;

            UnityEngine.Debug.Log(
                $"[PLAYER PLAN COMMIT] Owner={owner.Data?.CharacterName ?? owner.name}, " +
                $"Skill={slot.Skill.SkillName}, CostCommitted={slot.ResourceCostCommitted}, " +
                $"Energy={owner.CurrentEnergy}/{owner.MaxEnergy}, ImmediatePreparation=True");
        }

        return applied;
    }
}