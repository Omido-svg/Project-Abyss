#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>R5 live mixed command: actual DamageManager + deferred R6 reaction.</summary>
public static class R5MixedDamageReactionVerification20261102
{
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode required");
        var mgr=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var battle=mgr?.BattleContext;
        var player=battle?.Player;
        var target=battle?.Enemies?.FirstOrDefault(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts);
        if(player==null||target==null||battle.ResolveDamageManager()==null)
            throw new InvalidOperationException("Battle test manager + normal enemy required");
        int pass=0,fail=0;
        void Check(string id,bool success,string info="")
        {
            if(success){pass++;Debug.Log("[R5-Mixed] PASS "+id+" | "+info);}
            else{fail++;Debug.LogError("[R5-Mixed] FAIL "+id+" | "+info);}
        }
        var ability=new AbilityInstance(new AbilityDefinitionRef("R5-MIXED","TEST"),
            new AbilityIdentity("R5-MIXED",R4LiveSlotQuery.ActorRuntimeId(player),"S",0));
        var cause=new AbilityEventContext(AbilityEventKind.RollUsing,
            980301,980302,R4LiveSlotQuery.ActorRuntimeId(player),0,false,false);
        var damage=new CombatCommandEnvelope("R5-MIX-DAMAGE",ability.Identity,
            CombatCommandKind.DealDamage,cause.ActionId,cause.CauseId,
            new CombatTargetRef(R4LiveSlotQuery.ActorRuntimeId(target),"",null),
            4,phase:1,priority:0,registrationOrder:0,commandSequence:0);
        var react=new CombatCommandEnvelope("R5-MIX-REACTION",ability.Identity,
            CombatCommandKind.RequestReactionAction,cause.ActionId,cause.CauseId,
            new CombatTargetRef(R4LiveSlotQuery.ActorRuntimeId(player),"",null),
            0,phase:2,priority:0,registrationOrder:0,commandSequence:1);
        var request=new ReactionRequest(980303,cause.ActionId,cause.ActionId,cause.CauseId,
            980304,R4LiveSlotQuery.ActorRuntimeId(player),"R5_MIXED_COUNTER",
            ReactionTimingCheckpoint.AfterActionCompleted,0,1);
        var backend=new R5CombatSpecialistBackend(battle);
        var scheduler=new BattleReactionScheduler();
        var reactionBackend=new R5ReactionRequestCommandBackend(scheduler);
        backend.RegisterDamage(damage,DamageRequest.Direct(player,target,4));
        reactionBackend.RegisterRequest(react,request);
        var mixed=new R5CompositeCommandBackend(backend,reactionBackend);
        var service=new CombatCommandApplyService();
        int hpBefore=target.CurrentHP;
        var result=service.Apply(ability,cause,new[]{react,damage},mixed);
        Check("M01 commands stable-sorted damage then reaction",
            result.Records.Count==2 &&
            result.Records[0].Request.Kind==CombatCommandKind.DealDamage &&
            result.Records[1].Request.Kind==CombatCommandKind.RequestReactionAction);
        Check("M02 damage always delegated to actual DamageManager",
            backend.TryGetDamageContext(damage.CommandId,out var ctx) &&
            ctx?.Result!=null&&ctx.Target==target &&
            target.CurrentHP<=hpBefore,
            "before="+hpBefore+" after="+target.CurrentHP);
        Check("M03 reaction is pending, not an executed skill",
            result.ScheduledCount==1&&scheduler.PendingCount==1 &&
            result.Records[1].Receipt.Outcome==CombatCommandOutcome.Scheduled &&
            !result.Records[1].Receipt.EffectCommitted,
            "pending="+scheduler.PendingCount);
        Check("M04 applied count only reflects real damage commit",
            result.CommittedCount==(result.Records[0].Receipt.EffectCommitted?1:0) &&
            ability.RuntimeState.SuccessfulActivationCount<=1);
        int after=target.CurrentHP;
        var replay=service.Apply(ability,cause,new[]{damage,react},mixed);
        Check("M05 no double damage or extra queued reaction",
            replay.CommittedCount==0 && replay.ScheduledCount==0 &&
            target.CurrentHP==after && scheduler.PendingCount==1 &&
            replay.Records.All(x=>x.Receipt.Outcome==CombatCommandOutcome.SkippedDuplicate));
        Check("M06 invalid checkpoint cannot release request",
            !scheduler.TryDequeue(ReactionTimingCheckpoint.AfterDamageResultCommitted,out _));
        Check("M07 coordinator releases the exact reaction at action completion",
            scheduler.TryDequeue(ReactionTimingCheckpoint.AfterActionCompleted,out var queued)&&
            queued.RequestId==request.RequestId &&
            queued.CauseEventId==cause.CauseId &&
            scheduler.PendingCount==0);
        Check("M08 no extra combat action was created by the command backend",
            reactionBackend.BoundRequestCount==1 &&
            result.Records[1].Receipt.AppliedAmount==0);
        backend.ClearBindings();
        reactionBackend.ClearForBattleEnd();
        scheduler.ClearForBattleEnd();
        service.ResetForBattle();
        Check("M09 battle end clears both specialist and scheduler payloads",
            backend.BoundCommandCount==0 &&
            reactionBackend.BoundRequestCount==0 &&
            scheduler.PendingCount==0 &&
            service.UniqueAttemptCount==0);
        Debug.Log("[R5-Mixed] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R5 mixed live verification failed "+fail);
    }
}
#endif
