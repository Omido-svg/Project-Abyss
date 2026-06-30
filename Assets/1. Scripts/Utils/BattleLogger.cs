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
    // PRETURN / FORESIGHT
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
                .SetBroken(action.TargetPart.IsBroken)
                .SetDead(action.Target.IsDead)
                .Build()

        );
    }

    //------------------------------------------------
    // 일반 데미지
    //------------------------------------------------

    public void LogDamage(
        BattleAction action,
        int damage,
        int beforeHP,
        int afterHP)
    {
        logs.Add(

            BattleLogEntry.Create(action)
                .SetType(BattleLogType.Normal)
                .SetDamage(damage, beforeHP, afterHP)
                .SetBroken(action.TargetPart.IsBroken)
                .SetDead(action.Target.IsDead)
                .Build()

        );
    }

    //------------------------------------------------
    // 합 결과 (승/패 각각 한 번씩 호출)
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
                .SetClash(myPower, enemyPower);

        if (isWinner)
        {
            builder
                .SetDamage(damage, beforeHP, afterHP)
                .SetPrestige(prestigeGain)
                .SetBroken(action.TargetPart.IsBroken)
                .SetDead(action.Target.IsDead);
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
            sb.AppendLine(log.ToString());
            sb.AppendLine("----------------------------------");
        }

        sb.AppendLine("==================================");

        Debug.Log(sb.ToString());

        logs.Clear();
    }
}