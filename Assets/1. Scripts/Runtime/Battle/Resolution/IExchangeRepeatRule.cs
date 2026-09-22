/// <summary>
/// 0922 룰브레이커가 한 교환 종료 뒤 양쪽의 다음 교환 존재 여부를 조정하는 계약.
/// 기존 IExchangeContinuationRule(승자가 상대 남은 굴림을 삭제)과 분리하여
/// 유진 낙일 계약을 바꾸지 않고 올라프 「명예로운 전투」 반복을 표현한다.
/// </summary>
public interface IExchangeRepeatRule
{
    void ModifyRemainingRollCountsAfterExchange(
        ClashExchangeResult exchange,
        ref int firstRemaining,
        ref int secondRemaining);
}
