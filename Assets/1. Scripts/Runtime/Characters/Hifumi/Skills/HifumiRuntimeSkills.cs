public sealed class HifumiNormalRuntimeSkill : DataNormalSkill
{
    public HifumiNormalRuntimeSkill(SkillDefinition definition) : base(definition) { }
    public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;
}

public sealed class HifumiDuelRuntimeSkill : DataDuelSkill
{
    public HifumiDuelRuntimeSkill(SkillDefinition definition) : base(definition) { }
    public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;
}

public sealed class HifumiPreparationRuntimeSkill : DataPreparationSkill
{
    public HifumiPreparationRuntimeSkill(SkillDefinition definition) : base(definition) { }

    public override void Execute(BattleAction action)
    {
        base.Execute(action);
        action?.Owner?.GetMechanic<HifumiMechanic>()?.ExecuteSkill(action);
    }
}

public sealed class HifumiPrestigeRuntimeSkill : DataPrestigeSkill
{
    public HifumiPrestigeRuntimeSkill(SkillDefinition definition) : base(definition) { }

    public override void Execute(BattleAction action)
    {
        base.Execute(action);
        action?.Owner?.GetMechanic<HifumiMechanic>()?.ExecuteSkill(action);
    }
}
