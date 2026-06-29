public class EnemyNormalAttack : NormalSkill
{
    public EnemyNormalAttack()
    {
        SkillName = "물어뜯기(일반공격)";

        BasePower = 5;

        Resolver = new DiceResolver(1, 2);
    }

    public override void Execute(BattleAction action)
    {
        // 추가 효과 없음
    }
}