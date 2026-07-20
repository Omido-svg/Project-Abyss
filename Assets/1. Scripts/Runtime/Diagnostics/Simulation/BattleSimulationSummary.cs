using System;
using System.Collections.Generic;
using System.Text;

public enum BattleSimulationMode
{
    WinRate,
    Damage
}

[Serializable]
public sealed class BattleSimulationBatchEvent
{
    public string schemaVersion = "1.3.0";
    public long sequence;
    public string utcTimestamp;
    public string eventType;
    public string mode;
    public int runIndex;
    public int completedRuns;
    public int requestedRuns;
    public string message;
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

    // 기존 호환 필드: 화면에 표시되는 대표 피해량.
    public int playerDamageDealt;
    public int playerDamageTaken;

    // 실제 피해 경로별 수치.
    public int playerCharacterHpDamageDealt;
    public int playerCharacterHpDamageTaken;
    public int playerPartDamageDealt;
    public int playerPartDamageTaken;
    public int playerDirectHpDamageDealt;
    public int playerDirectHpDamageTaken;

    // 전투 시작/종료 HP로 계산한 순손실과 누적 HP 피해와의 차이.
    public int playerNetHpLoss;
    public int playerEstimatedHealingReceived;

    public int playerActions;
    public int enemyActions;
    public int playerEnergySpent;
    public int enemyEnergySpent;
    public int clashes;
    public int playerRemainingHp;
    public int playerMaxHp;
}

[Serializable]
public sealed class BattleSimulationSummary
{
    public string schemaVersion = "1.3.0";
    public string status;
    public string mode;
    public string scene;
    public string outputDirectory;
    public bool detailedTraceDuringBatch;
    public string startedUtc;
    public string completedUtc;
    public int requestedRuns;
    public int completedRuns;
    public int successfulRuns;
    public int wins;
    public int losses;
    public int draws;
    public int failures;

    public float winRate;
    public float averageTurns;

    // 기존 호환 지표: 표시 피해량.
    public float averagePlayerDamageDealt;
    public float averagePlayerDamageTaken;
    public float averagePlayerDamagePerTurn;
    public float averageEnemyDamagePerTurn;
    public float averageDamagePerPlayerAction;
    public float averageDamagePerEnemyAction;

    // 실제 피해 경로별 지표.
    public float averagePlayerCharacterHpDamageDealt;
    public float averagePlayerCharacterHpDamageTaken;
    public float averagePlayerPartDamageDealt;
    public float averagePlayerPartDamageTaken;
    public float averagePlayerNetHpLoss;
    public float averagePlayerEstimatedHealingReceived;

    public float averagePlayerEnergySpent;
    public float averageEnemyEnergySpent;

    public List<BattleSimulationRunResult> runs = new();

    public void Recalculate()
    {
        completedRuns = runs?.Count ?? 0;
        successfulRuns = 0;
        wins = 0;
        losses = 0;
        draws = 0;
        failures = 0;

        long turns = 0;
        long displayDealt = 0;
        long displayTaken = 0;
        long hpDealt = 0;
        long hpTaken = 0;
        long partDealt = 0;
        long partTaken = 0;
        long netHpLoss = 0;
        long estimatedHealing = 0;
        long playerActions = 0;
        long enemyActions = 0;
        long playerEnergy = 0;
        long enemyEnergy = 0;

        if (runs != null)
        {
            foreach (BattleSimulationRunResult run in runs)
            {
                if (run == null)
                    continue;

                if (run.failed)
                {
                    failures++;
                    continue;
                }

                successfulRuns++;

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

                turns += run.turns;
                displayDealt += run.playerDamageDealt;
                displayTaken += run.playerDamageTaken;
                hpDealt += run.playerCharacterHpDamageDealt;
                hpTaken += run.playerCharacterHpDamageTaken;
                partDealt += run.playerPartDamageDealt;
                partTaken += run.playerPartDamageTaken;
                netHpLoss += run.playerNetHpLoss;
                estimatedHealing += run.playerEstimatedHealingReceived;
                playerActions += run.playerActions;
                enemyActions += run.enemyActions;
                playerEnergy += run.playerEnergySpent;
                enemyEnergy += run.enemyEnergySpent;
            }
        }

        int validRuns = Math.Max(1, successfulRuns);
        winRate = 100f * wins / validRuns;
        averageTurns = (float)turns / validRuns;
        averagePlayerDamageDealt = (float)displayDealt / validRuns;
        averagePlayerDamageTaken = (float)displayTaken / validRuns;
        averagePlayerCharacterHpDamageDealt = (float)hpDealt / validRuns;
        averagePlayerCharacterHpDamageTaken = (float)hpTaken / validRuns;
        averagePlayerPartDamageDealt = (float)partDealt / validRuns;
        averagePlayerPartDamageTaken = (float)partTaken / validRuns;
        averagePlayerNetHpLoss = (float)netHpLoss / validRuns;
        averagePlayerEstimatedHealingReceived =
            (float)estimatedHealing / validRuns;

        averagePlayerDamagePerTurn = turns <= 0
            ? 0f
            : (float)displayDealt / turns;
        averageEnemyDamagePerTurn = turns <= 0
            ? 0f
            : (float)displayTaken / turns;
        averageDamagePerPlayerAction = playerActions <= 0
            ? 0f
            : (float)displayDealt / playerActions;
        averageDamagePerEnemyAction = enemyActions <= 0
            ? 0f
            : (float)displayTaken / enemyActions;
        averagePlayerEnergySpent =
            (float)playerEnergy / validRuns;
        averageEnemyEnergySpent =
            (float)enemyEnergy / validRuns;
    }

    public string ToDisplayString()
    {
        Recalculate();

        StringBuilder builder = new();
        string headline = status switch
        {
            "RUNNING" => "분석 진행",
            "COMPLETED" => "분석 완료",
            "STOPPED" => "분석 중단",
            "PLAY_MODE_STOPPED" => "Play Mode 종료로 분석 중단",
            _ => "분석 비정상 종료"
        };

        builder.AppendLine(
            $"{headline}: {completedRuns}/{requestedRuns}회 " +
            $"(유효 {successfulRuns} / 실패 {failures})");
        builder.AppendLine(
            $"승 {wins} / 패 {losses} / 무·타임아웃 {draws}");
        builder.AppendLine($"유효 실행 기준 승률: {winRate:0.0}%");
        builder.AppendLine($"평균 턴: {averageTurns:0.00}");
        builder.AppendLine(
            $"표시 피해 가함/받음: " +
            $"{averagePlayerDamageDealt:0.00} / " +
            $"{averagePlayerDamageTaken:0.00}");
        builder.AppendLine(
            $"실제 캐릭터 HP 피해 가함/받음: " +
            $"{averagePlayerCharacterHpDamageDealt:0.00} / " +
            $"{averagePlayerCharacterHpDamageTaken:0.00}");
        builder.AppendLine(
            $"부위 피해 가함/받음: " +
            $"{averagePlayerPartDamageDealt:0.00} / " +
            $"{averagePlayerPartDamageTaken:0.00}");
        builder.AppendLine(
            $"플레이어 순 HP 손실/추정 회복: " +
            $"{averagePlayerNetHpLoss:0.00} / " +
            $"{averagePlayerEstimatedHealingReceived:0.00}");
        builder.AppendLine(
            $"표시 피해 턴당 가함/받음: " +
            $"{averagePlayerDamagePerTurn:0.00} / " +
            $"{averageEnemyDamagePerTurn:0.00}");
        builder.AppendLine(
            $"표시 피해 행동당 가함/받음: " +
            $"{averageDamagePerPlayerAction:0.00} / " +
            $"{averageDamagePerEnemyAction:0.00}");
        builder.AppendLine(
            $"전투당 에너지 소비(플레이어/적): " +
            $"{averagePlayerEnergySpent:0.00} / " +
            $"{averageEnemyEnergySpent:0.00}");

        return builder.ToString();
    }
}
