using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class BattleLogger
{
    private readonly List<BattleLogEntry> logs = new();

    public void Clear()
    {
        logs.Clear();
    }

    public IReadOnlyList<BattleLogEntry> Logs => logs;

    //----------------------------------------
    // 일반 행동
    //----------------------------------------

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

    //----------------------------------------
    // 데미지
    //----------------------------------------

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

    //----------------------------------------
    // 합
    //----------------------------------------

    public void LogClash(
        BattleAction winner,
        BattleAction loser,
        int winnerPower,
        int loserPower,
        int damage,
        int prestigeGain,
        int beforeHP,
        int afterHP)
    {
        logs.Add(

            BattleLogEntry.Create(winner)
                .SetClash(winnerPower, loserPower)
                .SetDamage(damage, beforeHP, afterHP)
                .SetPrestige(prestigeGain)
                .SetBroken(winner.TargetPart.IsBroken)
                .SetDead(winner.Target.IsDead)
                .Build()

        );

        logs.Add(

            BattleLogEntry.Create(loser)
                .SetClash(loserPower, winnerPower)
                .Build()

        );
    }

    //----------------------------------------

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