using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillUpgradeStep
{
    [TextArea(1, 4)] public string DesignNote;
    public int BasePowerDelta;
    public int AllRollMinPowerDelta;
    public int AllRollMaxPowerDelta;
    public List<int> PerRollPowerDelta = new();
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
}

public sealed class SkillUpgradeState
{
    private readonly Dictionary<string, int> levels = new(StringComparer.OrdinalIgnoreCase);

    public int GetLevel(SkillDefinition definition)
    {
        if (definition == null) return 0;
        return levels.TryGetValue(definition.SkillId, out int level) ? level : 0;
    }

    public bool TryUpgrade(SkillDefinition definition, int maximumLevel = 2)
    {
        if (definition == null) return false;
        int current = GetLevel(definition);
        if (current >= Mathf.Max(0, maximumLevel)) return false;
        levels[definition.SkillId] = current + 1;
        return true;
    }
}

public static class SkillUpgradeService
{
    public const int DefaultMaximumLevel = 2;

    public static int GetUpgradeCost(
        SkillDefinition definition,
        int targetLevel,
        float secondLevelMultiplier = 1.5f)
    {
        if (definition == null || targetLevel < 1 || targetLevel > 2)
            return 0;

        int baseCost = definition.UpgradeProfile?.GetBaseCost(definition.ActionType) ??
            definition.ActionType switch
            {
                ActionType.NormalAttack => 50,
                ActionType.Preparation => 100,
                ActionType.Duel => 125,
                ActionType.Prestige => 150,
                _ => 0
            };

        return targetLevel == 1
            ? baseCost
            : Mathf.RoundToInt(baseCost * Mathf.Max(0f, secondLevelMultiplier));
    }
}
