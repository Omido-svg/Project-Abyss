using UnityEngine;

public enum ClashJudgmentOutcome
{
    Tie = 0,
    FirstWins = 1,
    SecondWins = 2
}

public readonly struct ClashJudgmentResult
{
    public ClashJudgmentOutcome Outcome { get; }
    public BattleAction Winner { get; }
    public BattleAction Loser { get; }

    public bool IsTie =>
        Outcome == ClashJudgmentOutcome.Tie;

    public ClashJudgmentResult(
        ClashJudgmentOutcome outcome,
        BattleAction winner,
        BattleAction loser)
    {
        Outcome = outcome;
        Winner = winner;
        Loser = loser;
    }
}

/// <summary>
/// 표준 합 파이프라인의 첫 두 단계.
///
/// 1. 굴림
/// 2. 합 판정값 완성 및 비교
///
/// 기세는 합 수치에 더하지 않으며,
/// 순수 굴림값과 합 판정 보정값을 분리해서 유지합니다.
/// </summary>
public sealed class ClashPowerPipeline
{
    private readonly ClashRuleSettings settings;

    public ClashPowerPipeline(
        ClashRuleSettings settings)
    {
        this.settings =
            settings ??
            new ClashRuleSettings();

        this.settings.Normalize();
    }

    public void RollForClash(
        BattleAction action,
        BattleAction opponent,
        int exchangeIndex)
    {
        if (action == null)
            return;

        action.RollPowerForExchange(
            exchangeIndex);

        int speedModifier =
            CalculateSpeedModifier(
                action,
                opponent);

        int preparationModifier =
            action.Owner?.TurnClashPowerBonus ?? 0;

        action.ApplyClashModifiers(
            speedModifier,
            momentumModifier: 0,
            preparationModifier);
    }

    public void RollOneSided(
        BattleAction action,
        int exchangeIndex)
    {
        if (action == null)
            return;

        action.RollPowerForExchange(
            exchangeIndex);

        // 일방 공격은 비교 대상이 없으므로 합 전용 보정을 제거합니다.
        action.ClearClashModifiers();
    }

    public ClashJudgmentResult Judge(
        BattleAction first,
        BattleAction second)
    {
        if (first == null || second == null)
        {
            return new ClashJudgmentResult(
                ClashJudgmentOutcome.Tie,
                null,
                null);
        }

        if (first.ClashPower == second.ClashPower)
        {
            return new ClashJudgmentResult(
                ClashJudgmentOutcome.Tie,
                null,
                null);
        }

        bool firstWins =
            first.ClashPower >
            second.ClashPower;

        return new ClashJudgmentResult(
            firstWins
                ? ClashJudgmentOutcome.FirstWins
                : ClashJudgmentOutcome.SecondWins,
            firstWins ? first : second,
            firstWins ? second : first);
    }

    private int CalculateSpeedModifier(
        BattleAction self,
        BattleAction opponent)
    {
        if (self == null ||
            opponent == null ||
            settings.SpeedWeight <= 0)
        {
            return 0;
        }

        int speedGap =
            Mathf.Max(
                0,
                self.Speed -
                opponent.Speed);

        // 속도 차이가 6 이상일 때만 합 수치 +1.
        return speedGap >= 6
            ? 1
            : 0;
    }
}
