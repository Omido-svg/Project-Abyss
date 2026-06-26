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

    private void ResolveQueue(
        Queue<BattleAction> queue)
    {
        while (queue.Count > 0)
        {
            BattleAction action = queue.Dequeue();
            
            battleContext._battleEvent.RaiseActionStart(action);

            // 스킬 실행
            if (action.Skill == null) {
                Debug.Log("스킬이 NULL임");
                continue;
            }
            action.Skill.Execute(action);

            battleContext._battleEvent.RaiseActionEnd(action);
        }
    }
}