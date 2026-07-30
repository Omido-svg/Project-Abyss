using System;

[Serializable]
public sealed class BattleClashVisualExchange
{
    public int ExchangeIndex;
    public bool IsOneSided;
    public bool IsTie;
    public bool WasCancelled;

    // 실제 전투 계산 시점의 교환별 기세 스냅샷이다.
    // 전투 로직은 선계산되지만 UI는 이 값을 사용해 각 굴림 직후 순서대로 재생한다.
    public int MomentumBefore;
    public int MomentumAfter;
    public int MomentumShift;

    // 합 전체의 FirstAction 관점으로 고정된 표시 데이터다.
    // 승자가 바뀌어도 양쪽 숫자와 라벨 위치가 뒤집히지 않는다.
    public ClashRollVisualStep DisplayStep;

    // 승패 전환 모션은 실제 피해 발생 여부와 무관하게 필요하므로
    // 판정 결과의 Winner/Loser Action을 별도로 보존한다.
    public BattleAction WinnerAction;
    public BattleAction LoserAction;

    // 피해가 발생하는 교환만 존재한다.
    // 수비 승리, 동률, 취소처럼 피해 없는 교환에서는 null이다.
    public BattleVisualRequest AttackRequest;

    public bool HasResolvedWinner =>
        !WasCancelled &&
        !IsTie &&
        WinnerAction != null &&
        LoserAction != null;

    public bool HasAttack =>
        !WasCancelled &&
        AttackRequest != null &&
        AttackRequest.SourceAction != null;
}