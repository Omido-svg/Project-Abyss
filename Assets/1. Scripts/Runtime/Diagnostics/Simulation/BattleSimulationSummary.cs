using System;
using System.Collections.Generic;
using System.Text;

public enum BattleSimulationMode
{
    WinRate,
    Damage
}

[Serializable]
public sealed class BattleSimulationRunResult
{
    public int runIndex;
    public string outcome;
    public bool timedOut;
    public bool failed;
    public string failureMessage;

    public int turns;
    public int playerDamageDealt;
    public int playerDamageTaken;
    public int playerActions;
    public int enemyActions;
    public int clashes;
    public int playerRemainingHp;
    public int playerMaxHp;
}

[Serializable]
public sealed class BattleSimulationSummary
{
    public string schemaVersion = "1.0.0";
    public string mode;
    public string scene;
    public string startedUtc;
    public string completedUtc;
    public int requestedRuns;
    public int completedRuns;
    public int wins;
    public int losses;
    public int draws;
    public int failures;

    public float winRate;
    public float averageTurns;
    public float averagePlayerDamageDealt;
    public float averagePlayerDamageTaken;
    public float averagePlayerDamagePerTurn;
    public float averageEnemyDamagePerTurn;
    public float averageDamagePerPlayerAction;
    public float averageDamagePerEnemyAction;

    public List<BattleSimulationRunResult> runs = new();

    public void Recalculate()
    {
        completedRuns = runs?.Count ?? 0;
        wins = 0;
        losses = 0;
        draws = 0;
        failures = 0;

        long turns = 0;
        long dealt = 0;
        long taken = 0;
        long playerActions = 0;
        long enemyActions = 0;

        if (runs != null)
        {
            foreach (BattleSimulationRunResult run in runs)
            {
                if (run == null)
                    continue;

                switch (run.outcome)
                {
                    case "PLAYER_VICTORY":
                        wins++;
                        break;
                    case "PLAYER_DEFEAT":
                        losses++;
                        break;
                    default:
                        draws++;
                        break;
                }

                if (run.failed)
                    failures++;

                turns += run.turns;
                dealt += run.playerDamageDealt;
                taken += run.playerDamageTaken;
                playerActions += run.playerActions;
                enemyActions += run.enemyActions;
            }
        }

        int validRuns = Math.Max(1, completedRuns);
        winRate = 100f * wins / validRuns;
        averageTurns = (float)turns / validRuns;
        averagePlayerDamageDealt = (float)dealt / validRuns;
        averagePlayerDamageTaken = (float)taken / validRuns;
        averagePlayerDamagePerTurn = turns <= 0
            ? 0f
            : (float)dealt / turns;
        averageEnemyDamagePerTurn = turns <= 0
            ? 0f
            : (float)taken / turns;
        averageDamagePerPlayerAction = playerActions <= 0
            ? 0f
            : (float)dealt / playerActions;
        averageDamagePerEnemyAction = enemyActions <= 0
            ? 0f
            : (float)taken / enemyActions;
    }

    public string ToDisplayString()
    {
        Recalculate();

        StringBuilder builder = new();
        builder.AppendLine($"분석 완료: {completedRuns}/{requestedRuns}회");
        builder.AppendLine(
            $"승 {wins} / 패 {losses} / 무·타임아웃 {draws}");
        builder.AppendLine($"승률: {winRate:0.0}%");
        builder.AppendLine($"평균 턴: {averageTurns:0.00}");
        builder.AppendLine(
            $"플레이어 평균 가한 피해: {averagePlayerDamageDealt:0.00}");
        builder.AppendLine(
            $"플레이어 평균 받은 피해: {averagePlayerDamageTaken:0.00}");
        builder.AppendLine(
            $"턴당 가한/받은 피해: " +
            $"{averagePlayerDamagePerTurn:0.00} / " +
            $"{averageEnemyDamagePerTurn:0.00}");
        builder.AppendLine(
            $"행동당 가한/받은 피해: " +
            $"{averageDamagePerPlayerAction:0.00} / " +
            $"{averageDamagePerEnemyAction:0.00}");

        if (failures > 0)
            builder.AppendLine($"실패한 실행: {failures}");

        return builder.ToString();
    }
}
