public class DataPrestigeSkill : PrestigeSkill, IVisualSkill
{
    private readonly SkillDefinition definition;
    
    public SkillVisualDefinition VisualDefinition =>
        definition.VisualDefinition;

    public override bool CanBreakPart => definition.CanBreakPart;
    public override bool GainPrestige => definition.GainPrestige;

    public DataPrestigeSkill(SkillDefinition definition)
    {
        this.definition = definition;

        SkillName = definition.SkillName;
        BasePower = definition.BasePower;
        Resolver = definition.CreateResolver();
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        SkillEffectContext context =
            new SkillEffectContext(
                action,
                definition);

        foreach (SkillEffectDefinition effect in definition.Effects)
        {
            if (effect == null)
                continue;

            effect.Apply(context);
        }
    }
}