/// <summary>
/// 판정값 비교보다 우선하는 명시적 교환 승패 규칙.
/// 캐릭터 전용 구현이 결과만 요청하고 ClashManager는 구체 캐릭터를 알지 않는다.
/// </summary>
public enum ForcedRollJudgmentDirective
{
    None = 0,
    ForceWin = 1,
    ForceLoss = 2
}

public interface IForcedRollJudgmentRule
{
    bool TryGetForcedRollJudgment(
        BattleAction action,
        RollResult rollResult,
        out ForcedRollJudgmentDirective directive,
        out string reason);
}
