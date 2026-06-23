public class EnemyNormalAttack : Skill
{
    public EnemyNormalAttack()
    {
        SkillName = "물어뜯기";

        SkillType = SkillType.NORMALATTACK;

        BasePower = 5;

        Resolver = new DiceResolver(owner, 1, 6);
    }

    public override void Execute(BattleAction action)
    {
        // 추가 효과 없음
    }
}