public enum YujinPeekDecision
{
    Continue = 0,
    Fold = 1
}

public readonly struct YujinPeekDecisionContext
{
    public readonly Yujin Owner;
    public readonly BattleAction OwnAction;
    public readonly BattleAction OpponentAction;
    public readonly int ExchangeIndex;
    public readonly RollResult OpponentRoll;

    public YujinPeekDecisionContext(
        Yujin owner,
        BattleAction ownAction,
        BattleAction opponentAction,
        int exchangeIndex,
        RollResult opponentRoll)
    {
        Owner = owner;
        OwnAction = ownAction;
        OpponentAction = opponentAction;
        ExchangeIndex = exchangeIndex;
        OpponentRoll = opponentRoll;
    }
}

/// <summary>
/// 0916 살수의 감 "패 보기" 런타임 선택 hook.
/// 실제 UI 흐름은 (미정)이므로 Phase D는 provider 계약만 제공한다.
/// provider가 없으면 항상 Continue다.
/// </summary>
public interface IYujinPeekDecisionProvider
{
    YujinPeekDecision Decide(
        YujinPeekDecisionContext context);
}
