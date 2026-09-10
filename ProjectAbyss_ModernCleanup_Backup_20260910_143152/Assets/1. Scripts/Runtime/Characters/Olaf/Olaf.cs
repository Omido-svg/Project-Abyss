using System.Collections.Generic;
using UnityEngine;

public class Olaf : Character, ICharacterAuthoringTarget
{

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
        return bundle != null &&
               bundle.Kind == CharacterAuthoringKind.Olaf &&
               bundle.CombatLoadout != null;
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

        bodyParts.Add(new BodyPart(PartType.HEAD, hp));
        bodyParts.Add(new BodyPart(PartType.LEFT_HAND, hp));
        bodyParts.Add(new BodyPart(PartType.RIGHT_HAND, hp));
        bodyParts.Add(new BodyPart(PartType.LEGS, hp));
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

}

public sealed class OlafNormalRuntimeSkill : DataNormalSkill
{
    public OlafNormalRuntimeSkill(SkillDefinition definition)
        : base(definition)
    {
    }
}

public sealed class OlafDuelRuntimeSkill : DataDuelSkill
{
    public OlafDuelRuntimeSkill(SkillDefinition definition)
        : base(definition)
    {
    }

    public override int GetMomentumPushBonus(BattleAction action) => 0;
}

public sealed class OlafPreparationRuntimeSkill : DataPreparationSkill
{
    public OlafPreparationRuntimeSkill(SkillDefinition definition)
        : base(definition)
    {
    }

    public override void Execute(BattleAction action)
    {
        base.Execute(action);
        action?.Owner?.GetMechanic<OlafMadnessMechanic>()?.ExecuteSkill(action);
    }
}

public sealed class OlafPrestigeRuntimeSkill : DataPrestigeSkill
{
    public OlafPrestigeRuntimeSkill(SkillDefinition definition)
        : base(definition)
    {
    }

    public override void Execute(BattleAction action)
    {
        base.Execute(action);
        action?.Owner?.GetMechanic<OlafMadnessMechanic>()?.ExecuteSkill(action);
    }
}

