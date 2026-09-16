/// <summary>
/// 굴림 결과 자체가 일반 판정값과 무관하게 확정 실패해야 하는 캐릭터 규칙.
/// 예: 히후미 친치로 1·2·3 대실패.
///
/// paired exchange에서는 이 결과를 확정 패배로 바꾸고,
/// one-sided exchange에서는 적중/피해 없이 실패로 처리한다.
/// 부가 효과(자해/자원/반격 적립)는 ExchangeResolved observer가 담당한다.
/// </summary>
public interface IForcedRollFailureRule
{
    bool IsForcedRollFailure(
        BattleAction action,
        RollResult rollResult,
        out string reason);
}
