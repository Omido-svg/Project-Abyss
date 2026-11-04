using System;
using ProjectAbyss.Abilities;
using UnityEngine;

/// <summary>
/// 1029 §12.3 counter soldier: authoritatively queues a NEW one-roll,
/// 6-power RED one-sided reaction at R6 coordinator checkpoints.
/// Not a synchronous/recursive attack from a BattleEvent callback.
/// </summary>
public static class EnemyCounterReaction
{
    public const string CounterSkillId=
        "enemy1029.stage1.counter_guard.revenge";
    public const string ReactionSlotPrefix="R7-8-COUNTER:";
    public static SkillDefinition CounterDefinition =>
        Resources.Load<SkillDefinition>(
            EnemyStageOneSkillCatalog.ResourcePath+"/"+
            CounterSkillId);

    public static bool IsCounterTaggedSkill(string id)=>
        id=="enemy1029.stage1.counter_guard.normal" ||
        id=="enemy1029.stage1.counter_guard.strong_duel";

    public static bool IsCounterReaction(BattleAction action) =>
        action?.Slot?.SlotId?.StartsWith(ReactionSlotPrefix,
            StringComparison.Ordinal)==true;

    public static bool TryQueue(NormalEnemy owner,BattleAction origin,
        Character attacker,ReactionTimingCheckpoint when)
    {
        var battle=owner?.BattleContext;
        var service=battle?.Services;
        var scheduler=service?.ReactionScheduler;
        var actions=service?.ReactionActionRegistry;
        var source=CounterDefinition;
        if(owner==null||owner.IsDead||origin==null||
           origin.ActionId<=0||IsCounterReaction(origin)||
           attacker==null||attacker==owner||attacker.IsDead||
           scheduler==null||actions==null||
           source==null||source.SkillId!=CounterSkillId||
           source.EffectiveRollCount!=1||source.ActionType!=ActionType.NormalAttack)
            return false;
        if(battle.Player!=attacker || battle.Enemies==null ||
           !battle.Enemies.Contains(owner))return false;
        var point=BattleTargetValidator.ChooseWeightedTargetPoint(
            attacker,TargetSelectionRule.StandardAttack,
            brokenPartWeight:.7f);
        if(!point.IsValid || point.Character!=attacker)return false;

        var skill=owner.CreateRuntimeSkillForLoadout(source);
        if(skill==null)return false;
        skill.Initialize(owner,owner.BattleEvent);
        long id=scheduler.AllocateRequestId();
        // R6 reaction requires a unique, non-planned ActionId and cannot
        // steal a resource-committed normal action.
        var slot=new ActionSlot
        {
            ActionId=id,Owner=owner,Part=null,Skill=skill,
            TargetCharacter=attacker,TargetPart=point.Part,
            TargetSlot=null,ActionIndex=0,
            Phase=ActionPhase.COMBAT,
            SlotId=ReactionSlotPrefix+id,
            ResourceCostCommitted=false,Speed=0
        };
        long actor=R4LiveSlotQuery.ActorRuntimeId(owner);
        var req=new ReactionRequest(id,origin.ActionId,origin.ActionId,id,
            origin.ActionId,actor,source.SkillId,when,0,id);
        if(!actions.TryRegister(req,slot,out var why))
        {
            Debug.LogWarning("[R7-8-Counter] Cannot register: "+why);
            return false;
        }
        if(!scheduler.TrySchedule(req))
        {
            actions.CancelUnscheduled(id);
            Debug.LogWarning("[R7-8-Counter] Scheduler rejected reaction.");
            return false;
        }
        return true;
    }
}