public class EliteDuelSkill : DuelSkill
{
    public EliteDuelSkill()
    {
        SkillName = "정예병의 압박(결투)";

        BasePower = 7;

        Resolver = new DiceResolver(2, 4);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Target == null)
            return;

        // 결투 피해는 DamageManager에서 처리
        // 추가 효과: 출혈 2스택

        action.Target.AddStatus(
            new Bleeding(2),
            action.Owner);
    }
}