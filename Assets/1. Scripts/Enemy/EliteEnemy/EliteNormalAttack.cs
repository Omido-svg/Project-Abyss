public class EliteNormalAttack : NormalSkill
{
    public EliteNormalAttack()
    {
        SkillName = "정예병의 참격(일반공격)";

        BasePower = 6;

        Resolver = new DiceResolver(1, 4);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Target == null)
            return;

        // 기본 피해는 DamageManager에서 처리
        // 추가 효과: 출혈 1스택

        action.Target.AddStatus(
            new Bleeding(1),
            action.Owner);
    }
}