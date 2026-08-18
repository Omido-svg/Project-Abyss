public class DataDuelSkill : DuelSkill
{
    private readonly SkillDefinition definition;

    protected override SkillDefinition RuntimeDefinition =>
        definition;

    public override bool CanBreakPart =>
        definition != null && definition.CanBreakPart;

    public override bool GainPrestige =>
        definition != null && definition.GainPrestige;

    public DataDuelSkill(SkillDefinition definition)
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