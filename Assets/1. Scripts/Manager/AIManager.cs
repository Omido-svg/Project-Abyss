using System.Collections.Generic;

public class AIManager
{
    private readonly BattleContext battleContext;
    private readonly ActionManager actionManager;

    // 이번 턴 부위별 타겟팅 횟수
    private readonly Dictionary<BodyPart, int> targetCounter
        = new();

    //--------------------------------

    public AIManager(
        BattleContext battleContext,
        ActionManager actionManager)
    {
        this.battleContext = battleContext;
        this.actionManager = actionManager;
    }

    //--------------------------------

    public void BeginTurn()
    {
        targetCounter.Clear();
    }

    //--------------------------------

    public int GetTargetCount(BodyPart part)
    {
        if (!targetCounter.ContainsKey(part))
            return 0;

        return targetCounter[part];
    }

    //--------------------------------

    public void RegisterTarget(BodyPart part)
    {
        if (!targetCounter.ContainsKey(part))
            targetCounter.Add(part, 0);

        targetCounter[part]++;
    }

    //--------------------------------

    public void DecideEnemyActions()
    {
        foreach (Enemy enemy in battleContext.Enemies)
        {
            List<BattleAction> actions = enemy.DecideActions(battleContext);

            foreach (BattleAction action in actions)
            {
                actionManager.AddAction(action);
            }
        }
    }
}