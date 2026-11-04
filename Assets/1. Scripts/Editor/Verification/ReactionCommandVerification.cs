#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>R5-I reaction commands are scheduled, never treated as applied effects.</summary>
public static class ReactionCommandVerification
{
    private sealed class DamageBackend : ICombatCommandBackend
    {
        public readonly List<string> Calls=new List<string>();
        public int Total;
        public bool CanApply(CombatCommandKind kind)=>kind==CombatCommandKind.DealDamage;
        public CombatCommandReceipt Apply(CombatCommandEnvelope request)
        {
            Calls.Add(request.CommandId);Total+=request.RequestedAmount;
            return new CombatCommandReceipt(CombatCommandOutcome.Applied,
                request.RequestedAmount,0,true,"R5 fake damage test only");
        }
    }
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
        int pass=0,fail=0;
        void Check(string id,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R5-Reaction] PASS "+id+" | "+detail);}
            else{fail++;Debug.LogError("[R5-Reaction] FAIL "+id+" | "+detail);}
        }
        var scheduler=new BattleReactionScheduler();
        var reactions=new R5ReactionRequestCommandBackend(scheduler);
        var damage=new DamageBackend();
        var composite=new R5CompositeCommandBackend(damage,reactions);
        var ability=new AbilityInstance(new AbilityDefinitionRef("R5-REACTION-PROBE","none"),
            new AbilityIdentity("R5-REACTION-PROBE",101,"SLOT",0));
        var e=new AbilityEventContext(AbilityEventKind.RollUsing,50001,60001,101,0,false,false);
        var r1=new ReactionRequest(90001,50001,50001,60001,777,101,
            "COUNTER",ReactionTimingCheckpoint.AfterActionCompleted,2,1);
        var c1=new CombatCommandEnvelope("react-1",ability.Identity,
            CombatCommandKind.RequestReactionAction,50001,60001,
            new CombatTargetRef(101,"",""),0,phase:1,priority:2,
            registrationOrder:0,commandSequence:1);
        reactions.RegisterRequest(c1,r1);
        Check("I01 composite routes each typed operation to exactly one backend",
            composite.CanApply(CombatCommandKind.RequestReactionAction)&&
            composite.CanApply(CombatCommandKind.DealDamage)&&
            !composite.CanApply(CombatCommandKind.ScheduleAbility));
        var apply=new CombatCommandApplyService();
        var batch=apply.Apply(ability,e,new[]{c1},composite);
        Check("I02 request is scheduled, never claimed as executed gameplay",
            batch.ScheduledCount==1&&batch.CommittedCount==0 &&
            batch.Records[0].Receipt.Outcome==CombatCommandOutcome.Scheduled &&
            !batch.Records[0].Receipt.EffectCommitted &&
            ability.RuntimeState.SuccessfulActivationCount==0,
            "scheduled="+batch.ScheduledCount+" applied="+batch.CommittedCount);
        Check("I03 R6 queue receives exact request lineage",
            scheduler.PendingCount==1 &&
            reactions.TryGetDecision(c1.CommandId,out var decision)&&decision.Accepted &&
            decision.Request.ParentActionId==50001 &&
            decision.Request.CauseEventId==60001 &&
            decision.Request.ChainId==777,
            "R6 pending="+scheduler.PendingCount);
        var duplicate=apply.Apply(ability,e,new[]{c1},composite);
        Check("I04 same command never enqueues twice",
            duplicate.Records[0].Receipt.Outcome==CombatCommandOutcome.SkippedDuplicate &&
            scheduler.PendingCount==1);
        Check("I05 request not available before its checkpoint",
            !scheduler.TryDequeue(ReactionTimingCheckpoint.AfterExchangeSettled,out _));
        Check("I06 coordinator can dequeue only matching checkpoint",
            scheduler.TryDequeue(ReactionTimingCheckpoint.AfterActionCompleted,out var dequeued)&&
            dequeued.RequestId==r1.RequestId&&scheduler.PendingCount==0);
        bool wrongOwner=false,wrongCause=false,wrongKind=false;
        try{
            reactions.RegisterRequest(new CombatCommandEnvelope("bad-owner",ability.Identity,
                CombatCommandKind.RequestReactionAction,50001,60001,
                new CombatTargetRef(999,"",""),0,1,0,0,1),r1);
        }catch(ArgumentException){wrongOwner=true;}
        try{
            reactions.RegisterRequest(new CombatCommandEnvelope("bad-cause",ability.Identity,
                CombatCommandKind.RequestReactionAction,50001,60002,
                new CombatTargetRef(101,"",""),0,1,0,0,1),r1);
        }catch(ArgumentException){wrongCause=true;}
        try{
            reactions.RegisterRequest(new CombatCommandEnvelope("bad-type",ability.Identity,
                CombatCommandKind.ScheduleAbility,50001,60001,
                new CombatTargetRef(101,"",""),0,1,0,0,1),r1);
        }catch(ArgumentException){wrongKind=true;}
        Check("I07 forged reaction owner, cause or command kind blocked pre-register",
            wrongOwner&&wrongCause&&wrongKind);
        var cloned=new ReactionRequest(90001,50001,50001,60001,777,101,
            "COUNTER",ReactionTimingCheckpoint.AfterActionCompleted,2,2);
        var c2=new CombatCommandEnvelope("react-different-id",ability.Identity,
            CombatCommandKind.RequestReactionAction,50001,60001,
            new CombatTargetRef(101,"",""),0,1,0,0,2);
        reactions.RegisterRequest(c2,cloned);
        var second=apply.Apply(ability,e,new[]{c2},composite);
        Check("I08 R6 rejects duplicate RequestId under different command IDs",
            second.ScheduledCount==0 && second.RejectedCount==1 &&
            scheduler.PendingCount==0);
        var badTiming=new ReactionRequest(90002,50001,50001,60001,777,101,
            "COUNTER",ReactionTimingCheckpoint.AfterDamageResultCommitted,1,3);
        var c3=new CombatCommandEnvelope("react-unsupported",ability.Identity,
            CombatCommandKind.RequestReactionAction,50001,60001,
            new CombatTargetRef(101,"",""),0,1,0,0,3);
        reactions.RegisterRequest(c3,badTiming);
        var unsupported=apply.Apply(ability,e,new[]{c3},composite);
        Check("I09 unsupported mid-commit checkpoint is not queued",
            unsupported.CommittedCount==0 && unsupported.RejectedCount==1 &&
            scheduler.PendingCount==0);

        var r4=new ReactionRequest(90003,50001,50001,60001,777,101,
            "COUNTER",ReactionTimingCheckpoint.AfterClashCompleted,1,4);
        var c4=new CombatCommandEnvelope("react-with-damage",ability.Identity,
            CombatCommandKind.RequestReactionAction,50001,60001,
            new CombatTargetRef(101,"",""),0,phase:2,priority:0,
            registrationOrder:0,commandSequence:4);
        var d4=new CombatCommandEnvelope("damage-before-reaction",ability.Identity,
            CombatCommandKind.DealDamage,50001,60001,
            new CombatTargetRef(202,"",""),7,phase:1,priority:0,
            registrationOrder:0,commandSequence:3);
        reactions.RegisterRequest(c4,r4);
        var mixed=apply.Apply(ability,e,new[]{c4,d4},composite);
        Check("I10 mixed damage and reaction batch produces distinct outcomes",
            mixed.ScheduledCount==1&&mixed.CommittedCount==1&&
            mixed.Records.Count==2 &&
            mixed.Records[0].Request.Kind==CombatCommandKind.DealDamage&&
            mixed.Records[1].Receipt.Outcome==CombatCommandOutcome.Scheduled &&
            damage.Total==7 && scheduler.PendingCount==1,
            "damage actual fake commit 7, reaction queued separately");
        Check("I11 an accepted reaction request is not counted as applied damage",
            mixed.Records[1].Receipt.AppliedAmount==0 &&
            mixed.Records[1].Receipt.AbsorbedAmount==0 &&
            ability.RuntimeState.SuccessfulActivationCount==1);
        var ambiguous=new R5CompositeCommandBackend(damage,new DamageBackend());
        Check("I12 two providers for same command type reject routing",
            !ambiguous.CanApply(CombatCommandKind.DealDamage) &&
            ambiguous.Apply(d4).Outcome==CombatCommandOutcome.Rejected);
        scheduler.ClearForBattleEnd();
        reactions.ClearForBattleEnd();
        apply.ResetForBattle();
        Check("I13 lifecycle clear releases queue and bound intent",
            scheduler.PendingCount==0 && reactions.BoundRequestCount==0 &&
            apply.UniqueAttemptCount==0);
        Debug.Log("[R5-Reaction] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R5 reaction command contract failed "+fail);
    }
}
#endif
