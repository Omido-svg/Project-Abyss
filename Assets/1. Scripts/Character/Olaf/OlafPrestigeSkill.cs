public class OlafPrestigeSkill : Skill
{
    public OlafPrestigeSkill()
    {
        SkillName = "불사의 광란";

        SkillType = SkillType.PRESTIGE;

        BasePower = 20;

        Resolver = new CoinResolver(owner, 5);
    }

    public override void Execute(Character target)
    {
        target.TakeDamage(Roll());

        // 출혈 폭발
        // 광기 스택 초기화
        // 부위 파괴
    }
}