public class OlafNormalAttack : Skill
{
    public OlafNormalAttack()
    {
        SkillName = "피의 일격";
        SkillType = SkillType.NORMALATTACK;

        BasePower = 5;

        Resolver = new CoinResolver(owner, 1);
    }

    public override void Execute(BattleAction action)
    {
        action.Target.TakeDamage(
            action.TargetPart,
            Roll());
        action.Target.AddStatus(new Bleeding(1), owner);
    }
}