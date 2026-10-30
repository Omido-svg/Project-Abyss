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
        BasePower = PowerFormulaService.ResolveBasePower(definition);
        Resolver = definition.CreateResolver();
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        // R1: card OnExecute is invoked per *actual* roll by ClashManager.
        // The Duel-vs-Duel card-effect gate is enforced at the roll boundary.
    }
}
