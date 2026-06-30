public class ElitePreparationSkill : PreparationSkill
{
    public ElitePreparationSkill()
    {
        SkillName = "전투 태세(도사림)";

        BasePower = 0;

        Resolver = new DiceResolver(0, 0);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Target == null)
            return;

        // 도사림은 합에 참여하지 않는 선행 행동
        // 직접 피해 대신 상태이상 부여

        action.Target.AddStatus(
            new Burn(1),
            action.Owner);

        action.Target.AddStatus(
            new Bleeding(1),
            action.Owner);
    }
}