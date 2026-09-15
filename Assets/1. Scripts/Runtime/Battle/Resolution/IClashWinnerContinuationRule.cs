/// <summary>
/// 합의 실제 맞붙은 교환 다수결 승자가 확정된 직후,
/// 일방타격 구간으로 넘어가기 전에 패자의 남은 굴림 수를 수정하는 계약.
///
/// 정본 예외인 유진 낙일처럼 "합 승리" 자체가 남은 굴림을 지우는 규칙에 사용한다.
/// 개별 교환 승리에는 사용하지 않는다.
/// </summary>
public interface IClashWinnerContinuationRule
{
    int ModifyLoserRemainingRollCountAfterClash(
        BattleAction winnerAction,
        BattleAction loserAction,
        ClashResultContext result,
        int currentRemainingRollCount);
}
