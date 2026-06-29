using System.Collections.Generic;
using System.Linq;

public class ClashPair
{
    public BattleAction First;
    public BattleAction Second;

    public bool IsClash => Second != null;

    public ClashPair(BattleAction first, BattleAction second = null)
    {
        First = first;
        Second = second;
    }
}

public class ActionExecutionQueue
{
    public Queue<BattleAction> PrestigeQueue = new();
    public Queue<BattleAction> AmbushQueue = new();
    public Queue<ClashPair> ClashQueue = new();
}

public class ActionManager
{
    private readonly BattleContext battleContext;

    private readonly List<BattleAction> actions = new();
    private readonly List<ClashPair> clashPairs = new();

    public IReadOnlyList<BattleAction> Actions => actions;
    public IReadOnlyList<ClashPair> ClashPairs => clashPairs;

    public ActionManager(BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }

    //------------------------------------------------

    public void Clear()
    {
        actions.Clear();
        clashPairs.Clear();
    }

    //------------------------------------------------

    public void AddAction(BattleAction action)
    {
        actions.Add(action);
    }

    //------------------------------------------------
    // 핵심 수정: 안정적인 Execution Pipeline
    //------------------------------------------------

    public ActionExecutionQueue BuildExecutionQueue()
    {
        clashPairs.Clear();

        // 1. Phase 기준 그룹화 (핵심)
        var groupedByPhase = actions
            .GroupBy(a => a.Phase)
            .ToDictionary(g => g.Key, g => g.ToList());

        ActionExecutionQueue queue = new();

        //------------------------------------------------
        // 2. PRETURN (위세)
        //------------------------------------------------
        if (groupedByPhase.TryGetValue(ActionPhase.PRETURN, out var preturnActions))
        {
            foreach (var action in SortBySpeed(preturnActions))
            {
                queue.PrestigeQueue.Enqueue(action);
            }
        }

        //------------------------------------------------
        // 3. FORESIGHT (도사림)
        //------------------------------------------------
        if (groupedByPhase.TryGetValue(ActionPhase.FORESIGHT, out var foresightActions))
        {
            foreach (var action in SortBySpeed(foresightActions))
            {
                queue.AmbushQueue.Enqueue(action);
            }
        }

        //------------------------------------------------
        // 4. COMBAT (Clash 포함)
        //------------------------------------------------
        if (groupedByPhase.TryGetValue(ActionPhase.COMBAT, out var combatActions))
        {
            // Speed 정렬된 COMBAT 리스트
            var sortedCombat = SortBySpeed(combatActions);

            BuildClashes(sortedCombat);

            foreach (var pair in clashPairs)
            {
                queue.ClashQueue.Enqueue(pair);
            }
        }

        return queue;
    }

    //------------------------------------------------
    // Speed 정렬 (재사용용)
    //------------------------------------------------

    private List<BattleAction> SortBySpeed(List<BattleAction> list)
    {
        return list
            .OrderByDescending(a => a.Speed)
            .ToList();
    }

    //------------------------------------------------
    // Clash 생성 (COMBAT 전용)
    //------------------------------------------------

    private void BuildClashes(List<BattleAction> combatActions)
    {
        clashPairs.Clear();

        HashSet<BattleAction> matched = new();

        foreach (var action in combatActions)
        {
            if (matched.Contains(action))
                continue;

            var opponent = FindOpponent(action, combatActions, matched);

            if (opponent != null)
            {
                clashPairs.Add(new ClashPair(action, opponent));
                matched.Add(action);
                matched.Add(opponent);
            }
            else
            {
                clashPairs.Add(new ClashPair(action));
                matched.Add(action);
            }
        }
    }

    //------------------------------------------------

    private BattleAction FindOpponent(
        BattleAction action,
        List<BattleAction> combatActions,
        HashSet<BattleAction> matched)
    {
        foreach (var other in combatActions)
        {
            if (matched.Contains(other))
                continue;

            if (other == action)
                continue;

            if (other.Owner == action.Target &&
                other.Target == action.Owner)
            {
                return other;
            }
        }

        return null;
    }
}