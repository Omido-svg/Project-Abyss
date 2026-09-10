using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AIActionSource
{
    public AIActionSource(
        Enemy owner,
        BodyPart part,
        int maxSlots)
    {
        Owner = owner;
        Part = part;
        MaxSlots = Mathf.Max(0, maxSlots);
    }

    public Enemy Owner { get; }
    public BodyPart Part { get; }
    public int MaxSlots { get; }

    public IReadOnlyList<Skill> Skills => GetSkills(0);

    public CharacterSlotConfig GetSlotConfig(int actionIndex) =>
        Owner?.CombatRulesRuntime?.GetSlotConfig(Part, Mathf.Max(0, actionIndex));

    public IReadOnlyList<Skill> GetSkills(
        int actionIndex)
    {
        if (Owner == null)
            return Array.Empty<Skill>();

        IReadOnlyList<Skill> configured =
            Owner.GetSelectableSkills(
                Part,
                Mathf.Max(0, actionIndex));

        return configured ?? Array.Empty<Skill>();
    }

    public bool IsValid
    {
        get
        {
            if (Owner == null ||
                Owner.IsDead ||
                MaxSlots <= 0 ||
                (Part == null
                    ? (!Owner.IsSingleHpTarget &&
                       (Owner.CombatRulesRuntime?.GetSlotCountForPart(null) ?? 0) <= 0)
                    : Part.IsBroken))
            {
                return false;
            }

            for (int actionIndex = 0;
                 actionIndex < MaxSlots;
                 actionIndex++)
            {
                if (GetSkills(actionIndex).Count > 0)
                    return true;
            }

            return false;
        }
    }
}

public sealed class AISlotPlanner
{
    public List<AIActionSource> CreateSources(
        Enemy enemy)
    {
        List<AIActionSource> result = new();

        if (enemy == null || enemy.IsDead)
            return result;

        if (enemy.UsesBodyParts)
        {
            AddBodyPartSources(enemy, result);
            AddGlobalStructuredSource(enemy, result);
            return result;
        }

        AddSingleHpSource(enemy, result);
        return result;
    }

    private void AddBodyPartSources(
        Enemy enemy,
        List<AIActionSource> result)
    {
        if (enemy.BodyParts == null)
            return;

        foreach (BodyPart part in enemy.BodyParts)
        {
            if (part == null || part.IsBroken)
                continue;

            int maxSlots =
                Mathf.Max(
                    0,
                    enemy.GetMaxActionSlotsForPart(part));

            AIActionSource source =
                new AIActionSource(
                    enemy,
                    part,
                    maxSlots);

            if (source.IsValid)
                result.Add(source);
        }
    }

    private void AddGlobalStructuredSource(
        Enemy enemy,
        List<AIActionSource> result)
    {
        int maxSlots = Mathf.Max(0, enemy.GetMaxActionSlotsForPart(null));
        if (maxSlots <= 0)
            return;

        AIActionSource source = new AIActionSource(
            enemy, null, maxSlots);
        if (source.IsValid)
            result.Add(source);
    }

    private void AddSingleHpSource(
        Enemy enemy,
        List<AIActionSource> result)
    {

        int maxSlots =
            Mathf.Max(
                0,
                enemy.GetMaxActionSlotsForPart(null));

        AIActionSource source =
            new AIActionSource(
                enemy,
                null,
                maxSlots);

        if (source.IsValid)
            result.Add(source);
    }

}