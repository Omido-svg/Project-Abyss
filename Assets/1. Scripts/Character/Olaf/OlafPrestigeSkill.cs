public class OlafPrestigeSkill : PrestigeSkill
{
    public OlafPrestigeSkill()
    {
        SkillName = "불사의 광란(위세)";

        BasePower = 20;

        Resolver = new CoinResolver(5);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Owner == null)
            return;

        if (action.Target == null)
            return;

        if (action.TargetPart == null)
            return;

        //--------------------------------
        // 위세 스킬은 특수 필살기
        // 직접 피해 가능
        //--------------------------------

        int damage = action.RollPower();

        action.Target.TakeDamage(
            action.TargetPart,
            damage,
            true);

        //--------------------------------
        // 출혈 폭발
        //--------------------------------

        Bleeding bleeding =
            action.Target.GetStatus<Bleeding>();

        if (bleeding != null)
        {
            int explosionDamage =
                bleeding.Stack * 5;

            action.Target.TakeTrueDamage(
                explosionDamage,
                bleeding);

            action.Target.RemoveStatus(bleeding);
        }

        //--------------------------------
        // 대상 부위 강제 파괴 시도
        //--------------------------------

        if (action.TargetPart.IsWeakened)
        {
            action.Target.BreakPart(action.TargetPart);
        }

        //--------------------------------
        // 광기 초기화
        //--------------------------------

        Olaf olaf = action.Owner as Olaf;

        if (olaf != null && olaf.Passive != null)
        {
            olaf.Passive.ResetMadness();
        }
    }
}