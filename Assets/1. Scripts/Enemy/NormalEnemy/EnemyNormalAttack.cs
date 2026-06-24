public class EnemyNormalAttack : Skill
{
    public EnemyNormalAttack()
    {
        SkillName = "물어뜯기";

        BasePower = 5;

        Resolver = new DiceResolver(1, 6);
    }

    public override void Execute(BattleAction action)
    {
        // 추가 효과 없음
    }
}