using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AIActionSource
{
    public AIActionSource(
        Enemy owner,
        BodyPart part,
        IReadOnlyList<Skill> skills,
        int maxSlots)
    {
        Owner = owner;
        Part = part;
        Skills = skills ?? Array.Empty<Skill>();
        MaxSlots = Mathf.Max(0, maxSlots);
    }

    public Enemy Owner { get; }
    public BodyPart Part { get; }
    public IReadOnlyList<Skill> Skills { get; }
    public int MaxSlots { get; }

    public bool IsValid =>
        Owner != null &&
        !Owner.IsDead &&
        MaxSlots > 0 &&
        Skills.Count > 0 &&
        (Part == null
            ? Owner.IsSingleHpTarget
            : !Part.IsBroken);
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
        // 현재 단일 HP 일반몹은 P3부터 CharacterSkills를 공개한다.
        // 이후 다른 단일 HP 적을 추가할 때는 같은 런타임 스킬 공급 계약을
        // 구현하거나 이 메서드에 명시적으로 연결하면 된다.
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
