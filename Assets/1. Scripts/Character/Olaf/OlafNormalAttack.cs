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
        // 데미지는 DamageManager에서 처리

        action.TargetPart.AddStatus(
            new Bleeding(),
            action.Owner);
    }
}