using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AIActionSource
{
    private readonly IReadOnlyList<Skill> fallbackSkills;

    public AIActionSource(
        Enemy owner,
        BodyPart part,
        IReadOnlyList<Skill> skills,
        int maxSlots)
    {
        Owner = owner;
        Part = part;
        fallbackSkills = skills ?? Array.Empty<Skill>();
        MaxSlots = Mathf.Max(0, maxSlots);
    }

    public Enemy Owner { get; }
    public BodyPart Part { get; }
    public int MaxSlots { get; }

    // 구버전 호출부 호환. 슬롯별 허용 스킬은 GetSkills(actionIndex)를 사용한다.
    public IReadOnlyList<Skill> Skills => GetSkills(0);

    public CharacterSlotConfig GetSlotConfig(int actionIndex) =>
        Owner?.CombatRulesRuntime?.GetSlotConfig(Part, Mathf.Max(0, actionIndex));

    public IReadOnlyList<Skill> GetSkills(
        int actionIndex)
    {
        if (Owner == null)
            return fallbackSkills;

        IReadOnlyList<Skill> configured =
            Owner.GetSelectableSkills(
                Part,
                Mathf.Max(0, actionIndex));

        if (Owner.CombatRulesRuntime?.HasStructuredRules == true)
        {
            return configured ?? Array.Empty<Skill>();
        }

        return configured != null && configured.Count > 0
            ? configured
            : fallbackSkills;
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
                    part.AvailableSkills,
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
            enemy, null, enemy.RuntimeSkills, maxSlots);
        if (source.IsValid)
            result.Add(source);
    }

    private void AddSingleHpSource(
        Enemy enemy,
        List<AIActionSource> result)
    {
        IReadOnlyList<Skill> skills =
            GetSingleHpSkills(enemy);

        int maxSlots =
            Mathf.Max(
                0,
                enemy.GetMaxActionSlotsForPart(null));

        AIActionSource source =
            new AIActionSource(
                enemy,
                null,
                skills,
                maxSlots);

        if (source.IsValid)
            result.Add(source);
    }

    private IReadOnlyList<Skill> GetSingleHpSkills(
        Enemy enemy)
    {
        // 현재 단일 HP 일반몹은 CharacterSkills를 런타임 스킬 공급원으로 사용한다.
        if (enemy is NormalEnemy normalEnemy)
        {
            return normalEnemy.CharacterSkills ??
                   Array.Empty<Skill>();
        }

        Debug.LogWarning(
            $"[AI SOURCE SKIP] 단일 HP 적의 런타임 스킬 공급자를 찾지 못했습니다. " +
            $"Enemy={GetCharacterName(enemy)}");

        return Array.Empty<Skill>();
    }

    private string GetCharacterName(
        Character character)
    {
        if (character == null)
            return "NULL";

        return character.Data == null
            ? character.name
            : character.Data.CharacterName;
    }
}