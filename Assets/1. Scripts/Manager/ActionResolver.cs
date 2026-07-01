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
        if (executionQueue == null)
            return;

        ResolveQueue(executionQueue.PrestigeQueue);

        ResolveQueue(executionQueue.AmbushQueue);

        clashManager.Resolve(executionQueue.ClashQueue);
    }

    //------------------------------------------------

    private void ResolveQueue(Queue<ActionSlot> queue)
    {
        if (queue == null)
            return;

        while (queue.Count > 0)
        {
            ActionSlot slot = queue.Dequeue();

            if (!IsValidSlot(slot))
                continue;

            BattleAction action =
                CreateBattleAction(slot);

            battleContext._battleEvent.RaiseActionStart(
                action);

            //--------------------------------
            // 스킬 실행 + 자원 소모
            //--------------------------------

            ExecuteSkill(action);

            BattleLogType type = action.Phase switch
            {
                ActionPhase.PRETURN => BattleLogType.Prestige,
                ActionPhase.FORESIGHT => BattleLogType.Preparation,
                _ => BattleLogType.Normal
            };

            //--------------------------------
            // 직접 피해 로그가 있으면 Damage 로그
            //--------------------------------

            if (action.HasDamageLog)
            {
                battleContext.battleManager.BattleLogger.LogDamage(
                    action,
                    type,
                    action.LoggedDamage,
                    action.LoggedBeforeHP,
                    action.LoggedAfterHP);
            }
            else
            {
                battleContext.battleManager.BattleLogger.LogAction(
                    action,
                    type);
            }

            battleContext._battleEvent.RaiseActionEnd(
                action);
        }
    }

    //------------------------------------------------
    // 공통 스킬 실행
    //------------------------------------------------

    private void ExecuteSkill(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Skill == null)
            return;

        action.Skill.Execute(action);

        action.Skill.ConsumeResource(
            action.Owner);
    }

    //------------------------------------------------

    private bool IsValidSlot(ActionSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.Owner == null)
            return false;

        if (slot.TargetCharacter == null)
            return false;

        if (slot.Part == null)
            return false;

        if (slot.TargetPart == null)
            return false;

        if (slot.Skill == null)
        {
            Debug.LogWarning("Skill NULL");
            return false;
        }

        if (slot.Owner.IsDead)
            return false;

        if (slot.TargetCharacter.IsDead)
            return false;

        if (slot.Part.IsBroken)
            return false;

        if (slot.TargetPart.IsBroken)
            return false;

        return true;
    }

    //------------------------------------------------

    private BattleAction CreateBattleAction(ActionSlot slot)
    {
        return new BattleAction
        {
            Slot = slot
        };
    }
}