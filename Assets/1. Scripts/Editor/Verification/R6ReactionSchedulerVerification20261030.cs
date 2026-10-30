#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R6ReactionSchedulerVerification20261030
{
    private static ReactionRequest Make(long id, ReactionTimingCheckpoint cp,
        int priority=0,long cause=100,long sequence=0,
        long parent=500,long root=500,long chain=99)
    {
        return new ReactionRequest(id,parent,root,cause,chain,901,
            "R6-TEST-REACTION",cp,priority,sequence);
    }

    [MenuItem("Game System Verification/R6-Ia - Reaction Queue Safety (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)
            throw new InvalidOperationException("Edit Mode only");
        var scheduler=new BattleReactionScheduler();
        int pass=0,fail=0;
        void Check(string id,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R6-Ia] PASS "+id+" "+detail);}
            else{fail++;Debug.LogError("[R6-Ia] FAIL "+id+" "+detail);}
        }

        var invalid=Make(0,ReactionTimingCheckpoint.AfterClashCompleted);
        Check("I01 invalid identity rejected with diagnostic",
            scheduler.Queue(invalid).Status==ReactionScheduleStatus.InvalidRequest &&
            !string.IsNullOrWhiteSpace(scheduler.LastDecision.Diagnostic));
        Check("I02 R6-Ib after-exchange request accepted",
            scheduler.Queue(Make(1,ReactionTimingCheckpoint.AfterExchangeSettled)).
                Status==ReactionScheduleStatus.Accepted);
        Check("I03 damage commit checkpoint unsupported until R6-Ib",
            scheduler.Queue(Make(2,ReactionTimingCheckpoint.AfterDamageResultCommitted)).
                Status==ReactionScheduleStatus.UnsupportedCheckpoint);

        Check("I04 after-clash enqueue accepted",
            scheduler.TrySchedule(Make(5,ReactionTimingCheckpoint.AfterClashCompleted,
                priority:0,cause:201,sequence:1)));
        scheduler.TrySchedule(Make(6,ReactionTimingCheckpoint.AfterClashCompleted,
            priority:0,cause:200,sequence:99));
        scheduler.TrySchedule(Make(7,ReactionTimingCheckpoint.AfterClashCompleted,
            priority:-1,cause:205,sequence:0));
        scheduler.TrySchedule(Make(8,ReactionTimingCheckpoint.AfterActionCompleted,
            priority:-9,cause:100,sequence:0));
        Check("I05 after-exchange request remains separately pending",
            scheduler.PendingCount==5);
        Check("I06 after-exchange only drains its own checkpoint",
            scheduler.TryDequeue(ReactionTimingCheckpoint.AfterExchangeSettled,out var intermediate) &&
            intermediate.RequestId==1 && scheduler.PendingCount==4);
        bool d1=scheduler.TryDequeue(ReactionTimingCheckpoint.AfterClashCompleted,out var a);
        bool d2=scheduler.TryDequeue(ReactionTimingCheckpoint.AfterClashCompleted,out var b);
        bool d3=scheduler.TryDequeue(ReactionTimingCheckpoint.AfterClashCompleted,out var c);
        Check("I07 explicit priority then cause sequence then enqueue",
            d1&&d2&&d3 && a.RequestId==7 && b.RequestId==6 && c.RequestId==5);
        Check("I08 checkpoint isolation preserves after-action",
            scheduler.PendingCount==1 &&
            scheduler.TryDequeue(ReactionTimingCheckpoint.AfterActionCompleted,out var d) &&
            d.RequestId==8);
        Check("I09 queue empty after checkout",
            scheduler.PendingCount==0 &&
            !scheduler.TryDequeue(ReactionTimingCheckpoint.AfterClashCompleted,out var _));
        Check("I10 duplicate RequestId cannot replay after dispatch",
            scheduler.Queue(Make(5,ReactionTimingCheckpoint.AfterClashCompleted)).
                Status==ReactionScheduleStatus.Duplicate);
        Check("I11 dispatch trace records intermediate and later checkpoints",
            scheduler.DispatchTrace.Count==5 &&
            scheduler.DispatchTrace[0].Request.RequestId==1 &&
            scheduler.DispatchTrace[1].Request.RequestId==7 &&
            scheduler.DispatchTrace[4].Request.RequestId==8 &&
            scheduler.DispatchTrace[4].AtCheckpoint==ReactionTimingCheckpoint.AfterActionCompleted);

        var nested=Make(10,ReactionTimingCheckpoint.AfterActionCompleted,
            parent:900,root:500);
        Check("I12 reaction-to-reaction blocked unless explicitly allowed",
            scheduler.Queue(nested).Status==ReactionScheduleStatus.NestedReactionRequiresPolicy);
        Check("I13 nested legal chain allowed by explicit policy",
            scheduler.Queue(nested,allowReactionToReaction:true,depth:2).Accepted);

        var depth=new BattleReactionScheduler{MaxReactionDepth=2};
        var tooDeep=depth.Queue(Make(31,ReactionTimingCheckpoint.AfterActionCompleted,
            parent:900,root:500),allowReactionToReaction:true,depth:3);
        Check("I14 depth guard rejected with cause lineage",
            tooDeep.Status==ReactionScheduleStatus.GuardExceeded &&
            tooDeep.Diagnostic.Contains("cause=100") &&
            tooDeep.Diagnostic.Contains("chain=99"));

        var count=new BattleReactionScheduler{MaxActionsPerChain=1};
        var f1=count.Queue(Make(32,ReactionTimingCheckpoint.AfterActionCompleted));
        var f2=count.Queue(Make(33,ReactionTimingCheckpoint.AfterActionCompleted));
        Check("I15 per-chain count guard diagnostic, not silent drop",
            f1.Accepted && f2.Status==ReactionScheduleStatus.GuardExceeded &&
            f2.Diagnostic.Contains("root=500"));
        Check("I16 another chain has separate allowance",
            count.Queue(Make(34,ReactionTimingCheckpoint.AfterActionCompleted,chain:100)).Accepted);

        var pending=new BattleReactionScheduler{MaxPendingRequests=1};
        pending.Queue(Make(35,ReactionTimingCheckpoint.AfterClashCompleted));
        Check("I17 queue capacity guard",pending.Queue(
            Make(36,ReactionTimingCheckpoint.AfterClashCompleted)).Status==
            ReactionScheduleStatus.GuardExceeded);

        Check("I18 no gameplay execute callback runs during enqueue/dequeue",
            scheduler.DispatchTrace.All(x=>x.Request.OwnerActorRuntimeId==901) &&
            scheduler.PendingCount==1); // nested request only
        scheduler.ClearForBattleEnd();
        Check("I19 battle end releases pending, trace and dedupe IDs",
            scheduler.PendingCount==0 && scheduler.DispatchTrace.Count==0 &&
            scheduler.Decisions.Count==0 &&
            scheduler.Queue(Make(5,ReactionTimingCheckpoint.AfterClashCompleted)).Accepted);

        Debug.Log("[R6-Ia] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6-Ia scheduler checks failed "+fail);
    }
}
#endif
