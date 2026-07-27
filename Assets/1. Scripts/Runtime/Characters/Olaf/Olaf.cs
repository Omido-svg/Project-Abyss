using System.Collections.Generic;
using UnityEngine;

public class Olaf : Character, ICharacterAuthoringTarget
{
    [Header("Skill Set")]
    [SerializeField]
    private OlafSkillSet skillSet;

    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts =>
        bodyParts;

    public OlafMadnessMechanic MadnessMechanic =>
        GetMechanic<OlafMadnessMechanic>();

    public OlafImmortalFuryMechanic ImmortalFuryMechanic =>
        GetMechanic<OlafImmortalFuryMechanic>();

    public bool ApplyCharacterAuthoring(
        CharacterAuthoringBundle bundle)
    {
        if (bundle?.SkillSet is not OlafSkillSet configuredSkillSet)
            return false;

        skillSet = configuredSkillSet;
        return true;
    }

    public override Skill CreateRuntimeSkillForLoadout(
        SkillDefinition definition)
    {
        if (definition == null)
            return null;

        return definition.ActionType switch
        {
            ActionType.Duel =>
                new OlafDuelRuntimeSkill(
                    definition),

            ActionType.Preparation =>
                new OlafPreparationRuntimeSkill(
                    definition),

            _ =>
                base.CreateRuntimeSkillForLoadout(
                    definition)
        };
    }

    protected override void BuildBodyParts()
    {
        bodyParts.Clear();

        if (skillSet == null)
        {
            Debug.LogWarning(
                $"{nameof(Olaf)} SkillSet이 연결되지 않았습니다. " +
                "부위는 생성되지만 사용할 수 있는 스킬이 없습니다.");
        }

        bodyParts.Add(
            new BodyPart(
                PartType.HEAD,
                40,
                CreateSkillArray(
                    skillSet?.CreateNormalAttack(),
                    skillSet?.CreateDuelSkill(),
                    skillSet?.CreatePreparationSkill(),
                    skillSet?.CreatePrestigeSkill())));

        bodyParts.Add(
            new BodyPart(
                PartType.LEFT_HAND,
                30,
                CreateSkillArray(
                    skillSet?.CreateNormalAttack(),
                    skillSet?.CreateDuelSkill(),
                    skillSet?.CreatePrestigeSkill())));

        bodyParts.Add(
            new BodyPart(
                PartType.RIGHT_HAND,
                30,
                CreateSkillArray(
                    skillSet?.CreateNormalAttack(),
                    skillSet?.CreateDuelSkill(),
                    skillSet?.CreatePrestigeSkill())));

        bodyParts.Add(
            new BodyPart(
                PartType.LEGS,
                50,
                CreateSkillArray(
                    skillSet?.CreatePreparationSkill(),
                    skillSet?.CreatePrestigeSkill())));
    }

    protected override void BuildMechanics()
    {
        AddMechanic(
            new OlafMadnessMechanic());

        AddMechanic(
            new OlafImmortalFuryMechanic());

        // 피 묻은 도끼는 OlafBloodyAxeItem이 장착되었을 때
        // CharacterBuildController가 메커닉을 추가한다.
        // 기본 메커닉으로 중복 추가하지 않는다.
    }

    protected override StatusEffect CreateDisabledDebuff(
        BodyPart part)
    {
        if (part == null)
            return null;

        return part.Type switch
        {
            PartType.HEAD =>
                new HeadDisabled(),

            PartType.LEFT_HAND =>
                new ArmDisabled(
                    PartType.LEFT_HAND),

            PartType.RIGHT_HAND =>
                new ArmDisabled(
                    PartType.RIGHT_HAND),

            PartType.LEGS =>
                new LegsDisabled(),

            _ => null
        };
    }

    protected override StatusEffect CreateBrokenPartStatus(
        BodyPart part)
    {
        if (part == null)
            return null;

        return part.Type switch
        {
            PartType.HEAD =>
                new BrokenHead(),

            PartType.LEFT_HAND =>
                new BrokenArm(
                    PartType.LEFT_HAND),

            PartType.RIGHT_HAND =>
                new BrokenArm(
                    PartType.RIGHT_HAND),

            PartType.LEGS =>
                new BrokenLegs(),

            _ => null
        };
    }

    private Skill[] CreateSkillArray(
        params Skill[] skills)
    {
        List<Skill> result = new();

        if (skills == null)
            return result.ToArray();

        foreach (Skill skill in skills)
        {
            if (skill != null)
                result.Add(skill);
        }

        return result.ToArray();
    }
}