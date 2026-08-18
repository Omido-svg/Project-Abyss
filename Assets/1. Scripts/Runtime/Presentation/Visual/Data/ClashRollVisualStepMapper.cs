/// <summary>
/// Battle domain의 ClashExchangeResult를 Presentation 전용 표시 모델로 변환한다.
/// Domain result는 더 이상 ClashRollVisualStep을 알지 않는다.
/// </summary>
public static class ClashRollVisualStepMapper
{
    public static ClashRollVisualStep Create(
        ClashExchangeResult exchange,
        BattleAction perspectiveAction)
    {
        if (exchange == null)
            return default;

        bool swap =
            perspectiveAction != null &&
            perspectiveAction == exchange.SecondAction;

        ClashRollVisualStep step = swap
            ? new ClashRollVisualStep(
                exchange.ExchangeIndex,
                exchange.SecondClashPower,
                exchange.FirstClashPower,
                exchange.SecondRollResult,
                exchange.FirstRollResult,
                exchange.SecondRollResult?.SpeedModifier ?? 0,
                exchange.FirstRollResult?.SpeedModifier ?? 0,
                exchange.SecondRollResult?.MomentumModifier ?? 0,
                exchange.FirstRollResult?.MomentumModifier ?? 0,
                exchange.SecondRollResult?.IsCritical ?? false,
                exchange.FirstRollResult?.IsCritical ?? false)
            : new ClashRollVisualStep(
                exchange.ExchangeIndex,
                exchange.FirstClashPower,
                exchange.SecondClashPower,
                exchange.FirstRollResult,
                exchange.SecondRollResult,
                exchange.FirstRollResult?.SpeedModifier ?? 0,
                exchange.SecondRollResult?.SpeedModifier ?? 0,
                exchange.FirstRollResult?.MomentumModifier ?? 0,
                exchange.SecondRollResult?.MomentumModifier ?? 0,
                exchange.FirstRollResult?.IsCritical ?? false,
                exchange.SecondRollResult?.IsCritical ?? false);

        step.IsOneSided = exchange.IsOneSided;
        step.WasCancelled = exchange.WasCancelled;
        step.AttackerDamage = swap
            ? exchange.WinnerAction == exchange.SecondAction
                ? exchange.Damage
                : 0
            : exchange.WinnerAction == exchange.FirstAction
                ? exchange.Damage
                : 0;
        step.TargetDamage = swap
            ? exchange.WinnerAction == exchange.FirstAction
                ? exchange.Damage
                : 0
            : exchange.WinnerAction == exchange.SecondAction
                ? exchange.Damage
                : 0;

        return step;
    }
}
