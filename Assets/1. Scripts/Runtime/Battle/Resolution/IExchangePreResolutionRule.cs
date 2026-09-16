/// <summary>
/// 두 굴림이 나온 뒤 승패/피해를 확정하기 직전에 교환 자체를 취소할 수 있는 공통 계약.
/// 유진의 결투 전용 "패 보기 → 접기"처럼 승패와 피해가 모두 없어지는 규칙에 사용한다.
/// UI 선택 방식은 이 계층이 소유하지 않는다.
/// </summary>
public interface IExchangePreResolutionRule
{
    bool TryCancelPairedExchange(
        BattleAction ownAction,
        BattleAction opponentAction,
        int exchangeIndex,
        RollResult opponentRoll,
        out string reason);
}
