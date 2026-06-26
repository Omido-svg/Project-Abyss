public class OlafPrestigeSkill : Skill
{
    public override ActionType ActionType => ActionType.Prestige;
    public OlafPrestigeSkill()
    {
        SkillName = "불사의 광란(위세)";

        BasePower = 20;

        Resolver = new CoinResolver(5);
    }

    public override void Execute(BattleAction action)
    {
        action.Target.TakeDamage(
            action.TargetPart,
            action.RollPower());

        // 출혈 폭발
        // 광기 초기화
        // 부위 파괴
    }
}