using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillUpgradeEffectPayload
{
    [Tooltip("SkillEffectEntry.UpgradeKey와 매칭되는 안정 키. 비어 있으면 적용되지 않습니다.")]
    public string TargetEffectKey;

    [Header("Status")]
    public bool OverrideStack;
    [Min(1)] public int Stack = 1;
    public bool OverrideDuration;
    [Min(1)] public int Duration = 1;

    [Header("Numeric")]
    public bool OverrideAmount;
    public int Amount;
    public bool OverrideMinimum;
    public int Minimum;
    public bool OverrideMaximum;
    public int Maximum;
    public bool OverrideFlatValue;
    public int FlatValue;
    public bool OverrideMultiplier;
    public float Multiplier = 1f;

    [Header("Optional Contract")]
    public bool OverrideResourceKey;
    public string ResourceKey = string.Empty;
    public bool OverrideForceCharacterStatus;
    public bool ForceCharacterStatus;
    public bool OverrideGiveToSelectedTarget;
    public bool GiveToSelectedTarget;
}

[Serializable]
public sealed class SkillUpgradeStep
{
    [TextArea(1, 4)] public string DesignNote;

    [Header("Cost — 0 means Unset")]
    [Min(0)] public int CostOverride;

    [Header("Power")]
    public int BasePowerDelta;
    public int AllRollMinPowerDelta;
    public int AllRollMaxPowerDelta;
    public List<int> PerRollPowerDelta = new();

    [Header("Resource Cost")]
    public int EnergyCostDelta;
    public int PrestigeCostDelta;
    public int CustomResourceCostDelta;

    [Header("Effect Payload")]
    [Tooltip(
        "C-37: 상태량/자원량/잔효과/도사림 핵심값/위세 규모를 " +
        "SkillEffectEntry.UpgradeKey 단위로 정확히 덮어씁니다.")]
    public List<SkillUpgradeEffectPayload> EffectPayloads = new();
}

[CreateAssetMenu(menuName = "Battle/Progression/Skill Upgrade Profile", fileName = "NewSkillUpgradeProfile")]
public sealed class SkillUpgradeProfile : ScriptableObject
{
    [Min(0)] public int BaseCostOverride;
    public SkillUpgradeStep Upgrade1 = new();
    public SkillUpgradeStep Upgrade2 = new();

    public int GetBaseCost(ActionType type) => BaseCostOverride > 0
        ? BaseCostOverride
        : type switch
        {
            ActionType.NormalAttack => 50,
            ActionType.Preparation => 100,
            ActionType.Duel => 125,
            ActionType.Prestige => 150,
            _ => 0
        };

    public SkillUpgradeStep GetStep(int targetLevel) => targetLevel switch
    {
        1 => Upgrade1,
        2 => Upgrade2,
        _ => null
    };
}

public sealed class SkillUpgradeState
{
    private readonly Dictionary<string, int> levels =
        new(StringComparer.OrdinalIgnoreCase);

    public int GetLevel(SkillDefinition definition)
    {
        if (definition == null)
            return 0;

        string key = definition.SkillId;
        return !string.IsNullOrWhiteSpace(key) &&
               levels.TryGetValue(key, out int level)
            ? level
            : 0;
    }

    public bool TryUpgrade(SkillDefinition definition, int maximumLevel = 2)
    {
        if (definition == null)
            return false;

        int current = GetLevel(definition);
        if (current >= Mathf.Max(0, maximumLevel))
            return false;

        string key = definition.SkillId;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        levels[key] = current + 1;
        return true;
    }

    public bool TrySetLevel(SkillDefinition definition, int level, int maximumLevel = 2)
    {
        if (definition == null)
            return false;

        string key = definition.SkillId;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        levels[key] = Mathf.Clamp(level, 0, Mathf.Max(0, maximumLevel));
        return true;
    }

    public void Clear() => levels.Clear();
}

public static class SkillUpgradeService
{
    public const int DefaultMaximumLevel = 2;

    public static int GetLevel(
        SkillDefinition definition,
        SkillUpgradeState state) =>
        state?.GetLevel(definition) ?? 0;

    /// <summary>
    /// C-36:
    /// - Level 1: 50/100/125/150 (or BaseCostOverride).
    /// - Level 2: 정본이 (미정)이므로 Upgrade2.CostOverride가 명시된 경우에만 quote 가능.
    /// </summary>
    public static bool TryGetUpgradeCost(
        SkillDefinition definition,
        int targetLevel,
        out int cost)
    {
        cost = 0;
        if (definition == null || targetLevel < 1 || targetLevel > 2)
            return false;

        SkillUpgradeProfile profile = definition.UpgradeProfile;

        if (targetLevel == 1)
        {
            cost = profile?.Upgrade1?.CostOverride > 0
                ? profile.Upgrade1.CostOverride
                : profile?.GetBaseCost(definition.ActionType) ??
                  GetCanonicalFirstUpgradeCost(definition.ActionType);
            return cost > 0;
        }

        int explicitSecondCost =
            profile?.Upgrade2?.CostOverride ?? 0;

        if (explicitSecondCost <= 0)
            return false;

        cost = explicitSecondCost;
        return true;
    }

    // Legacy-friendly API. Unset은 0으로 반환하되 자동 배율 계산은 절대 하지 않는다.
    public static int GetUpgradeCost(
        SkillDefinition definition,
        int targetLevel) =>
        TryGetUpgradeCost(definition, targetLevel, out int cost)
            ? cost
            : 0;

    public static int GetCanonicalFirstUpgradeCost(ActionType type) => type switch
    {
        ActionType.NormalAttack => 50,
        ActionType.Preparation => 100,
        ActionType.Duel => 125,
        ActionType.Prestige => 150,
        _ => 0
    };

    public static bool TryPurchaseUpgrade(
        RunProgressionState runState,
        SkillDefinition definition,
        int maximumLevel = DefaultMaximumLevel)
    {
        if (runState == null || definition == null)
            return false;

        int current = runState.SkillUpgrades.GetLevel(definition);
        int target = current + 1;

        if (target > Mathf.Max(0, maximumLevel) ||
            !TryGetUpgradeCost(definition, target, out int cost) ||
            !runState.TrySpendGold(cost))
        {
            return false;
        }

        if (runState.SkillUpgrades.TryUpgrade(definition, maximumLevel))
            return true;

        runState.AddGold(cost);
        return false;
    }

    public static int GetCumulativeBasePowerDelta(
        SkillDefinition definition,
        SkillUpgradeState state) =>
        SumInt(definition, state, step => step.BasePowerDelta);

    public static int GetCumulativeEnergyCostDelta(
        SkillDefinition definition,
        SkillUpgradeState state) =>
        SumInt(definition, state, step => step.EnergyCostDelta);

    public static int GetCumulativePrestigeCostDelta(
        SkillDefinition definition,
        SkillUpgradeState state) =>
        SumInt(definition, state, step => step.PrestigeCostDelta);

    public static int GetCumulativeCustomResourceCostDelta(
        SkillDefinition definition,
        SkillUpgradeState state) =>
        SumInt(definition, state, step => step.CustomResourceCostDelta);

    public static int ResolveEnergyCost(
        SkillDefinition definition,
        SkillUpgradeState state,
        int baseCost) =>
        Mathf.Max(0, baseCost + GetCumulativeEnergyCostDelta(definition, state));

    public static int ResolvePrestigeCost(
        SkillDefinition definition,
        SkillUpgradeState state,
        int baseCost) =>
        Mathf.Max(0, baseCost + GetCumulativePrestigeCostDelta(definition, state));

    public static int ResolveCustomResourceCost(
        SkillDefinition definition,
        SkillUpgradeState state,
        int baseCost) =>
        Mathf.Max(0, baseCost + GetCumulativeCustomResourceCostDelta(definition, state));

    public static SkillRollData ResolveRollData(
        SkillDefinition definition,
        SkillUpgradeState state,
        SkillRollData source,
        int rollIndex)
    {
        if (definition == null || source == null)
            return source;

        int level = GetLevel(definition, state);
        if (level <= 0)
            return source;

        int minDelta = 0;
        int maxDelta = 0;
        int perRollDelta = 0;

        ForEachAppliedStep(definition, level, step =>
        {
            minDelta += step.AllRollMinPowerDelta;
            maxDelta += step.AllRollMaxPowerDelta;

            if (step.PerRollPowerDelta != null &&
                rollIndex >= 0 &&
                rollIndex < step.PerRollPowerDelta.Count)
            {
                perRollDelta += step.PerRollPowerDelta[rollIndex];
            }
        });

        if (minDelta == 0 && maxDelta == 0 && perRollDelta == 0)
            return source;

        SkillRollData result = CloneRollData(source);

        result.MinPower = Mathf.Max(0, result.MinPower + minDelta + perRollDelta);
        result.MaxPower = Mathf.Max(result.MinPower, result.MaxPower + maxDelta + perRollDelta);

        // 비 Dice RNG는 각 결과가 곧 최종 위력이므로 per-roll/all-roll delta를 결과에도 반영한다.
        int exactDelta = perRollDelta + Mathf.FloorToInt((minDelta + maxDelta) * 0.5f);
        if (exactDelta != 0)
        {
            result.CoinBackPower = Mathf.Max(0, result.CoinBackPower + exactDelta);
            result.CoinFrontPower = Mathf.Max(0, result.CoinFrontPower + exactDelta);
            result.ChinchiroHifumiPower += exactDelta;
            result.ChinchiroBlankPower += exactDelta;
            result.ChinchiroMokuPower += exactDelta;
            result.ChinchiroShigoroPower += exactDelta;
            result.ChinchiroArashiPower += exactDelta;
        }

        return result;
    }

    public static SkillEffectOverrides ResolveEffectOverrides(
        SkillDefinition definition,
        SkillUpgradeState state,
        string upgradeKey,
        SkillEffectOverrides source)
    {
        SkillEffectOverrides result = CloneOverrides(source);

        if (definition == null ||
            string.IsNullOrWhiteSpace(upgradeKey))
        {
            return result;
        }

        int level = GetLevel(definition, state);
        if (level <= 0)
            return result;

        ForEachAppliedStep(definition, level, step =>
        {
            if (step.EffectPayloads == null)
                return;

            foreach (SkillUpgradeEffectPayload payload in step.EffectPayloads)
            {
                if (payload == null ||
                    !string.Equals(
                        payload.TargetEffectKey,
                        upgradeKey,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ApplyPayload(result, payload);
            }
        });

        return result;
    }

    private static int SumInt(
        SkillDefinition definition,
        SkillUpgradeState state,
        Func<SkillUpgradeStep, int> selector)
    {
        if (definition == null || selector == null)
            return 0;

        int total = 0;
        int level = GetLevel(definition, state);
        ForEachAppliedStep(definition, level, step => total += selector(step));
        return total;
    }

    private static void ForEachAppliedStep(
        SkillDefinition definition,
        int level,
        Action<SkillUpgradeStep> visitor)
    {
        if (definition?.UpgradeProfile == null || visitor == null || level <= 0)
            return;

        if (level >= 1 && definition.UpgradeProfile.Upgrade1 != null)
            visitor(definition.UpgradeProfile.Upgrade1);

        if (level >= 2 && definition.UpgradeProfile.Upgrade2 != null)
            visitor(definition.UpgradeProfile.Upgrade2);
    }

    private static void ApplyPayload(
        SkillEffectOverrides target,
        SkillUpgradeEffectPayload payload)
    {
        if (target == null || payload == null)
            return;

        if (payload.OverrideStack)
        {
            target.OverrideStack = true;
            target.Stack = Mathf.Max(1, payload.Stack);
        }
        if (payload.OverrideDuration)
        {
            target.OverrideDuration = true;
            target.Duration = Mathf.Max(1, payload.Duration);
        }
        if (payload.OverrideAmount)
        {
            target.OverrideAmount = true;
            target.Amount = payload.Amount;
        }
        if (payload.OverrideMinimum)
        {
            target.OverrideMinimum = true;
            target.Minimum = payload.Minimum;
        }
        if (payload.OverrideMaximum)
        {
            target.OverrideMaximum = true;
            target.Maximum = payload.Maximum;
        }
        if (payload.OverrideFlatValue)
        {
            target.OverrideFlatValue = true;
            target.FlatValue = payload.FlatValue;
        }
        if (payload.OverrideMultiplier)
        {
            target.OverrideMultiplier = true;
            target.Multiplier = payload.Multiplier;
        }
        if (payload.OverrideResourceKey)
        {
            target.OverrideResourceKey = true;
            target.ResourceKey = payload.ResourceKey ?? string.Empty;
        }
        if (payload.OverrideForceCharacterStatus)
        {
            target.OverrideForceCharacterStatus = true;
            target.ForceCharacterStatus = payload.ForceCharacterStatus;
        }
        if (payload.OverrideGiveToSelectedTarget)
        {
            target.OverrideGiveToSelectedTarget = true;
            target.GiveToSelectedTarget = payload.GiveToSelectedTarget;
        }
    }

    private static SkillEffectOverrides CloneOverrides(SkillEffectOverrides source)
    {
        source ??= new SkillEffectOverrides();

        return new SkillEffectOverrides
        {
            OverrideStack = source.OverrideStack,
            Stack = source.Stack,
            OverrideDuration = source.OverrideDuration,
            Duration = source.Duration,
            OverrideAmount = source.OverrideAmount,
            Amount = source.Amount,
            OverrideMinimum = source.OverrideMinimum,
            Minimum = source.Minimum,
            OverrideMaximum = source.OverrideMaximum,
            Maximum = source.Maximum,
            OverrideFlatValue = source.OverrideFlatValue,
            FlatValue = source.FlatValue,
            OverrideMultiplier = source.OverrideMultiplier,
            Multiplier = source.Multiplier,
            OverrideResourceKey = source.OverrideResourceKey,
            ResourceKey = source.ResourceKey,
            OverrideForceCharacterStatus = source.OverrideForceCharacterStatus,
            ForceCharacterStatus = source.ForceCharacterStatus,
            OverrideGiveToSelectedTarget = source.OverrideGiveToSelectedTarget,
            GiveToSelectedTarget = source.GiveToSelectedTarget
        };
    }

    private static SkillRollData CloneRollData(SkillRollData source)
    {
        return new SkillRollData
        {
            Index = source.Index,
            Type = source.Type,
            OverridePhysicalType = source.OverridePhysicalType,
            PhysicalType = source.PhysicalType,
            DiceMode = source.DiceMode,
            MinPower = source.MinPower,
            MaxPower = source.MaxPower,
            RngSource = source.RngSource,
            CoinFrontChance = source.CoinFrontChance,
            CoinBackPower = source.CoinBackPower,
            CoinFrontPower = source.CoinFrontPower,
            CoinFrontIsCritical = source.CoinFrontIsCritical,
            SlotMinimum = source.SlotMinimum,
            SlotMaximum = source.SlotMaximum,
            ChinchiroHifumiPower = source.ChinchiroHifumiPower,
            ChinchiroBlankPower = source.ChinchiroBlankPower,
            ChinchiroMokuPower = source.ChinchiroMokuPower,
            ChinchiroShigoroPower = source.ChinchiroShigoroPower,
            ChinchiroArashiPower = source.ChinchiroArashiPower,
            JudgmentModifier = source.JudgmentModifier,
            ReuseValueAcrossAction = source.ReuseValueAcrossAction,
            EffectEntries = source.EffectEntries,
            OnWinEffectEntries = source.OnWinEffectEntries,
            OnLoseEffectEntries = source.OnLoseEffectEntries,
            OnWinEffects = source.OnWinEffects,
            OnLoseEffects = source.OnLoseEffects
        };
    }
}
