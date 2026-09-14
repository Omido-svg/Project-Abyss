using System.Collections.Generic;

/// <summary>
/// BattleUIManager가 Olaf/Yujin/Hifumi 같은 concrete 캐릭터를 알지 않도록
/// Character.Mechanics의 IActionPlanningRule만 조회한다.
/// </summary>
public static class ActionPlanningMechanicPolicy
{
    public static string GetSkillSelectionBlockReason(
        ActionPlanningSkillContext context)
    {
        IReadOnlyList<CombatMechanic> mechanics =
            context.Owner?.Mechanics;

        if (mechanics == null)
            return string.Empty;

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is not IActionPlanningRule rule)
                continue;

            string reason =
                rule.GetSkillSelectionBlockReason(context);

            if (!string.IsNullOrWhiteSpace(reason))
                return reason;
        }

        return string.Empty;
    }

    public static bool IsEnergyReservationExempt(
        ActionPlanningSkillContext context)
    {
        IReadOnlyList<CombatMechanic> mechanics =
            context.Owner?.Mechanics;

        if (mechanics == null)
            return false;

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is IActionPlanningRule rule &&
                rule.IsEnergyReservationExempt(context))
            {
                return true;
            }
        }

        return false;
    }

    public static void ConfigurePlannedSlot(
        ActionPlanningSkillContext context,
        ActionSlot slot)
    {
        IReadOnlyList<CombatMechanic> mechanics =
            context.Owner?.Mechanics;

        if (mechanics == null || slot == null)
            return;

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is IActionPlanningRule rule)
                rule.ConfigurePlannedSlot(context, slot);
        }
    }

    public static IReadOnlyList<ActionPlanningChoiceOption> GetPlanningChoices(
        ActionPlanningSkillContext context)
    {
        IReadOnlyList<CombatMechanic> mechanics = context.Owner?.Mechanics;
        if (mechanics == null)
            return System.Array.Empty<ActionPlanningChoiceOption>();

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is IActionPlanningChoiceRule rule)
            {
                IReadOnlyList<ActionPlanningChoiceOption> choices = rule.GetPlanningChoices(context);
                if (choices != null && choices.Count > 0)
                    return choices;
            }
        }
        return System.Array.Empty<ActionPlanningChoiceOption>();
    }

    public static bool TryCommitPlannedSlot(Character owner, ActionSlot slot, out string failureReason)
    {
        failureReason = string.Empty;
        IReadOnlyList<CombatMechanic> mechanics = owner?.Mechanics;
        if (mechanics == null || slot == null)
            return true;

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is IActionPlanningCommitRule rule &&
                !rule.TryCommitPlannedSlot(slot, out failureReason))
            {
                return false;
            }
        }
        return true;
    }

    public static void RollbackPlannedSlot(Character owner, ActionSlot slot)
    {
        IReadOnlyList<CombatMechanic> mechanics = owner?.Mechanics;
        if (mechanics == null || slot == null)
            return;

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is IActionPlanningCommitRule rule)
                rule.RollbackPlannedSlot(slot);
        }
    }

    public static void RestorePlanningState(
        Character owner,
        ActionSlot slot)
    {
        IReadOnlyList<CombatMechanic> mechanics =
            owner?.Mechanics;

        if (mechanics == null || slot == null)
            return;

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is IActionPlanningRule rule)
                rule.RestorePlanningState(slot);
        }
    }
}