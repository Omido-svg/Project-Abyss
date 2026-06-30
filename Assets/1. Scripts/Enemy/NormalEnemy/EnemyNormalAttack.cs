public class EnemyNormalAttack : NormalSkill
{
    public EnemyNormalAttack()
    {
        SkillName = "물어뜯기(일반공격)";

        BasePower = 5;

        Resolver = new DiceResolver(1, 2);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Target == null)
            return;

        // 일반공격 기본 데미지는 DamageManager에서 처리
        // 추가 효과: 낮은 출혈
        action.Target.AddStatus(
            new Bleeding(1),
            action.Owner);
    }
}