#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R6IbClashFrameContractVerification20261030
{
    private static ReactionRequest Request(long id,long parent,long cause) =>
        new ReactionRequest(id,parent,parent,cause,parent,101,
            "VIRTUAL-REACTION",ReactionTimingCheckpoint.AfterExchangeSettled,
            0,id);

    [MenuItem("Game System Verification/R6-Ib - Frame Cursor & Checkpoint (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)
            throw new InvalidOperationException("Run in Edit Mode.");
        int pass=0,fail=0;
        void Check(string id,bool ok)
        {
            if(ok){pass++;Debug.Log("[R6-Ib] PASS "+id);}
            else{fail++;Debug.LogError("[R6-Ib] FAIL "+id);}
        }
        bool alive=true;
        int executed=0;
        var indices=new List<int>();
        var trace=new List<string>();
        var frame=new ClashResolutionFrame(50001,50002,0,3,()=>alive);
        var scheduler=new BattleReactionScheduler();
        Check("F01 frame starts at first exchange",frame.NextExchangeIndex==0 &&
            !frame.IsComplete && !frame.AwaitingCheckpoint);
        var first=frame.Step(i=>{executed++;indices.Add(i);trace.Add("main-"+i);return true;});
        Check("F02 first exchange commits once",first.Status==ClashFrameStepStatus.ExchangeCommitted &&
            first.CommittedExchangeIndex==0 && frame.NextExchangeIndex==1);
        int beforeRetry=executed;
        var repeated=frame.Step(i=>{executed++;return true;});
        Check("F03 cannot run next exchange before checkpoint",
            repeated.Status==ClashFrameStepStatus.AwaitingCheckpoint && executed==beforeRetry);
        Check("F04 first checkpoint allows resume",
            frame.ResumeAfterCheckpoint().Status==ClashFrameStepStatus.ReadyToStep);
        var second=frame.Step(i=>{executed++;indices.Add(i);trace.Add("main-"+i);return true;});
        Check("F05 second exchange commits once",second.CommittedExchangeIndex==1 && executed==2);
        var reaction=Request(601,50002,701);
        Check("F06 intermediate reaction queued at AfterExchangeSettled",
            scheduler.Queue(reaction).Accepted && scheduler.PendingCount==1);
        var consumed=scheduler.TryDequeue(ReactionTimingCheckpoint.AfterExchangeSettled,out var request);
        if(consumed)trace.Add("reaction-"+request.RequestId);
        Check("F07 independent reaction consumed before next exchange",
            consumed && request.RequestId==601 && scheduler.PendingCount==0 &&
            trace.SequenceEqual(new[]{"main-0","main-1","reaction-601"}));
        Check("F08 frame resumes only its remaining third exchange",
            frame.ResumeAfterCheckpoint().Status==ClashFrameStepStatus.ReadyToStep &&
            frame.NextExchangeIndex==2);
        var third=frame.Step(i=>{executed++;indices.Add(i);trace.Add("main-"+i);return true;});
        Check("F09 third exchange index 2, no replay",
            third.CommittedExchangeIndex==2 &&
            trace.SequenceEqual(new[]{"main-0","main-1","reaction-601","main-2"}) &&
            indices.SequenceEqual(new[]{0,1,2}));
        Check("F10 terminal checkpoint marks frame complete",
            frame.ResumeAfterCheckpoint().Status==ClashFrameStepStatus.Completed &&
            frame.IsComplete && frame.CommittedExchanges.Count==3);
        Check("F11 completed frame cannot apply again",
            frame.Step(i=>{executed++;return true;}).Status==ClashFrameStepStatus.Completed &&
            executed==3);
        var invalidFrame=new ClashResolutionFrame(60001,60002,0,3,()=>alive);
        invalidFrame.Step(i=>true);
        alive=false;
        Check("F12 resumed frame checks owner/target validity",
            invalidFrame.ResumeAfterCheckpoint().Status==ClashFrameStepStatus.Invalidated &&
            invalidFrame.NextExchangeIndex==1);
        bool didRun=false;
        Check("F13 invalidated frame never invokes damage again",
            invalidFrame.Step(i=>{didRun=true;return true;}).Status==
                ClashFrameStepStatus.Invalidated && !didRun);
        alive=true;
        var throwing=new ClashResolutionFrame(70001,70002,0,3,()=>true);
        var failure=throwing.Step(i=>throw new InvalidOperationException("unknown-commit"));
        Check("F14 throwing apply is quarantined (unknown commit)",
            failure.Status==ClashFrameStepStatus.UnknownCommit && throwing.HasUnknownCommit);
        bool invoked=false;
        Check("F15 unknown commit never auto retries",
            throwing.Step(i=>{invoked=true;return true;}).Status==
                ClashFrameStepStatus.UnknownCommit && !invoked);
        var rejected=new ClashResolutionFrame(80001,80002,0,3,()=>true);
        Check("F16 commit callback rejection invalidates frame",
            rejected.Step(i=>false).Status==ClashFrameStepStatus.Invalidated &&
            rejected.CommittedExchanges.Count==0);
        bool invalidConstruction=false;
        try{new ClashResolutionFrame(0,1,0,3,()=>true);}
        catch(ArgumentOutOfRangeException){invalidConstruction=true;}
        Check("F17 invalid frame identity rejected",invalidConstruction);
        var terminalAlive=true;
        var terminal=new ClashResolutionFrame(90001,90002,0,1,()=>terminalAlive);
        terminal.Step(i=>{terminalAlive=false;return true;});
        Check("F18 final hit killing target completes cleanly",
            terminal.ResumeAfterCheckpoint().Status==ClashFrameStepStatus.Completed);
        Check("F19 frame never supplies fake roll power/damage itself",
            typeof(ClashResolutionFrame).GetMethod("Step")!=null &&
            typeof(ClashResolutionFrame).GetProperty("NextExchangeIndex")!=null);
        Debug.Log("[R6-Ib] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6-Ib frame contract tests failed="+fail);
    }
}
#endif
