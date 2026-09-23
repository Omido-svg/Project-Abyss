using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 0922 감정 증강이 기존 전투 파이프라인에 필요한 최소 공통 hook 모음.
/// 감정 카드가 Character/Damage/Clash 코어를 직접 참조해서 분기하지 않도록
/// interface 기반으로 격리한다.
/// </summary>
public static class Canonical0922EmotionRuntimeHooks
{
    public static int ModifyHealing(
        Character owner,
        int amount)
    {
        int value = Mathf.Max(0, amount);
        if (owner?.Mechanics == null || value <= 0)
            return value;

        foreach (CombatMechanic mechanic in owner.Mechanics)
        {
            if (mechanic is not ICanonical0922HealingRule rule)
                continue;

            value = Mathf.Max(0, rule.ModifyHealing(owner, value));
            if (value <= 0)
                break;
        }

        return value;
    }

    public static void NotifyHealingResolved(
        Character owner,
        Canonical0922HealingResult result)
    {
        if (owner?.Mechanics == null || result == null)
            return;

        // snapshot: callback가 다른 runtime mechanic을 추가/제거해도 안전하게 순회한다.
        List<CombatMechanic> snapshot =
            new List<CombatMechanic>(owner.Mechanics);

        foreach (CombatMechanic mechanic in snapshot)
        {
            if (mechanic is ICanonical0922HealingRule rule)
                rule.OnHealingResolved(owner, result);
        }
    }

    public static int ModifyBlockGain(
        Character owner,
        int amount)
    {
        int value = Mathf.Max(0, amount);
        if (owner?.Mechanics == null || value <= 0)
            return value;

        foreach (CombatMechanic mechanic in owner.Mechanics)
        {
            if (mechanic is not ICanonical0922BlockGainRule rule)
                continue;

            value = Mathf.Max(0, rule.ModifyBlockGain(owner, value));
            if (value <= 0)
                break;
        }

        return value;
    }

    public static void NotifyBlockGainResolved(
        Character owner,
        int requestedAmount,
        int appliedAmount)
    {
        if (owner?.Mechanics == null)
            return;

        List<CombatMechanic> snapshot =
            new List<CombatMechanic>(owner.Mechanics);

        foreach (CombatMechanic mechanic in snapshot)
        {
            if (mechanic is ICanonical0922BlockGainRule rule)
            {
                rule.OnBlockGainResolved(
                    owner,
                    Mathf.Max(0, requestedAmount),
                    Mathf.Max(0, appliedAmount));
            }
        }
    }

    /// <summary>
    /// 최소 1 보장 이후, 방어도 계산 이전의 최종 HP 피해 hook.
    /// 신의 T2 완전 방어처럼 "방어도 소모 없이 0"이 되어야 하는 규칙이 사용한다.
    /// </summary>
    public static int ModifyDamageBeforeGuard(
        DamageContext context,
        int damage)
    {
        Character target = context?.Target;
        int value = Mathf.Max(0, damage);

        if (target?.Mechanics == null || value <= 0)
            return value;

        foreach (CombatMechanic mechanic in target.Mechanics)
        {
            if (mechanic is not ICanonical0922PreGuardDamageRule rule)
                continue;

            value = Mathf.Max(0, rule.ModifyDamageBeforeGuard(context, value));
            if (value <= 0)
                break;
        }

        return value;
    }

    /// <summary>
    /// 방어도 흡수 이후, 실제 HP/부위 HP에 적용되기 직전 hook.
    /// 초연 T2 나눠 받기의 "실제 받을 피해 절반을 다음 턴으로 미룸"에 사용한다.
    /// </summary>
    public static int ModifyDamageAfterGuard(
        DamageContext context,
        int damage)
    {
        Character target = context?.Target;
        int value = Mathf.Max(0, damage);

        if (target?.Mechanics == null || value <= 0)
            return value;

        foreach (CombatMechanic mechanic in target.Mechanics)
        {
            if (mechanic is not ICanonical0922PostGuardDamageRule rule)
                continue;

            value = Mathf.Max(0, rule.ModifyDamageAfterGuard(context, value));
        }

        return value;
    }

    public static bool TryResolveClashSpeedModifierOverride(
        BattleAction action,
        out int speedModifier)
    {
        speedModifier = 0;
        if (action?.Owner?.Mechanics == null)
            return false;

        bool found = false;
        int best = 0;

        foreach (CombatMechanic mechanic in action.Owner.Mechanics)
        {
            if (mechanic is not ICanonical0922ClashSpeedOverride rule ||
                !rule.TryGetClashSpeedModifier(action, out int candidate))
            {
                continue;
            }

            found = true;
            best = Mathf.Max(best, candidate);
        }

        speedModifier = Mathf.Max(0, best);
        return found;
    }

    public static bool SuppressSingleWeakenedPenalty(Character owner)
    {
        if (owner?.Mechanics == null)
            return false;

        foreach (CombatMechanic mechanic in owner.Mechanics)
        {
            if (mechanic is ICanonical0922WeakenedPenaltyRule rule &&
                rule.SuppressSingleWeakenedPenalty(owner))
            {
                return true;
            }
        }

        return false;
    }

    public static int ResolveMomentumAdvantageThreshold(
        Character owner,
        int defaultThreshold)
    {
        int value = defaultThreshold;
        if (owner?.Mechanics == null)
            return value;

        foreach (CombatMechanic mechanic in owner.Mechanics)
        {
            if (mechanic is ICanonical0922MomentumBandRule rule)
            {
                value = Mathf.Max(
                    value,
                    rule.ResolveAdvantageThreshold(owner, value));
            }
        }

        return value;
    }

    public static bool TryGetPrestigeStockpileThreshold(
        Character owner,
        out int threshold)
    {
        threshold = 0;
        if (owner?.Mechanics == null)
            return false;

        foreach (CombatMechanic mechanic in owner.Mechanics)
        {
            if (mechanic is not ICanonical0922PrestigeStockpileRule rule)
                continue;

            int candidate = rule.GetPrestigeActivationThreshold(owner);
            if (candidate <= 0)
                continue;

            threshold = candidate;
            return true;
        }

        return false;
    }

    public static int ResolveCanonicalPrestigeThreshold(Character owner)
    {
        if (owner == null)
            return 0;

        IReadOnlyList<Skill> prestige = owner.GetSelectablePrestigeSkills();
        if (prestige != null)
        {
            foreach (Skill skill in prestige)
            {
                SkillDefinition definition = skill?.Definition;
                if (definition == null)
                    continue;

                if (definition.OverrideResourceRules &&
                    definition.PrestigeCost > 0)
                {
                    return Mathf.Max(
                        1,
                        SkillUpgradeService.ResolvePrestigeCost(
                            definition,
                            owner.BattleContext?.SkillUpgrades,
                            definition.PrestigeCost));
                }
            }
        }

        int dataMaximum = owner.Data?.maxPrestige ?? 0;
        if (dataMaximum > 0)
            return dataMaximum;

        return Mathf.Max(0, owner.CurrentStatus?.maxPrestige ?? 0);
    }
}

public sealed class Canonical0922HealingResult
{
    public int RequestedAmount;
    public int ModifiedAmount;
    public int AppliedWholeHp;
    public int OverhealAmount;
}

public interface ICanonical0922HealingRule
{
    int ModifyHealing(Character owner, int amount);
    void OnHealingResolved(Character owner, Canonical0922HealingResult result);
}

public interface ICanonical0922BlockGainRule
{
    int ModifyBlockGain(Character owner, int amount);
    void OnBlockGainResolved(Character owner, int requestedAmount, int appliedAmount);
}

public interface ICanonical0922PreGuardDamageRule
{
    int ModifyDamageBeforeGuard(DamageContext context, int damage);
}

public interface ICanonical0922PostGuardDamageRule
{
    int ModifyDamageAfterGuard(DamageContext context, int damage);
}

public interface ICanonical0922ClashSpeedOverride
{
    bool TryGetClashSpeedModifier(BattleAction action, out int modifier);
}

public interface ICanonical0922WeakenedPenaltyRule
{
    bool SuppressSingleWeakenedPenalty(Character owner);
}

public interface ICanonical0922MomentumBandRule
{
    int ResolveAdvantageThreshold(Character owner, int currentThreshold);
}

public interface ICanonical0922PrestigeStockpileRule
{
    int GetPrestigeActivationThreshold(Character owner);
}
