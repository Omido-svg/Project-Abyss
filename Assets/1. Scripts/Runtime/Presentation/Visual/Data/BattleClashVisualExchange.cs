using System;

[Serializable]
public sealed class BattleClashVisualExchange
{
    public int ExchangeIndex;
    public bool IsOneSided;
    public bool IsTie;
    public bool WasCancelled;

    // 합 전체의 FirstAction 관점으로 고정된 표시 데이터다.
    // 승자가 바뀌어도 양쪽 숫자와 라벨 위치가 뒤집히지 않는다.
    public ClashRollVisualStep DisplayStep;

    // 피해가 발생하는 교환만 존재한다.
    // 동률, 취소, 피해 없는 교환에서는 null이다.
    public BattleVisualRequest AttackRequest;

    public bool HasAttack =>
        !WasCancelled &&
        AttackRequest != null &&
        AttackRequest.SourceAction != null;
}
