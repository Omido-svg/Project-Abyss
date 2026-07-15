using System.Collections.Generic;
using UnityEngine;

public class NormalEnemy : Enemy
{
    [Header("Skill Set")]
    [SerializeField]
    private NormalEnemySkillSet skillSet;

    [Header("Single HP")]
    [SerializeField, Min(1)]
    private int singleMaxHP = 50;

    private readonly List<BodyPart> bodyParts = new();
    private readonly List<Skill> characterSkills = new();

    public override IReadOnlyList<BodyPart> BodyParts =>
        bodyParts;

    public IReadOnlyList<Skill> CharacterSkills =>
        characterSkills;

    protected override void BuildBodyParts()
    {
        // 일반몹은 가짜 HEAD를 만들지 않는다.
        bodyParts.Clear();
        characterSkills.Clear();

        AddRuntimeSkill(
            skillSet != null
                ? skillSet.NormalAttack
                : null);

        AddRuntimeSkill(
            skillSet != null
                ? skillSet.DuelSkill
                : null);

        AddRuntimeSkill(
            skillSet != null
                ? skillSet.PrestigeSkill
                : null);
    }

    protected override IEnumerable<Skill>
        GetCharacterSkills()
    {
        return characterSkills;
    }

    protected override IReadOnlyList<Skill>
        GetAvailableSkills(BodyPart part)
    {
        return part == null
            ? characterSkills
            : null;
    }

    protected override ICombatTargetModel
        CreateCombatTargetModel()
    {
        return new SingleHpTargetModel(
            singleMaxHP);
    }

    private void AddRuntimeSkill(
        SkillDefinition definition)
    {
        if (definition == null)
            return;

        Skill skill =
            definition.CreateRuntimeSkill();

        if (skill != null)
            characterSkills.Add(skill);
    }

    protected override void BuildMechanics()
    {
        base.BuildMechanics();

        AddMechanic(
            new NormalEnemyBloodScentMechanic());
    }

    protected override StatusEffect
        CreateDisabledDebuff(BodyPart part)
    {
        return null;
    }

    protected override StatusEffect
        CreateBrokenPartStatus(BodyPart part)
    {
        return null;
    }
}
