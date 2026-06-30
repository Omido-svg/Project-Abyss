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

    private void ResolveQueue(Queue<ActionSlot> queue)
    {
        while (queue.Count > 0)
        {
            BattleAction action = queue.Dequeue();

            //------------------------------------
            // 방어
            //------------------------------------

            if (action == null)
                continue;

            if (action.Owner == null || action.Target == null)
                continue;

            if (action.Skill == null)
            {
                Debug.LogWarning("Skill NULL");
                continue;
            }

            if (action.Owner.IsDead)
                continue;

            if (action.Target.IsDead)
                continue;

            //------------------------------------
            // Action Start
            //------------------------------------

            battleContext._battleEvent.RaiseActionStart(action);

            //------------------------------------
            // 실행
            //------------------------------------

            action.Skill.Execute(action);

            //------------------------------------
            // 로그
            //------------------------------------

            BattleLogType type = action.Phase switch
            {
                ActionPhase.PRETURN  => BattleLogType.Prestige,
                ActionPhase.FORESIGHT => BattleLogType.Preparation,
                _                     => BattleLogType.Normal
            };

            battleContext.battleManager.BattleLogger.LogAction(
                action,
                type);

            //------------------------------------
            // Action End
            //------------------------------------

            battleContext._battleEvent.RaiseActionEnd(action);
        }
    }
}