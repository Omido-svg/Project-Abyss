using System.Collections.Generic;
using UnityEngine;

public class ActionResolver
{
    private readonly BattleContext battleContext;
    private readonly ClashManager clashManager;

    public ActionResolver(
        BattleContext battleContext,
        ClashManager clashManager)
    {
        this.battleContext = battleContext;
        this.clashManager = clashManager;
    }

    //------------------------------------------------

    public void Resolve(ActionExecutionQueue executionQueue)
    {
        ResolveQueue(executionQueue.PrestigeQueue);

        ResolveQueue(executionQueue.AmbushQueue);
        
        clashManager.Resolve(executionQueue.ClashQueue);
    }

    //------------------------------------------------

    private void ResolveQueue(Queue<BattleAction> queue)
    {
        while (queue.Count > 0)
        {
            BattleAction action = queue.Dequeue();

            battleContext._battleEvent.RaiseActionStart(action);

            if (action.Skill == null)
            {
                Debug.LogWarning("Skill is NULL");
                continue;
            }

            action.Skill.Execute(action);

            BattleLogType type = action.ActionType switch
            {
                ActionType.Preparation => BattleLogType.Preparation,
                ActionType.Prestige    => BattleLogType.Prestige,
                _                      => BattleLogType.Normal
            };

            battleContext.battleManager.BattleLogger.LogAction(
                action,
                type);

            battleContext._battleEvent.RaiseActionEnd(action);
        }
    }
}