using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사용자가 Planning 단계에서 명시적으로 행동을 취소/재계획하는 경로.
/// C-04의 "전투 중 슬롯 소실 시 비용 환불 없음"과 의도적으로 분리한다.
///
/// ActionManager.RemoveSlot / BattleActionPlanCommandService.Remove:
///     runtime/no-refund 제거.
/// Cancel / CancelOwner:
///     Planning undo + commit 비용 환불.
/// </summary>
public sealed class PlanningActionCancellationService
{
    private readonly ActionManager actionManager;

    public PlanningActionCancellationService(
        ActionManager actionManager)
    {
        this.actionManager = actionManager;
    }

    public bool Cancel(
        Character owner,
        BodyPart part,
        int actionIndex,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (actionManager == null)
        {
            failureReason =
                "ActionManager가 준비되지 않았습니다.";
            return false;
        }

        ActionSlot slot =
            actionManager.FindSlot(
                owner,
                part,
                actionIndex);

        return Cancel(slot, out failureReason);
    }

    public bool Cancel(
        ActionSlot slot,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (actionManager == null ||
            slot == null ||
            slot.Owner == null)
        {
            failureReason =
                "취소할 Planning ActionSlot이 없습니다.";
            return false;
        }

        ActionSlot live =
            slot.ActionId > 0
                ? actionManager.FindSlotById(slot.ActionId)
                : actionManager.FindSlot(
                    slot.Owner,
                    slot.Part,
                    slot.ActionIndex);

        if (live == null)
        {
            failureReason =
                "취소할 ActionSlot이 이미 사라졌습니다.";
            return false;
        }

        Character owner = live.Owner;

        // 먼저 live plan에서 제거한다.
        // 이후 undo 중 UI/validator가 같은 슬롯을 다시 선택된 것으로 보지 않는다.
        if (!actionManager.RemoveSlot(live))
        {
            failureReason =
                "ActionSlot 제거에 실패했습니다.";
            return false;
        }

        bool wasImmediatePreparation =
            live.PlanningEffectCommitted &&
            live.Skill?.ActionType ==
                ActionType.Preparation;

        if (wasImmediatePreparation)
        {
            if (live.PlanningUndo != null &&
                !live.PlanningUndo.RollbackAll(
                    out string undoFailure))
            {
                // 슬롯은 이미 제거되었지만 부분 undo 상태를 숨기지 않는다.
                failureReason =
                    string.IsNullOrWhiteSpace(undoFailure)
                        ? "도사림 Planning 효과 rollback 실패"
                        : undoFailure;
                return false;
            }

            // ActionStart에서 증가한 SkillUsageLedger를 Planning 취소에 한해 되돌린다.
            live.Skill?.RollbackPlanningUse();
        }

        // 환형처럼 IActionPlanningCommitRule에서 계획 단계 상태를 commit한 메커닉.
        ActionPlanningMechanicPolicy
            .RollbackPlannedSlot(
                owner,
                live);

        if (live.ResourceCostCommitted)
        {
            int refund =
                Mathf.Max(
                    0,
                    live.CommittedEnergyCost > 0
                        ? live.CommittedEnergyCost
                        : live.Skill?.EnergyCost ?? 0);

            if (refund > 0)
            {
                owner.AddEnergy(
                    refund,
                    CombatResourceChangeReason.Restore);
            }

            live.ResourceCostCommitted = false;
            live.CommittedEnergyCost = 0;
        }

        live.PlanningEffectCommitted = false;
        live.SkipResolution = false;
        live.PlanningUndo?.Clear();

        return true;
    }

    public int CancelOwner(
        Character owner,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (actionManager == null || owner == null)
            return 0;

        List<ActionSlot> targets = new();

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot?.Owner == owner)
                targets.Add(slot);
        }

        // 자동계획은 낮은 ActionId부터 commit된다.
        // 역순으로 취소해 즉시 효과의 상태 전이를 stack처럼 안전하게 되감는다.
        targets.Sort(
            (left, right) =>
                right.ActionId.CompareTo(
                    left.ActionId));

        int cancelled = 0;

        foreach (ActionSlot slot in targets)
        {
            if (!Cancel(slot, out string reason))
            {
                failureReason = reason;
                break;
            }

            cancelled++;
        }

        return cancelled;
    }
}
