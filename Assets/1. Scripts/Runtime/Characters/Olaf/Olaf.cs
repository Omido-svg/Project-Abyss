using System.Collections.Generic;
using UnityEngine;

public class Olaf : Character, ICharacterAuthoringTarget
{
    [Header("Legacy Skill Set Fallback")]
    [SerializeField]
    private OlafSkillSet skillSet;

    [Header("Body Parts")]
    [SerializeField, Min(1)]
    private int partHitPoints = 180;

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
            ActionType.NormalAttack =>
                new OlafNormalRuntimeSkill(definition),

            ActionType.Duel =>
                new OlafDuelRuntimeSkill(definition),

            ActionType.Preparation =>
                new OlafPreparationRuntimeSkill(definition),

            ActionType.Prestige =>
                new OlafPrestigeRuntimeSkill(definition),

            _ =>
                base.CreateRuntimeSkillForLoadout(definition)
        };
    }

    protected override void BuildBodyParts()
    {
        bodyParts.Clear();

        int hp = Mathf.Max(1, partHitPoints);

        bodyParts.Add(
            new BodyPart(
                PartType.HEAD,
                hp,
                CreateLegacySkillArray(
                    includePreparation: true,
                    includeAttacks: true)));

        bodyParts.Add(
            new BodyPart(
                PartType.LEFT_HAND,
                hp,
                CreateLegacySkillArray(
                    includePreparation: false,
                    includeAttacks: true)));

        bodyParts.Add(
            new BodyPart(
                PartType.RIGHT_HAND,
                hp,
                CreateLegacySkillArray(
                    includePreparation: false,
                    includeAttacks: true)));

        bodyParts.Add(
            new BodyPart(
                PartType.LEGS,
                hp,
                CreateLegacySkillArray(
                    includePreparation: true,
                    includeAttacks: false)));
    }

    protected override void BuildMechanics()
    {
        AddMechanic(
            new OlafMadnessMechanic());

        AddMechanic(
            new OlafImmortalFuryMechanic());
    }

    protected override StatusEffect CreateDisabledDebuff(
        BodyPart part)
    {
        return part?.Type switch
        {
            PartType.HEAD =>
                new HeadDisabled(),

            PartType.LEFT_HAND =>
                new ArmDisabled(PartType.LEFT_HAND),

            PartType.RIGHT_HAND =>
                new ArmDisabled(PartType.RIGHT_HAND),

            PartType.LEGS =>
                new LegsDisabled(),

            _ => null
        };
    }

    protected override StatusEffect CreateBrokenPartStatus(
        BodyPart part)
    {
        return part?.Type switch
        {
            PartType.HEAD =>
                new BrokenHead(),

            PartType.LEFT_HAND =>
                new BrokenArm(PartType.LEFT_HAND),

            PartType.RIGHT_HAND =>
                new BrokenArm(PartType.RIGHT_HAND),

            PartType.LEGS =>
                new BrokenLegs(),

            _ => null
        };
    }

    private Skill[] CreateLegacySkillArray(
        bool includePreparation,
        bool includeAttacks)
    {
        List<Skill> skills = new();

        if (skillSet == null)
            return skills.ToArray();

        if (includeAttacks)
        {
            AddIfNotNull(
                skills,
                skillSet.CreateNormalAttack());

            AddIfNotNull(
                skills,
                skillSet.CreateDuelSkill());
        }

        if (includePreparation)
        {
            AddIfNotNull(
                skills,
                skillSet.CreatePreparationSkill());
        }

        AddIfNotNull(
            skills,
            skillSet.CreatePrestigeSkill());

        return skills.ToArray();
    }

    private static void AddIfNotNull(
        ICollection<Skill> destination,
        Skill skill)
    {
        if (skill != null)
            destination.Add(skill);
    }
}
