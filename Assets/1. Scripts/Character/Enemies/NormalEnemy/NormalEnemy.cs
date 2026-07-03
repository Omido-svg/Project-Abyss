using System.Collections.Generic;
using UnityEngine;

public class NormalEnemy : Enemy
{
    [Header("Skill Set")]
    [SerializeField]
    private NormalEnemySkillSet skillSet;

    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    //--------------------------------
    // 부위 구성
    //--------------------------------

    protected override void BuildBodyParts()
    {
        bodyParts.Clear();

        bodyParts.Add(
            new BodyPart(
                PartType.HEAD,
                50,
                CreateHeadSkillSet()));
    }

    //--------------------------------
    // 스킬 생성
    //--------------------------------

    private Skill[] CreateHeadSkillSet()
    {
        return CreateSkillArray(
            CreateSkill(skillSet != null ? skillSet.NormalAttack : null),
            CreateSkill(skillSet != null ? skillSet.DuelSkill : null),
            CreatePrestigeSkill());
    }

    private Skill CreateSkill(SkillDefinition definition)
    {
        if (definition == null)
            return null;

        return definition.CreateRuntimeSkill();
    }

    private Skill CreatePrestigeSkill()
    {
        if (skillSet == null)
            return null;

        if (skillSet.PrestigeSkill == null)
            return null;

        return skillSet.PrestigeSkill.CreateRuntimeSkill();
    }

    private Skill[] CreateSkillArray(params Skill[] skills)
    {
        List<Skill> result = new();

        foreach (Skill skill in skills)
        {
            if (skill == null)
                continue;

            result.Add(skill);
        }

        return result.ToArray();
    }

    //--------------------------------
    // 메커닉 구성
    //--------------------------------

    protected override void BuildMechanics()
    {
        base.BuildMechanics();

        AddMechanic(
            new NormalEnemyBloodScentMechanic());
    }

    //--------------------------------
    // 약화 디버프
    //--------------------------------

    protected override StatusEffect CreateDisabledDebuff(
        BodyPart part)
    {
        if (part == null)
            return null;

        return part.Type switch
        {
            PartType.HEAD => new HeadDisabled(),
            _ => null
        };
    }

    //--------------------------------
    // 파괴 디버프
    //--------------------------------

    protected override StatusEffect CreateBrokenPartStatus(
        BodyPart part)
    {
        if (part == null)
            return null;

        return part.Type switch
        {
            PartType.HEAD => new BrokenHead(),
            _ => null
        };
    }

    //--------------------------------

    public override void Die()
    {
        base.Die();
    }
}