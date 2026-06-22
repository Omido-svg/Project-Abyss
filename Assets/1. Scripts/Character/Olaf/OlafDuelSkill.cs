public class OlafDuelSkill : Skill
{
    public OlafDuelSkill()
    {
        SkillName = "광전사의 결투";

        SkillType = SkillType.DUEL;

        BasePower = 8;

        Resolver = new CoinResolver(owner, 3);
    }

    public override void Execute(Character target)
    {
        // 실제 효과는 합 승리 시 BattleEvent에서 처리
    }
}