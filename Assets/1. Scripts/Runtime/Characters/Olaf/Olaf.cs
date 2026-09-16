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

    public OlafRulebreakerMechanic RulebreakerMechanic =>
        GetMechanic<OlafRulebreakerMechanic>();

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
            new OlafRulebreakerMechanic());

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

    public override int EnergyCost
    {
        get
        {
            int baseCost = base.EnergyCost;
            return owner?.GetMechanic<OlafRulebreakerMechanic>()
                       ?.ResolveEnergyCost(Definition, baseCost) ?? baseCost;
        }
    }

    public override bool CanUseByResource(Character character)
    {
        OlafRulebreakerMechanic rulebreaker =
            character?.GetMechanic<OlafRulebreakerMechanic>();

        return (rulebreaker?.CanUse(Definition) ?? true) &&
               base.CanUseByResource(character);
    }

    public override bool TryConsumeResource(
        Character character,
        BattleAction sourceAction = null)
    {
        if (!base.TryConsumeResource(character, sourceAction))
            return false;

        character?.GetMechanic<OlafRulebreakerMechanic>()
            ?.RecordCommittedUse(Definition);
        return true;
    }

    protected override void OnPlanningUseRolledBack()
    {
        owner?.GetMechanic<OlafRulebreakerMechanic>()
            ?.RollbackCommittedUse(Definition);
    }

    public override PartBreakMode ResolvePartBreakMode(
        BattleAction action,
        BodyPart targetPart)
    {
        if (action?.PartBreakModeOverride != PartBreakMode.None)
            return action.PartBreakModeOverride;

        return base.ResolvePartBreakMode(action, targetPart);
    }

    public override void NotifyDuelMatched(
        BattleAction action,
        BattleAction opponentAction,
        int rollIndex,
        bool isOneSided)
    {
        base.NotifyDuelMatched(action, opponentAction, rollIndex, isOneSided);

        if (!isOneSided)
        {
            owner?.GetMechanic<OlafRulebreakerMechanic>()
                ?.NotifyDuelMatched(action, opponentAction);
        }
    }

    public override void Execute(BattleAction action)
    {
        base.Execute(action);
        action?.Owner?.GetMechanic<OlafRulebreakerMechanic>()?.Execute(action);
    }

    public override int GetMomentumPushBonus(BattleAction action) => 0;
}

public sealed class OlafPreparationRuntimeSkill : DataPreparationSkill
{
    public OlafPreparationRuntimeSkill(SkillDefinition definition)
        : base(definition)
    {
    }

    public override bool CanUseByResource(Character character)
    {
        OlafRulebreakerMechanic rulebreaker =
            character?.GetMechanic<OlafRulebreakerMechanic>();

        return (rulebreaker?.CanUse(Definition) ?? true) &&
               base.CanUseByResource(character);
    }

    public override void Execute(BattleAction action)
    {
        base.Execute(action);
        action?.Owner?.GetMechanic<OlafMadnessMechanic>()?.ExecuteSkill(action);
        action?.Owner?.GetMechanic<OlafRulebreakerMechanic>()?.Execute(action);
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

