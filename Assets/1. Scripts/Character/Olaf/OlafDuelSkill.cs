public class OlafDuelSkill : DuelSkill
{
    public OlafDuelSkill()
    {
        SkillName = "광전사의 결투(결투)";

        BasePower = 8;

        Resolver = new CoinResolver(3);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Target == null)
            return;

        // 결투 자체 데미지는 DamageManager에서 처리
        // 합 승리 시 위세 획득도 ClashManager / MomentumManager에서 처리

        // 결투 성공 후 추가 효과를 넣고 싶으면 여기
        // 예: 출혈 1 추가
        action.Target.AddStatus(
            new Bleeding(1),
            action.Owner);
    }
}