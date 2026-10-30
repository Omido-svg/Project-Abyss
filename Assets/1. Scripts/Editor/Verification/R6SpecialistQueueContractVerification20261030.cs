#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R6SpecialistQueueContractVerification20261030
{
    private static ReactionRequest R(long id,long root=600,long cause=900,long chain=600)
        =>new ReactionRequest(id,root,root,cause,chain,301,"HIFUMI-COUNTER",
            ReactionTimingCheckpoint.AfterClashCompleted,0,id);

    [MenuItem("Game System Verification/R6-Ia - Specialist Queue Contract (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only");
        int pass=0,fail=0;
        void Check(string id,bool ok)
        {
            if(ok){pass++;Debug.Log("[R6-Specialist] PASS "+id);}
            else{fail++;Debug.LogError("[R6-Specialist] FAIL "+id);}
        }
        var scheduler=new BattleReactionScheduler();
        var registry=new R6SpecialistReactionRegistry();
        int calls=0;
        var id=scheduler.AllocateRequestId();
        var req=R(id);
        Check("S01 allocated ID is positive",id>0);
        Check("S02 consecutive IDs distinct",scheduler.AllocateRequestId()!=id);
        Check("S03 explicit specialist payload registered",
            registry.TryRegister(req,()=>true,()=>calls++,out var _));
        Check("S04 registration never invokes game callback",
            calls==0&&registry.PendingCount==1);
        Check("S05 scheduler accepts registered request",scheduler.TrySchedule(req));
        Check("S06 duplicate scheduler request rejected",
            !scheduler.TrySchedule(req));
        Check("S07 checkpoint deferred until explicit dequeue",
            calls==0&&scheduler.PendingCount==1);
        Check("S08 wrong checkpoint cannot consume",
            !scheduler.TryDequeue(ReactionTimingCheckpoint.AfterActionCompleted,out var _));
        bool dequeued=scheduler.TryDequeue(ReactionTimingCheckpoint.AfterClashCompleted,
            out var queued);
        Check("S09 dequeue preserves request and provenance",
            dequeued&&queued.CauseEventId==req.CauseEventId&&queued.RequestId==req.RequestId);
        Check("S10 registered specialist executes one time",
            registry.TryExecute(queued,out var _)&&calls==1);
        Check("S11 re-executing consumed ID refused",
            !registry.TryExecute(queued,out var _)&&calls==1&&registry.ConsumedCount==1);
        Check("S12 cannot re-register consumed ID",
            !registry.TryRegister(req,()=>true,()=>calls++,out var _));

        var cancelled=R(710);
        Check("S13 cancellation before queue does not execute",
            registry.TryRegister(cancelled,()=>true,()=>calls++,out var _)&&
            registry.CancelUnscheduled(cancelled.RequestId) &&
            !registry.TryExecute(cancelled,out var _)&&calls==1);

        var invalid=R(711);
        Check("S14 invalidated target cannot execute original specialist",
            registry.TryRegister(invalid,()=>false,()=>calls++,out var _) &&
            !registry.TryExecute(invalid,out var _) && calls==1);

        var tamper=R(712,root:601,cause:901,chain:601);
        var altered=R(712,root:601,cause:902,chain:601);
        registry.TryRegister(tamper,()=>true,()=>calls++,out var _);
        Check("S15 altered cause rejected even with valid request ID",
            !registry.TryExecute(altered,out var _) && calls==1);

        var bad=R(713,root:602,cause:903,chain:602);
        registry.TryRegister(bad,()=>true,()=>{calls++;throw new InvalidOperationException();},out var _);
        Check("S16 exception quarantines chain and does not retry",
            !registry.TryExecute(bad,out var _) && calls==2 &&
            registry.IsChainQuarantined(bad.ChainId));
        var sameChain=R(714,root:602,cause:904,chain:602);
        registry.TryRegister(sameChain,()=>true,()=>calls++,out var _);
        Check("S17 quarantined chain cannot apply further callbacks",
            !registry.TryExecute(sameChain,out var _)&&calls==2);
        var other=R(715,root:603,cause:905,chain:603);
        registry.TryRegister(other,()=>true,()=>calls++,out var _);
        Check("S18 different chain remains executable",
            registry.TryExecute(other,out var _)&&calls==3);

        registry.ClearForBattleEnd();
        scheduler.ClearForBattleEnd();
        Check("S19 battle cleanup clears pending and dedupe",
            registry.PendingCount==0 && registry.ConsumedCount==0 &&
            !registry.IsChainQuarantined(bad.ChainId) && scheduler.PendingCount==0);
        var instance=new HifumiMechanic();
        Check("S20 Hifumi cannot enable opt-in before initialized",
            !instance.TryEnableR6DeferredCounters());
        Debug.Log("[R6-Specialist] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6 Specialist tests failed "+fail);
    }
}
#endif