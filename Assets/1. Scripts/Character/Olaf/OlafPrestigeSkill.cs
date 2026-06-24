public class OlafPrestigeSkill : Skill
{
    public OlafPrestigeSkill()
    {
        SkillName = "불사의 광란";

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