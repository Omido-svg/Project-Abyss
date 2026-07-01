public class OlafDuelSkill : DuelSkill
{
    public OlafDuelSkill()
    {
        SkillName = "광전사의 결투(결투)";

        BasePower = 8;

        Resolver = new CoinResolver(3);
    }

    public override int GetMomentumPushBonus(
        BattleAction action)
    {
        if (action == null)
            return 0;

        if (action.Owner == null)
            return 0;

        OlafMadnessMechanic madness =
            action.Owner.GetMechanic<OlafMadnessMechanic>();

        if (madness == null)
            return 0;

        return madness.GetDuelPushBonus();
    }

    public override void Execute(BattleAction action)
    {
        // 결투 승리 / 패배 효과는 OlafMadnessMechanic에서 처리한다.
        // - OnClashWin  : 출혈 부여 + 출혈 폭발 검사
        // - OnClashLose : 광기 +2
    }
}