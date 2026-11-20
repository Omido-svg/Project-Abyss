using UnityEngine;

/// <summary>
/// 11-20 「빛 최대치 −N」 — 걸려 있는 동안 대상의 빛 최대치를 N 낮춘다(현재 빛은 새 최대치로 깎인다).
/// 풀리면 최대치를 되돌린다(현재 빛은 채우지 않는다). 머리 파괴(BrokenHead)와 같은 방식.
/// 명성 「퇴로 차단」 1굴림 승리 — 상대 다음 턴 빛 최대치 −N(18.4). 다음 턴 시작에 1턴짜리로 건다.
/// </summary>
public sealed class EnergyCapDownStatus : NumericTimedStatus
{
    private int appliedPenalty;

    public EnergyCapDownStatus(int value, int duration = 1)
        : base("빛 최대치 감소", value, duration)
    {
    }

    public override void OnApply()
    {
        if (Owner == null || appliedPenalty > 0 || Stack <= 0)
            return;

        appliedPenalty = Mathf.Min(Stack, Owner.MaxEnergy);
        if (appliedPenalty > 0)
            Owner.AdjustEnergyMaximum(-appliedPenalty, fillToMaximum: false);
    }

    public override void OnRemove()
    {
        if (Owner == null || appliedPenalty <= 0)
            return;

        Owner.AdjustEnergyMaximum(+appliedPenalty, fillToMaximum: false);
        appliedPenalty = 0;
    }
}
