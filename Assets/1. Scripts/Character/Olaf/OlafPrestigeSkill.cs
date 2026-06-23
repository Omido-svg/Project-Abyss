public class OlafPrestigeSkill : Skill
{
    public OlafPrestigeSkill()
    {
        SkillName = "불사의 광란";

        SkillType = SkillType.PRESTIGE;

        BasePower = 20;

        Resolver = new CoinResolver(owner, 5);
    }

    public override void Execute(BattleAction action)
    {
        action.Target.TakeDamage(
            action.TargetPart,
            Roll());

        // 출혈 폭발
        // 광기 초기화
        // 부위 파괴
    }
}