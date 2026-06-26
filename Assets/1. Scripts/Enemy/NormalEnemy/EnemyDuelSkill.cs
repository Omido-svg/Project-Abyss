public class EnemyDuelSkill : DuelSkill
{
    public EnemyDuelSkill()
    {
        SkillName = "난폭한 공격(결투)";

        BasePower = 8;

        Resolver = new DiceResolver(2, 6);
    }

    public override void Execute(BattleAction action)
    {
        // 추가 효과 없음
    }
}