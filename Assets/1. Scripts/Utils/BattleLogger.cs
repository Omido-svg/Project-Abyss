using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class BattleLogger
{
    private readonly List<BattleLogEntry> logs = new();

    public IReadOnlyList<BattleLogEntry> Logs => logs;

    //------------------------------------------------

    public void Clear()
    {
        logs.Clear();
    }

    //------------------------------------------------
    // PRETURN / FORESIGHT / 단순 행동 로그
    //------------------------------------------------

    public void LogAction(
        BattleAction action,
        BattleLogType type)
    {
        logs.Add(
            BattleLogEntry.Create(action)
                .SetType(type)
                .Build()
        );
    }

    //------------------------------------------------
    // 일방공격
    //------------------------------------------------

    public void LogOneSide(
        BattleAction action,
        int damage,
        int beforeHP,
        int afterHP)
    {
        logs.Add(
            BattleLogEntry.Create(action)
                .SetType(BattleLogType.Normal)
                .SetDamage(damage, beforeHP, afterHP)
                .SetBroken(action != null &&
                           action.TargetPart != null &&
                           action.TargetPart.IsBroken)
                .SetDead(action != null &&
                         action.Target != null &&
                         action.Target.IsDead)
                .Build()
        );
    }

    //------------------------------------------------
    // 일반 데미지
    //------------------------------------------------

    public void LogDamage(
        BattleAction action,
        BattleLogType type,
        int damage,
        int beforeHP,
        int afterHP)
    {
        logs.Add(
            BattleLogEntry.Create(action)
                .SetType(type)
                .SetDamage(damage, beforeHP, afterHP)
                .SetBroken(action != null &&
                        action.TargetPart != null &&
                        action.TargetPart.IsBroken)
                .SetDead(action != null &&
                        action.Target != null &&
                        action.Target.IsDead)
                .Build()
        );
    }

    //------------------------------------------------
    // 합 결과
    //------------------------------------------------

    public void LogClashResult(
        BattleAction action,
        bool isWinner,
        int myPower,
        int enemyPower,
        int damage = 0,
        int prestigeGain = 0,
        int beforeHP = 0,
        int afterHP = 0)
    {
        BattleLogBuilder builder =
            BattleLogEntry.Create(action)
                .SetType(BattleLogType.Clash)
                .SetClash(myPower, enemyPower)
                .SetWinner(isWinner);

        if (isWinner)
        {
            builder
                .SetDamage(damage, beforeHP, afterHP)
                .SetPrestige(prestigeGain)
                .SetBroken(action != null &&
                           action.TargetPart != null &&
                           action.TargetPart.IsBroken)
                .SetDead(action != null &&
                         action.Target != null &&
                         action.Target.IsDead);
        }

        logs.Add(builder.Build());
    }

    //------------------------------------------------

    public void PrintTurn(int turn)
    {
        StringBuilder sb = new();

        sb.AppendLine("==================================");
        sb.AppendLine($"TURN {turn} RESULT");
        sb.AppendLine("==================================");

        foreach (BattleLogEntry log in logs)
        {
            if (log == null)
                continue;

            sb.AppendLine(log.ToString());
            sb.AppendLine("----------------------------------");
        }

        sb.AppendLine("==================================");

        Debug.Log(sb.ToString());

        logs.Clear();
    }
}