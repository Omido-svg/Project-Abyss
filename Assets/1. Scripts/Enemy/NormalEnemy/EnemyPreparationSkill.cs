public class EnemyPreparationSkill : PreparationSkill
{
    public EnemyPreparationSkill()
    {
        SkillName = "위협적인 포효(도사림)";

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
        // 여기서는 직접 데미지 대신 상태이상만 부여

        action.Target.AddStatus(
            new Burn(1),
            action.Owner);
    }
}