using UnityEngine;

public class DataPrestigeSkill : PrestigeSkill, IVisualSkill
{
    private readonly SkillDefinition definition;

    protected override SkillDefinition RuntimeDefinition =>
        definition;

    public SkillVisualDefinition VisualDefinition =>
        definition?.VisualDefinition;

    public override bool CanBreakPart =>
        definition != null && definition.CanBreakPart;

    public override bool GainPrestige =>
        definition != null && definition.GainPrestige;

    public DataPrestigeSkill(SkillDefinition definition)
    {
        this.definition = definition;

        if (definition == null)
        {
            SkillName = "NULL SKILL";
            BasePower = 0;
            Resolver = new DiceResolver(0, 0);
            return;
        }

        SkillName = definition.SkillName;
        BasePower = definition.BasePower;
        Resolver = definition.CreateResolver();
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        ExecuteDefinitionEffects(
            action,
            SkillEffectTiming.OnExecute);
    }
}
