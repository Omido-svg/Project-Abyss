#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>R6 lifetime phases, refusal isolation, cooldown and chain safety.</summary>
public static class R6LifetimeCheckpointVerification20261102
{
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("EditMode only.");
        int pass=0,fail=0;
        void Check(string label,bool ok,string details="")
        {
            if(ok){pass++;Debug.Log("[R6-Lifetime] PASS "+label+" "+details);}
            else{fail++;Debug.LogError("[R6-Lifetime] FAIL "+label+" "+details);}
        }
        var scheduler=new BattleReactionScheduler();
        var memory=new R6ReactionLifetimeRegistry20261102();
        ReactionRequest Request(long id,long cause,long actor,string skill,long chain=7001)=>
            new ReactionRequest(id,5000,5000,cause,chain,actor,skill,
                ReactionTimingCheckpoint.AfterExchangeSettled,0,id);
        AbilityLifetimeEvent E(long turn,long cause,long clash=5000,long roll=1,long hit=1)=>
            new AbilityLifetimeEvent(turn,clash,roll,hit,cause);
        ClashResultContext Completed()=>
            new ClashResultContext{
                Exchanges={new ClashExchangeResult{
                    ExchangeIndex=0,WasCancelled=false,IsOneSided=true}}
            };

        var req=Request(99001,99011,101,"R6_LIVE_REQUEST");
        var accepted=scheduler.Queue(req);
        var once=new AbilityLifetimeRules(oncePerTurn:true,fullCooldownTurns:2);
        Check("L01 Request accepted by actual R6 scheduler",accepted.Accepted);
        Check("L02 Requested phase commits once after successful Queue",
            memory.TryBindAccepted(accepted,once,E(7,99011),
                R3ReactionConsumeOn.Requested,null,out _)
            && memory.TryGetMemory(101,"R6_LIVE_REQUEST",out var m) &&
            m.SuccessfulCauseCount==1 && m.ReadyTurn==10);
        Check("L03 consumed Requested can still start its own bound request",
            memory.MayStart(req,out _));
        Check("L04 Request phase completes without charging twice",
            memory.Complete(req,Completed(),out _) &&
            memory.TryGetMemory(101,"R6_LIVE_REQUEST",out var after) &&
            after.SuccessfulCauseCount==1);
        var next=Request(99002,99012,101,"R6_LIVE_REQUEST");
        var nextDecision=scheduler.Queue(next);
        Check("L05 rejected lifetime binding cannot bypass via queued request",
            nextDecision.Accepted &&
            !memory.TryBindAccepted(nextDecision,once,E(8,99012),
                R3ReactionConsumeOn.Requested,null,out _) &&
            !memory.MayStart(next,out _));
        memory.Cancel(next);
        var ready=Request(99003,99013,101,"R6_LIVE_REQUEST");
        var readyDecision=scheduler.Queue(ready);
        Check("L06 cooldown blocks exactly two FULL turns 8 and 9, allows 10",
            readyDecision.Accepted &&
            memory.TryBindAccepted(readyDecision,once,E(10,99013),
                R3ReactionConsumeOn.Requested,null,out _) &&
            memory.TryGetMemory(101,"R6_LIVE_REQUEST",out var readyMem) &&
            readyMem.ReadyTurn==13);
        memory.Complete(ready,Completed(),out _);

        var start=Request(99101,99111,102,"R6_LIVE_START");
        var started=scheduler.Queue(start);
        var startedRules=new AbilityLifetimeRules(oncePerClash:true);
        AbilityLifetimeMemory startMem=null;
        Check("L07 Started phase does not consume at queue time",
            memory.TryBindAccepted(started,startedRules,E(8,99111),
                R3ReactionConsumeOn.Started,null,out _) &&
            memory.TryGetMemory(102,"R6_LIVE_START",out startMem)&&
            startMem.SuccessfulCauseCount==0);
        Check("L08 execution may start after valid lifetime check",
            memory.MayStart(start,out _));
        Check("L09 null/cancelled execution does NOT consume Started",
            !memory.Complete(start,null,out _) &&
            startMem.SuccessfulCauseCount==0);
        var retry=Request(99102,99112,102,"R6_LIVE_START");
        var retryDecision=scheduler.Queue(retry);
        Check("L10 actual committed exchange consumes Started once",
            memory.TryBindAccepted(retryDecision,startedRules,E(8,99112),
                R3ReactionConsumeOn.Started,null,out _) &&
            memory.Complete(retry,Completed(),out _) &&
            startMem.SuccessfulCauseCount==1);
        var sameClash=Request(99103,99113,102,"R6_LIVE_START");
        Check("L11 once-per-Clash holds even when new RequestId appears",
            !memory.TryBindAccepted(scheduler.Queue(sameClash),
                startedRules,E(8,99113),R3ReactionConsumeOn.Started,
                null,out _) && !memory.MayStart(sameClash,out _));
        memory.Cancel(sameClash);

        var applied=Request(99201,99211,103,"R6_LIVE_APPLIED");
        var condition=new AbilityLifetimeRules(oncePerOriginHit:true);
        Check("L12 Applied requires a factual applied-effect predicate",
            !memory.TryBindAccepted(scheduler.Queue(applied),condition,
                E(9,99211),R3ReactionConsumeOn.Applied,null,out _) &&
            !memory.MayStart(applied,out _));
        memory.Cancel(applied);
        var rejectedEffect=Request(99202,99212,103,"R6_LIVE_APPLIED");
        var outcome=scheduler.Queue(rejectedEffect);
        AbilityLifetimeMemory appliedMemory=null;
        Check("L13 result object alone cannot fake Applied",
            memory.TryBindAccepted(outcome,condition,E(9,99212),
                R3ReactionConsumeOn.Applied,_=>false,out _) &&
            !memory.Complete(rejectedEffect,Completed(),out _) &&
            memory.TryGetMemory(103,"R6_LIVE_APPLIED",out appliedMemory) &&
            appliedMemory.SuccessfulCauseCount==0);
        var actualEffect=Request(99203,99213,103,"R6_LIVE_APPLIED");
        var acceptedEffect=scheduler.Queue(actualEffect);
        Check("L14 attested actual applied effect consumes one origin hit",
            memory.TryBindAccepted(acceptedEffect,condition,E(9,99213),
                R3ReactionConsumeOn.Applied,
                result=>result?.Exchanges?.Count==1 &&
                    result.Exchanges[0].DamageContext?.WasApplied==true,
                out _) &&
            !memory.Complete(actualEffect,Completed(),out _) &&
            appliedMemory.SuccessfulCauseCount==0,
            "no actual DamageContext in fixture");
        var another=Request(99204,99214,104,"R6_LIVE_APPLIED");
        Check("L15 different actor with same skill has independent lifetime",
            memory.TryBindAccepted(scheduler.Queue(another),condition,
                E(9,99214),R3ReactionConsumeOn.Applied,_=>true,out _) &&
            memory.Complete(another,Completed(),out _));
        var unknown=Request(99301,99311,105,"R6_UNKNOWN",chain:7711);
        Check("L16 registered intent can be quarantined against replay",
            memory.TryBindAccepted(scheduler.Queue(unknown),
                new AbilityLifetimeRules(),E(3,99311),
                R3ReactionConsumeOn.Started,null,out _));
        memory.QuarantineUnknownCommit(unknown);
        var later=Request(99302,99312,105,"R6_UNKNOWN",chain:7711);
        Check("L17 unknown commit blocks future requests in same chain",
            memory.QuarantinedChainCount==1 &&
            !memory.TryBindAccepted(scheduler.Queue(later),
                new AbilityLifetimeRules(),E(3,99312),
                R3ReactionConsumeOn.Started,null,out _) &&
            !memory.MayStart(later,out _));
        memory.ClearForBattleEnd();
        Check("L18 battle end clears runtime memory and chain quarantine",
            memory.PendingBindings==0&&memory.CompletedCount==0 &&
            memory.QuarantinedChainCount==0 &&
            !memory.TryGetMemory(101,"R6_LIVE_REQUEST",out _));
        Debug.Log("[R6-Lifetime] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6 lifetime failed="+fail);
    }
}
#endif
