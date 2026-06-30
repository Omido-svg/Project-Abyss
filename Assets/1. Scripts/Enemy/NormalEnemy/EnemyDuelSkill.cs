public class EnemyDuelSkill : DuelSkill
{
    public EnemyDuelSkill()
    {
        SkillName = "난폭한 공격(결투)";

        BasePower = 2;

        Resolver = new DiceResolver(2, 6);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Target == null)
            return;

        // 결투 기본 데미지는 DamageManager에서 처리
        // 추가 효과: 출혈 1스택
        action.Target.AddStatus(
            new Bleeding(1),
            action.Owner);
    }
}