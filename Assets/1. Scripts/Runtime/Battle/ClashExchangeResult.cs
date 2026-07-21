public sealed class ClashExchangeResult
{
    public int ExchangeIndex;

    public BattleAction FirstAction;
    public BattleAction SecondAction;

    public bool IsOneSided;
    public bool IsTie;
    public bool WasCancelled;

    public int FirstClashPower;
    public int SecondClashPower;

    public RollResult FirstRollResult;
    public RollResult SecondRollResult;

    public BattleAction WinnerAction;
    public BattleAction LoserAction;

    public DamageContext DamageContext;

    public int MomentumBefore;
    public int MomentumAfter;
    public int MomentumShift;
    public int PrestigeDealtGain;
    public int PrestigeTakenGain;

    public int Damage =>
        DamageContext?.GetDisplayDamage() ?? 0;

    public ClashRollVisualStep CreateVisualStep(
        BattleAction perspectiveAction)
    {
        bool swap =
            perspectiveAction != null &&
            perspectiveAction == SecondAction;

        ClashRollVisualStep step = swap
            ? new ClashRollVisualStep(
                ExchangeIndex,
                SecondClashPower,
                FirstClashPower,
                SecondRollResult,
                FirstRollResult,
                SecondRollResult?.SpeedModifier ?? 0,
                FirstRollResult?.SpeedModifier ?? 0,
                SecondRollResult?.MomentumModifier ?? 0,
                FirstRollResult?.MomentumModifier ?? 0,
                SecondRollResult?.IsCritical ?? false,
                FirstRollResult?.IsCritical ?? false)
            : new ClashRollVisualStep(
                ExchangeIndex,
                FirstClashPower,
                SecondClashPower,
                FirstRollResult,
                SecondRollResult,
                FirstRollResult?.SpeedModifier ?? 0,
                SecondRollResult?.SpeedModifier ?? 0,
                FirstRollResult?.MomentumModifier ?? 0,
                SecondRollResult?.MomentumModifier ?? 0,
                FirstRollResult?.IsCritical ?? false,
                SecondRollResult?.IsCritical ?? false);

        step.IsOneSided = IsOneSided;
        step.WasCancelled = WasCancelled;
        step.AttackerDamage = swap
            ? WinnerAction == SecondAction ? Damage : 0
            : WinnerAction == FirstAction ? Damage : 0;
        step.TargetDamage = swap
            ? WinnerAction == FirstAction ? Damage : 0
            : WinnerAction == SecondAction ? Damage : 0;

        return step;
    }
}