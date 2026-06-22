public class OlafNormalAttack : Skill
{
    public OlafNormalAttack()
    {
        SkillName = "피의 일격";
        SkillType = SkillType.NORMALATTACK;

        BasePower = 5;

        Resolver = new CoinResolver(owner, 1);
    }

    public override void Execute(Character target)
    {
        target.TakeDamage(Roll());
        target.AddStatus(new Bleeding(1), owner);
    }
}