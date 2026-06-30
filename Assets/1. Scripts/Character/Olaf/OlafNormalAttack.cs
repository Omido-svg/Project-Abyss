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
        if (action == null)
            return;

        if (action.Target == null)
            return;

        // 데미지는 DamageManager에서 처리
        // 출혈은 캐릭터 상태이상으로 부여

        action.Target.AddStatus(
            new Bleeding(1),
            action.Owner);
    }
}