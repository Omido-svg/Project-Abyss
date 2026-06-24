using System.Collections.Generic;

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

    public ActionExecutionQueue BuildExecutionQueue()
    {
        CalculateSpeed();
        SortBySpeed();
        BuildClashes();

        ActionExecutionQueue queue = new();

        // 위세
        foreach (BattleAction action in actions)
        {
            if (action.Phase == ActionPhase.PRETURN)
                queue.PrestigeQueue.Enqueue(action);
        }

        // 도사림
        foreach (BattleAction action in actions)
        {
            if (action.Phase == ActionPhase.FORESIGHT)
                queue.AmbushQueue.Enqueue(action);
        }

        // 합
        foreach (ClashPair pair in clashPairs)
        {
            if (pair.First.Phase == ActionPhase.COMBAT)
            {
                queue.ClashQueue.Enqueue(pair);
            }
        }

        return queue;
    }

    //------------------------------------------------

    private void CalculateSpeed()
    {
        foreach (BattleAction action in actions)
        {
            action.CalculateSpeed();
        }
    }

    //------------------------------------------------

    private void SortBySpeed()
    {
        actions.Sort((a, b) => b.Speed.CompareTo(a.Speed));
    }

    //------------------------------------------------

    private void BuildClashes()
    {
        clashPairs.Clear();

        HashSet<BattleAction> matched = new();

        foreach (BattleAction action in actions)
        {
            if (matched.Contains(action))
                continue;

            // 위세 / 도사림은 Clash 대상 아님
            if (action.Phase != ActionPhase.COMBAT)
                continue;

            BattleAction opponent = FindOpponent(action, matched);

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
        HashSet<BattleAction> matched)
    {
        foreach (BattleAction other in actions)
        {
            if (matched.Contains(other))
                continue;

            if (other == action)
                continue;

            if (other.Phase != ActionPhase.COMBAT)
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