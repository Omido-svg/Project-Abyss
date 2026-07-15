using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Character Skill Set/Normal Enemy Skill Set",
    fileName = "NormalEnemySkillSet")]
public class NormalEnemySkillSet : ScriptableObject
{
    [Header("Normal Enemy Skills")]
    public SkillDefinition NormalAttack;
    public SkillDefinition DuelSkill;
    public SkillDefinition PrestigeSkill;

    public IEnumerable<SkillDefinition> Definitions
    {
        get
        {
            if (NormalAttack != null)
                yield return NormalAttack;

            if (DuelSkill != null)
                yield return DuelSkill;

            if (PrestigeSkill != null)
                yield return PrestigeSkill;
        }
    }

    public List<Skill> CreateRuntimeSkills()
    {
        List<Skill> result = new();

        AddRuntimeSkill(
            result,
            NormalAttack,
            ActionType.NormalAttack,
            nameof(NormalAttack));

        AddRuntimeSkill(
            result,
            DuelSkill,
            ActionType.Duel,
            nameof(DuelSkill));

        AddRuntimeSkill(
            result,
            PrestigeSkill,
            ActionType.Prestige,
            nameof(PrestigeSkill));

        return result;
    }

    private void OnValidate()
    {
        ValidateDefinition(
            NormalAttack,
            ActionType.NormalAttack,
            nameof(NormalAttack));

        ValidateDefinition(
            DuelSkill,
            ActionType.Duel,
            nameof(DuelSkill));

        ValidateDefinition(
            PrestigeSkill,
            ActionType.Prestige,
            nameof(PrestigeSkill));
    }

    private void AddRuntimeSkill(
        List<Skill> result,
        SkillDefinition definition,
        ActionType expectedType,
        string fieldName)
    {
        if (!ValidateDefinition(
                definition,
                expectedType,
                fieldName))
        {
            return;
        }

        Skill skill =
            definition.CreateRuntimeSkill();

        if (skill != null)
            result.Add(skill);
    }

    private bool ValidateDefinition(
        SkillDefinition definition,
        ActionType expectedType,
        string fieldName)
    {
        if (definition == null)
            return false;

        if (definition.ActionType == expectedType)
            return true;

        Debug.LogWarning(
            $"[{name}] {fieldName} ActionType 불일치 / " +
            $"Expected={expectedType}, Actual={definition.ActionType}",
            this);

        return false;
    }
}
