public class OlafDuelSkill : Skill
{
    public OlafDuelSkill()
    {
        SkillName = "광전사의 결투(결투)";

        BasePower = 8;

        Resolver = new CoinResolver(3);
    }

    public override void Execute(BattleAction action)
    {
        // 실제 효과는 합 승리 시 BattleEvent에서 처리
    }
}