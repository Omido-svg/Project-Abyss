using System.Collections.Generic;

public class ClashPair
{
    // 속도가 더 빠른 Action
    public BattleAction First;

    // 합이 없으면 null
    public BattleAction Second;

    public bool IsClash => Second != null;

    public ClashPair(BattleAction first, BattleAction second = null)
    {
        First = first;
        Second = second;
    }
}

public class ActionManager
{
    private readonly BattleContext battleContext;

    // 이번 턴 생성된 모든 Action
    private readonly List<BattleAction> actions = new();

    // 이번 턴 생성된 ClashPair
    private readonly List<ClashPair> clashPairs = new();

    public IReadOnlyList<BattleAction> Actions => actions;
    public IReadOnlyList<ClashPair> ClashPairs => clashPairs;

    //----------------------------------------------------

    public ActionManager(BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }

    //----------------------------------------------------

    public void Clear()
    {
        actions.Clear();
        clashPairs.Clear();
    }

    //----------------------------------------------------

    public void AddAction(BattleAction action)
    {
        actions.Add(action);
    }

    //----------------------------------------------------

    /// 이번 턴 실행목록 생성
    public void BuildTurnActions()
    {
        CalculateSpeed();

        SortBySpeed();

        MatchClashes();
    }

    //----------------------------------------------------

    private void CalculateSpeed()
    {
        foreach (BattleAction action in actions)
        {
            action.CalculateSpeed();
        }
    }

    //----------------------------------------------------

    private void SortBySpeed()
    {
        actions.Sort((a, b) => b.Speed.CompareTo(a.Speed));
    }

    //----------------------------------------------------

    private void MatchClashes()
    {
        clashPairs.Clear();

        HashSet<BattleAction> matched = new();

        foreach (BattleAction action in actions)
        {
            if (matched.Contains(action))
                continue;

            BattleAction opponent = FindFastestOpponent(action, matched);

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

    //----------------------------------------------------

    private BattleAction FindFastestOpponent(
        BattleAction action,
        HashSet<BattleAction> matched)
    {
        foreach (BattleAction other in actions)
        {
            if (matched.Contains(other))
                continue;

            if (other == action)
                continue;

            // 서로를 공격하는가?
            if (other.Owner == action.Target &&
                other.Target == action.Owner)
            {
                return other;
            }
        }

        return null;
    }

    //----------------------------------------------------

    public Queue<ClashPair> BuildQueue()
    {
        BuildTurnActions();

        return new Queue<ClashPair>(clashPairs);
    }
}