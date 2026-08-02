public sealed class ExchangeRerollContext
{
    public BattleAction Action { get; }
    public BattleAction OpponentAction { get; }
    public int ExchangeIndex { get; }
    public int RerollIndex { get; }

    public int CurrentPower =>
        Action?.ClashPower ?? 0;

    public int OpponentPower =>
        OpponentAction?.ClashPower ?? 0;

    public bool IsWinning =>
        Action != null &&
        OpponentAction != null &&
        CurrentPower > OpponentPower;

    public bool IsTied =>
        Action != null &&
        OpponentAction != null &&
        CurrentPower == OpponentPower;

    public bool IsCritical =>
        Action?.LastRollResult?.IsCritical == true;

    public ExchangeRerollContext(
        BattleAction action,
        BattleAction opponentAction,
        int exchangeIndex,
        int rerollIndex)
    {
        Action = action;
        OpponentAction = opponentAction;
        ExchangeIndex = exchangeIndex;
        RerollIndex = rerollIndex;
    }
}
