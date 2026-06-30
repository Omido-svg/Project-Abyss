public class ElitePrestigeSkill : PrestigeSkill
{
    public ElitePrestigeSkill()
    {
        SkillName = "처형자의 일격(위세)";

        BasePower = 14;

        Resolver = new DiceResolver(2, 6);
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
        // 위세 스킬은 특수 행동이라 직접 피해 처리
        //--------------------------------

        int damage = action.RollPower();

        action.Target.TakeDamage(
            action.TargetPart,
            damage,
            true);

        //--------------------------------
        // 추가 효과
        //--------------------------------

        action.Target.AddStatus(
            new Bleeding(3),
            action.Owner);

        //--------------------------------
        // 약화된 부위라면 파괴
        //--------------------------------

        if (action.TargetPart.IsWeakened)
        {
            action.Target.BreakPart(action.TargetPart);
        }
    }
}