using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill/Skill Catalog",
    fileName = "ProjectAbyssSkillCatalog")]
public sealed class SkillCatalog : ScriptableObject
{
    [SerializeField] private List<SkillDefinition> skills = new();

    public IReadOnlyList<SkillDefinition> Skills => skills;

    public IEnumerable<SkillDefinition> Enumerate(ActionType actionType)
    {
        if (skills == null)
            yield break;

        for (int i = 0; i < skills.Count; i++)
        {
            SkillDefinition definition = skills[i];
            if (definition != null && definition.ActionType == actionType)
                yield return definition;
        }
    }

    public bool Contains(SkillDefinition definition)
    {
        return definition != null &&
               skills != null &&
               skills.Contains(definition);
    }

    public SkillDefinition FindById(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId) || skills == null)
            return null;

        for (int i = 0; i < skills.Count; i++)
        {
            SkillDefinition definition = skills[i];
            if (definition == null)
                continue;

            if (string.Equals(
                    definition.SkillId,
                    skillId,
                    StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    public void ReplaceAll(IEnumerable<SkillDefinition> definitions)
    {
        skills ??= new List<SkillDefinition>();
        skills.Clear();

        if (definitions == null)
            return;

        HashSet<SkillDefinition> visited = new();
        foreach (SkillDefinition definition in definitions)
        {
            if (definition != null && visited.Add(definition))
                skills.Add(definition);
        }

        skills.Sort((left, right) =>
        {
            int typeCompare = left.ActionType.CompareTo(right.ActionType);
            if (typeCompare != 0)
                return typeCompare;

            return string.Compare(
                left.SkillName,
                right.SkillName,
                StringComparison.Ordinal);
        });
    }

    private void OnValidate()
    {
        skills ??= new List<SkillDefinition>();
        HashSet<SkillDefinition> visited = new();

        for (int i = skills.Count - 1; i >= 0; i--)
        {
            SkillDefinition definition = skills[i];
            if (definition == null || !visited.Add(definition))
                skills.RemoveAt(i);
        }
    }
#endif
}
