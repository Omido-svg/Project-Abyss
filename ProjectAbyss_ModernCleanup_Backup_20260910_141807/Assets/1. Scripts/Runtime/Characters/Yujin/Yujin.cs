using System.Collections.Generic;
using UnityEngine;

public sealed class Yujin : Character, ICharacterAuthoringTarget, IPhysicalDamageTypeProvider
{
    [Header("Body Parts")]
    [SerializeField, Min(1)]
    private int partHitPoints = 180;

    [SerializeField]
    private YujinWeaponType startingWeapon =
        YujinWeaponType.Baeku;

    [Header("Weapon Physical Types — provisional, inspector-overridable")]
    [SerializeField] private PhysicalDamageType baekuPhysicalType = PhysicalDamageType.Cut;
    [SerializeField] private PhysicalDamageType jeokseolPhysicalType = PhysicalDamageType.Pierce;
    [SerializeField] private PhysicalDamageType nakilPhysicalType = PhysicalDamageType.Cut;

    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts =>
        bodyParts;

    public YujinMechanic YujinMechanic =>
        GetMechanic<YujinMechanic>();

    public YujinWeaponType StartingWeapon =>
        startingWeapon;

    public PhysicalDamageType ResolveWeaponPhysicalType() =>
        ResolveWeaponPhysicalType(YujinMechanic?.CurrentWeapon ?? startingWeapon);

    public PhysicalDamageType ResolveWeaponPhysicalType(YujinWeaponType weapon) =>
        weapon switch
        {
            YujinWeaponType.Baeku => baekuPhysicalType,
            YujinWeaponType.Jeokseol => jeokseolPhysicalType,
            YujinWeaponType.Nakil => nakilPhysicalType,
            _ => PhysicalDamageType.Cut
        };

    public bool TryResolvePhysicalDamageType(
        Skill skill,
        SkillRollData roll,
        out PhysicalDamageType damageType)
    {
        // 기존 규칙: 유진의 현재 무기 속성이 개별 Roll override보다 우선한다.
        damageType =
            ResolveWeaponPhysicalType();

        return true;
    }

    public bool ApplyCharacterAuthoring(
        CharacterAuthoringBundle bundle)
    {
        return bundle != null &&
               bundle.SkillSet == null;
    }

    public override Skill CreateRuntimeSkillForLoadout(
        SkillDefinition definition)
    {
        if (definition == null)
            return null;

        return definition.ActionType switch
        {
            ActionType.NormalAttack =>
                new YujinNormalRuntimeSkill(definition),

            ActionType.Duel =>
                new YujinDuelRuntimeSkill(definition),

            ActionType.Preparation =>
                new YujinPreparationRuntimeSkill(definition),

            ActionType.Prestige =>
                new YujinPrestigeRuntimeSkill(definition),

            _ =>
                base.CreateRuntimeSkillForLoadout(definition)
        };
    }

    protected override void BuildBodyParts()
    {
        bodyParts.Clear();

        int hp = Mathf.Max(1, partHitPoints);
        Skill[] none = System.Array.Empty<Skill>();

        bodyParts.Add(
            new BodyPart(
                PartType.HEAD,
                hp,
                none));

        bodyParts.Add(
            new BodyPart(
                PartType.LEFT_HAND,
                hp,
                none));

        bodyParts.Add(
            new BodyPart(
                PartType.RIGHT_HAND,
                hp,
                none));

        bodyParts.Add(
            new BodyPart(
                PartType.LEGS,
                hp,
                none));
    }

    protected override void BuildMechanics()
    {
        AddMechanic(
            new YujinMechanic(
                startingWeapon));
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
}