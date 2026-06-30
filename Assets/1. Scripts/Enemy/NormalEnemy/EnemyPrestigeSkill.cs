public class EnemyPrestigeSkill : PrestigeSkill
{
    public EnemyPrestigeSkill()
    {
        SkillName = "광폭한 포식(위세)";

        BasePower = 10;

        Resolver = new DiceResolver(2, 8);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Target == null)
            return;

        if (action.TargetPart == null)
            return;

        //--------------------------------
        // 위세 스킬은 특수 필살기라 직접 피해 처리
        //--------------------------------

        int damage = action.RollPower();

        action.Target.TakeDamage(
            action.TargetPart,
            damage,
            true);

        //--------------------------------
        // 추가 효과: 출혈 + 화상
        //--------------------------------

        action.Target.AddStatus(
            new Bleeding(2),
            action.Owner);

        action.Target.AddStatus(
            new Burn(1),
            action.Owner);

        //--------------------------------
        // 약화 부위라면 파괴 시도
        //--------------------------------

        if (action.TargetPart.IsWeakened)
        {
            action.Target.BreakPart(action.TargetPart);
        }
    }
}