public class OlafNormalAttack : NormalSkill
{
    public OlafNormalAttack()
    {
        SkillName = "피의 일격(일반공격)";

        BasePower = 5;

        Resolver = new CoinResolver(1);
    }

    public override void Execute(BattleAction action)
    {
        action.Target.TakeDamage(
            action.TargetPart,
            action.RollPower());
        action.Target.AddStatus(new Bleeding(1), owner);
    }
}