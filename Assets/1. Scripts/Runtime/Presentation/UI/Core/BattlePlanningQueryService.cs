using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Planning 상태를 읽어 UI 표시용 계산 결과를 제공한다.
/// 입력 처리/ActionSlot mutation/UI rendering은 수행하지 않는다.
/// </summary>
public sealed class BattlePlanningQueryService
{
    private readonly BattlePlanningSelectionState state;
    private readonly TargetSelectionViewModel selection;

    public BattlePlanningQueryService(
        BattlePlanningSelectionState state,
        TargetSelectionViewModel selection)
    {
        this.state = state;
        this.selection = selection;
    }

    public int GetWorldPlanningSlotSpeed(
        BattleManager battleManager,
        Character owner,
        BodyPart part)
    {
        if (owner == null ||
            battleManager?.SpeedManager == null)
        {
            return 0;
        }

        return battleManager.SpeedManager
            .GetSpeed(
                owner,
                part);
    }

    public bool IsWorldPlanningSlotSelected(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        return owner != null &&
               part != null &&
               state != null &&
               state.SelectedOwner == owner &&
               IsSamePart(
                   state.SelectedOwnerPart,
                   part) &&
               state.SelectedActionIndex == actionIndex &&
               state.InputMode !=
                   BattleInputMode.SelectOwner;
    }

    public bool IsWorldPlanningSlotPendingTarget(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        return
            IsWorldPlanningSlotSelected(
                owner,
                part,
                actionIndex) &&
            state.InputMode ==
                BattleInputMode.SelectTarget &&
            selection?.Skill != null &&
            selection.Skill.ActionType !=
                ActionType.Preparation;
    }

    public bool IsWorldTargetSlotAssigned(
        BattleManager battleManager,
        ActionSlot targetSlot)
    {
        if (targetSlot == null ||
            battleManager?.ActionManager?.Slots == null)
        {
            return false;
        }

        Character player =
            battleManager.BattleContext?.Player;

        if (player == null)
            return false;

        foreach (ActionSlot slot
                 in battleManager.ActionManager.Slots)
        {
            if (slot == null ||
                slot.Owner != player)
            {
                continue;
            }

            if (slot.TargetSlot == targetSlot ||
                slot.TargetSlot != null &&
                slot.TargetSlot.ActionId ==
                    targetSlot.ActionId &&
                slot.TargetSlot.Owner ==
                    targetSlot.Owner)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetPlayerEnergyDisplay(
        BattleManager battleManager,
        Character player,
        out int available,
        out int maximum,
        out int plannedCost,
        out int pendingCost,
        out bool hasPendingPreview)
    {
        available = 0;
        maximum = 0;
        plannedCost = 0;
        pendingCost = 0;
        hasPendingPreview = false;

        if (player == null)
            return false;

        maximum =
            Mathf.Max(0, player.MaxEnergy);

        int current =
            Mathf.Max(0, player.CurrentEnergy);

        bool planning =
            battleManager?.TurnManager != null &&
            !battleManager.TurnManager.IsResolving;

        ActionManager actionManager =
            battleManager?.ActionManager;

        if (!planning || actionManager == null)
        {
            available = current;
            return true;
        }

        plannedCost =
            actionManager.GetPlannedEnergyCost(player);

        Skill pendingSkill =
            selection?.Skill;

        if (state != null &&
            state.SelectedOwner == player &&
            state.SelectedOwnerPart != null &&
            pendingSkill != null &&
            state.InputMode !=
                BattleInputMode.SelectOwner)
        {
            int withoutEditedSlot =
                actionManager.GetPlannedEnergyCost(
                    player,
                    state.SelectedOwnerPart,
                    state.SelectedActionIndex);

            pendingCost =
                Mathf.Max(0, pendingSkill.EnergyCost);

            plannedCost =
                withoutEditedSlot +
                pendingCost;

            hasPendingPreview = true;
        }

        available =
            Mathf.Max(0, current - plannedCost);

        return true;
    }

    public IReadOnlyList<Skill>
        GetSelectableSkillsForCurrentSlot(
            BodyPart part)
    {
        Character owner =
            state?.SelectedOwner;

        if (owner == null)
            return System.Array.Empty<Skill>();

        return owner.GetSelectableSkills(
                   part,
                   state.SelectedActionIndex) ??
               System.Array.Empty<Skill>();
    }
    private static bool IsSamePart(
        BodyPart first,
        BodyPart second)
    {
        if (first == null ||
            second == null)
        {
            return
                first == null &&
                second == null;
        }

        return
            first == second ||
            first.Type == second.Type;
    }

}
