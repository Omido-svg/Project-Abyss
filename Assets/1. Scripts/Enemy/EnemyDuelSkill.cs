public class EnemyDuelSkill : Skill
{
    public EnemyDuelSkill()
    {
        SkillName = "난폭한 공격";

        SkillType = SkillType.DUEL;

        BasePower = 8;

        Resolver = new DiceResolver(owner, 2, 6);
    }

    public override void Execute(Character target)
    {
        // 추가 효과 없음
    }
}