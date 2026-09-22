using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 0922 공용 상태 계산 축.
/// 저장 단계에서는 Entry를 보존하고, 실제 효과를 계산할 때만 같은 축을 합산한다.
/// </summary>
public static class CommonStatusAlgebra
{
    public static bool AreOpposites(StatusEffect first, StatusEffect second)
    {
        if (first == null || second == null)
            return false;

        Type a = first.GetType();
        Type b = second.GetType();

        return IsPair<StrengthStatus, WeaknessStatus>(a, b) ||
               IsPair<ProtectionStatus, RuptureStatus>(a, b) ||
               IsPair<SturdyStatus, DisarmStatus>(a, b) ||
               IsPair<HeatStatus, StagnationStatus>(a, b);
    }

    public static int GetNumericTotal<TStatus>(
        IReadOnlyList<StatusEffect> statuses)
        where TStatus : NumericTimedStatus
    {
        if (statuses == null)
            return 0;

        int total = 0;

        for (int i = 0; i < statuses.Count; i++)
        {
            if (statuses[i] is not TStatus status ||
                status.IsExpired)
            {
                continue;
            }

            total += Mathf.Max(0, status.NumericValue);
        }

        return Mathf.Max(0, total);
    }

    public static int GetHpDamageFlatModifier(
        Character target,
        BodyPart targetPart)
    {
        IReadOnlyList<StatusEffect> characterStatuses =
            target?.StatusEffects;

        IReadOnlyList<StatusEffect> partStatuses =
            targetPart != null &&
            targetPart.Owner == target
                ? targetPart.StatusEffects
                : null;

        return GetHpDamageFlatModifier(
            characterStatuses,
            partStatuses);
    }

    public static int GetHpDamageFlatModifier(
        IReadOnlyList<StatusEffect> characterStatuses,
        IReadOnlyList<StatusEffect> partStatuses)
    {
        int rupture =
            GetNumericTotal<RuptureStatus>(characterStatuses) +
            GetNumericTotal<RuptureStatus>(partStatuses);

        int protection =
            GetNumericTotal<ProtectionStatus>(characterStatuses) +
            GetNumericTotal<ProtectionStatus>(partStatuses);

        return rupture - protection;
    }

    public static int GetStaggerDamageFlatModifier(
        Character target,
        BodyPart targetPart)
    {
        IReadOnlyList<StatusEffect> characterStatuses =
            target?.StatusEffects;

        IReadOnlyList<StatusEffect> partStatuses =
            targetPart != null &&
            targetPart.Owner == target
                ? targetPart.StatusEffects
                : null;

        return GetStaggerDamageFlatModifier(
            characterStatuses,
            partStatuses);
    }

    public static int GetStaggerDamageFlatModifier(
        IReadOnlyList<StatusEffect> characterStatuses,
        IReadOnlyList<StatusEffect> partStatuses)
    {
        int disarm =
            GetNumericTotal<DisarmStatus>(characterStatuses) +
            GetNumericTotal<DisarmStatus>(partStatuses);

        int sturdy =
            GetNumericTotal<SturdyStatus>(characterStatuses) +
            GetNumericTotal<SturdyStatus>(partStatuses);

        return disarm - sturdy;
    }

    public static int GetFearRollPenalty(
        IReadOnlyList<StatusEffect> statuses)
    {
        if (statuses == null)
            return 0;

        for (int i = 0; i < statuses.Count; i++)
        {
            if (statuses[i] is OlafFearStatus fear &&
                !fear.IsExpired)
            {
                return 1;
            }
        }

        return 0;
    }

    public static int GetTurnEndPrestigeDelta(
        IReadOnlyList<StatusEffect> statuses)
    {
        int heat =
            GetNumericTotal<HeatStatus>(statuses);

        int stagnation =
            GetNumericTotal<StagnationStatus>(statuses);

        return heat - stagnation;
    }

    public static int GetRegenerationTotal(
        IReadOnlyList<StatusEffect> statuses)
    {
        return GetNumericTotal<RegenerationStatus>(statuses);
    }

    /// <summary>
    /// 회복 modifier 공통 경로.
    /// Presence인 Pain은 어떤 이유로 중복 instance가 존재해도 한 번만 적용한다.
    /// </summary>
    public static int ApplyHealingModifiers(
        IReadOnlyList<StatusEffect> statuses,
        int amount)
    {
        if (amount <= 0)
            return 0;

        if (statuses == null)
            return amount;

        int modified = amount;
        bool painApplied = false;

        for (int i = 0; i < statuses.Count; i++)
        {
            StatusEffect effect = statuses[i];

            if (effect == null || effect.IsExpired)
                continue;

            if (effect is PainStatus)
            {
                if (painApplied)
                    continue;

                painApplied = true;
            }

            modified =
                effect.ModifyHealing(modified);

            if (modified <= 0)
                return 0;
        }

        return Mathf.Max(0, modified);
    }

    private static bool IsPair<TA, TB>(Type a, Type b)
    {
        return (a == typeof(TA) && b == typeof(TB)) ||
               (a == typeof(TB) && b == typeof(TA));
    }
}
