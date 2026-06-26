using System.Collections.Generic;
using UnityEngine;

public class BattleLogger
{
    private readonly List<BattleLogEntry> logs = new();

    // =========================================
    // LOG CONTROL
    // =========================================

    public void Clear()
    {
        logs.Clear();
    }

    public void Add(BattleLogEntry entry)
    {
        if (entry == null)
        {
            Debug.LogWarning("BattleLogEntry is NULL");
            return;
        }

        logs.Add(entry);
    }

    // =========================================
    // PRINT
    // =========================================

    public void PrintTurn()
    {
        if (logs.Count == 0)
        {
            Debug.Log("==================================");
            Debug.Log("TURN RESULT (EMPTY)");
            Debug.Log("==================================");
            return;
        }

        Debug.Log("==================================");
        Debug.Log("TURN RESULT");
        Debug.Log("==================================");

        foreach (BattleLogEntry log in logs)
        {
            if (log == null)
                continue;

            Debug.Log(log.ToString());
        }

        Debug.Log("==================================");

        // 🔥 턴 출력 후 초기화 (중요)
        Clear();
    }

    // =========================================
    // DEBUG / EXTENSION
    // =========================================

    public IReadOnlyList<BattleLogEntry> GetLogs()
    {
        return logs;
    }
}