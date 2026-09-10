using System.Collections.Generic;
using UnityEngine;

public class EliteEnemy : Enemy, ICharacterAuthoringTarget
{
    [Header("Skill Set")]
    [SerializeField]
    private EliteEnemySkillSet skillSet;

    [Header("Posture")]
    [SerializeField]
    private bool usePostureRotation = true;

    [SerializeField]
    private EnemyPostureSettings postureSettings = new();

    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;

    public override bool SupportsLastStand => true;

    public bool ApplyCharacterAuthoring(
        CharacterAuthoringBundle bundle)
    {
        if (bundle?.SkillSet is not EliteEnemySkillSet configuredSkillSet)
            return false;

        skillSet = configuredSkillSet;
        usePostureRotation = bundle.UseElitePostureRotation;
        postureSettings =
            bundle.ElitePostureSettings ??
            new EnemyPostureSettings();

        return true;
    }

    //--------------------------------
    // EliteEnemy는 도사림 사용 허용
    //--------------------------------

    protected override bool AllowPreparationSkillAI => true;

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

        bodyParts.Add(
            new BodyPart(
                PartType.LEFT_HAND,
                50,
                CreateHandSkillSet()));

        bodyParts.Add(
            new BodyPart(
                PartType.RIGHT_HAND,
                50,
                CreateHandSkillSet()));

        bodyParts.Add(
            new BodyPart(
                PartType.LEGS,
                50,
                CreateLegSkillSet()));
    }

    //--------------------------------
    // 스킬 생성
    //--------------------------------

    private Skill[] CreateHeadSkillSet()
    {
        return CreateSkillArray(
            CreateSkill(skillSet != null ? skillSet.NormalAttack : null),
            CreateSkill(skillSet != null ? skillSet.DuelSkill : null),
            CreateSkill(skillSet != null ? skillSet.PreparationSkill : null),
            CreatePrestigeSkill());
    }

    private Skill[] CreateHandSkillSet()
    {
        return CreateSkillArray(
            CreateSkill(skillSet != null ? skillSet.NormalAttack : null),
            CreateSkill(skillSet != null ? skillSet.DuelSkill : null));
    }

    private Skill[] CreateLegSkillSet()
    {
        return CreateSkillArray(
            CreateSkill(skillSet != null ? skillSet.PreparationSkill : null));
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
            new EliteEnemyMechanic());

        if (usePostureRotation)
        {
            AddMechanic(
                new EnemyPostureMechanic(postureSettings));
        }
    }

    public override int GetMaxCombatActionSlots()
    {
        EnemyPostureMechanic posture =
            GetMechanic<EnemyPostureMechanic>();

        return posture?.CurrentAttackSlotLimit ?? 3;
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
            PartType.LEFT_HAND => new ArmDisabled(PartType.LEFT_HAND),
            PartType.RIGHT_HAND => new ArmDisabled(PartType.RIGHT_HAND),
            PartType.LEGS => new LegsDisabled(),
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
            PartType.LEFT_HAND => new BrokenArm(PartType.LEFT_HAND),
            PartType.RIGHT_HAND => new BrokenArm(PartType.RIGHT_HAND),
            PartType.LEGS => new BrokenLegs(),
            _ => null
        };
    }

    //--------------------------------

    public override void Die()
    {
        base.Die();
    }
}
