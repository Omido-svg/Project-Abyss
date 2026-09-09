/// <summary>
/// 개별 교환 종료 뒤 합의 남은 굴림 진행을 수정하는 캐릭터/메커닉 능력 계약.
/// 공통 ClashManager는 특정 캐릭터나 무기 규칙을 직접 알지 않는다.
/// </summary>
public interface IExchangeContinuationRule
{
    int ModifyOpponentRemainingRollCount(
        BattleAction winnerAction,
        BattleAction opponentAction,
        int currentRemainingRollCount);
}
