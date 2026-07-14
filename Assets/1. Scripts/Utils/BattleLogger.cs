using System.Collections.Generic;
using System.Text;

public class BattleLogger
{
    private readonly List<string> logs =
        new();

    private readonly List<BattleLogEntry> entries =
        new();

    private int nextSequence = 1;

    public IReadOnlyList<string> Logs =>
        logs;

    public IReadOnlyList<BattleLogEntry> Entries =>
        entries;

    public void Clear()
    {
        logs.Clear();
        entries.Clear();
        nextSequence = 1;
    }

    public void LogAction(
        BattleAction action,
        BattleLogType type)
    {
        AddEntry(
            BattleLogEntry.Create(action)
                .SetType(type)
                .SetCategory(
                    BattleLogCategory.Combat)
                .Build());
    }

    public void LogOneSideResult(
        BattleAction action,
        int damage,
        int beforeHP,
        int afterHP,
        bool targetPartWasBrokenBeforeDamage =
            false)
    {
        if (action == null)
            return;

        BattleLogCategory category =
            BattleLogCategory.Combat;

        if (damage > 0)
            category |= BattleLogCategory.Damage;

        AddEntry(
            BattleLogEntry.Create(action)
                .SetType(
                    ResolveType(action))
                .SetCategory(category)
                .SetDamage(
                    damage,
                    beforeHP,
                    afterHP)
                .SetTargetPartWasBrokenBeforeDamage(
                    targetPartWasBrokenBeforeDamage)
                .SetBroken(
                    action.TargetPart != null &&
                    action.TargetPart.IsBroken)
                .SetDead(
                    action.Target != null &&
                    action.Target.IsDead)
                .Build());
    }

    public void LogDamage(
        BattleAction action,
        BattleLogType type,
        int damage,
        int beforeHP,
        int afterHP)
    {
        AddEntry(
            BattleLogEntry.Create(action)
                .SetType(type)
                .SetCategory(
                    BattleLogCategory.Damage)
                .SetDamage(
                    damage,
                    beforeHP,
                    afterHP)
                .SetBroken(
                    action != null &&
                    action.TargetPart != null &&
                    action.TargetPart.IsBroken)
                .SetDead(
                    action != null &&
                    action.Target != null &&
                    action.Target.IsDead)
                .Build());
    }

    public void LogClashResult(
        BattleAction action,
        bool isWin,
        int myClash,
        int enemyClash,
        int damage = 0,
        int prestigeGain = 0,
        int beforeHP = 0,
        int afterHP = 0,
        bool targetPartWasBrokenBeforeDamage =
            false)
    {
        if (action == null)
            return;

        BattleLogCategory category =
            BattleLogCategory.Combat;

        if (damage > 0)
            category |= BattleLogCategory.Damage;

        AddEntry(
            BattleLogEntry.Create(action)
                .SetClash(
                    myClash,
                    enemyClash)
                .SetCategory(category)
                .SetWinner(isWin)
                .SetDamage(
                    damage,
                    beforeHP,
                    afterHP)
                .SetPrestige(
                    prestigeGain)
                .SetTargetPartWasBrokenBeforeDamage(
                    targetPartWasBrokenBeforeDamage)
                .SetBroken(
                    action.TargetPart != null &&
                    action.TargetPart.IsBroken)
                .SetDead(
                    action.Target != null &&
                    action.Target.IsDead)
                .Build());
    }

    public List<BattleLogEntry> GetEntries(
        BattleLogCategory categories,
        BattleLogLevel minimumLevel =
            BattleLogLevel.Trace)
    {
        List<BattleLogEntry> result =
            new();

        if (categories == BattleLogCategory.None)
            categories = BattleLogCategory.All;

        foreach (BattleLogEntry entry in entries)
        {
            if (entry == null)
                continue;

            if (entry.Level < minimumLevel)
                continue;

            if ((entry.Category & categories) == 0)
                continue;

            result.Add(entry);
        }

        return result;
    }

    public void PrintTurn(int turn)
    {
        StringBuilder builder =
            new();

        builder.AppendLine(
            "==================================");

        builder.AppendLine(
            $"TURN {turn} RESULT");

        builder.AppendLine(
            "==================================");

        BattleLogCategory visibleCategories =
            BattleLogCategory.None;

        int visibleCount = 0;

        foreach (BattleLogEntry entry in entries)
        {
            if (entry == null)
                continue;

            if (entry.TurnNumber <= 0)
                entry.TurnNumber = turn;

            if (!BattleDebugLog.IsEnabled(
                    entry.Category,
                    entry.Level))
            {
                continue;
            }

            visibleCategories |=
                entry.Category;

            builder.AppendLine(
                entry.ToString());

            visibleCount++;
        }

        builder.AppendLine(
            "==================================");

        if (visibleCount > 0)
        {
            BattleDebugLog.Log(
                visibleCategories,
                builder.ToString(),
                BattleLogLevel.Info);
        }

        Clear();
    }

    private void AddEntry(
        BattleLogEntry entry)
    {
        if (entry == null)
            return;

        entry.Sequence =
            nextSequence++;

        entry.CaptureActionSnapshot();

        entries.Add(entry);
        logs.Add(entry.ToString());
    }

    private static BattleLogType ResolveType(
        BattleAction action)
    {
        if (action == null)
            return BattleLogType.Normal;

        switch (action.ActionType)
        {
            case ActionType.Preparation:
                return BattleLogType.Preparation;

            case ActionType.Prestige:
                return BattleLogType.Prestige;

            default:
                return BattleLogType.Normal;
        }
    }
}
