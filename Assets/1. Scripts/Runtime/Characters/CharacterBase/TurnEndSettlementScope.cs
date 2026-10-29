using System;

/// <summary>
/// 1029 DMG-04: 턴 종료 정산 범위 표식.
///
/// 이 범위 안에서만 HP가 음수로 내려갈 수 있고, 사망 판정은 보류된다.
/// 정산이 끝나면 <see cref="Character.CommitTurnEndSettlement"/>가
/// 최종 HP를 정규화하고 사망 판정을 한 번만 수행한다.
/// </summary>
public readonly struct TurnEndSettlementScope : IDisposable
{
    private readonly Character owner;

    internal TurnEndSettlementScope(Character owner)
    {
        this.owner = owner;
    }

    public void Dispose()
    {
        owner?.EndTurnEndSettlement();
    }
}
