#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R6ReactionActionExecutionVerification20261030
{
    private sealed class ProbeSkill : NormalSkill
    {
        public ProbeSkill(string name,Character owner,BattleEvent events)
        {
            SkillName=name;BasePower=1;Resolver=new DiceResolver(1,1);
            Initialize(owner,events);
        }
        public override void Execute(BattleAction action){}
    }

    private sealed class TracePresenter : IBattleActionPresentation
    {
        public readonly List<long> Shown=new List<long>();
        public IEnumerator PlayClash(ClashResultContext r)
        {
            if(r?.FirstAction!=null)Shown.Add(r.FirstAction.ActionId);
            yield break;
        }
        public IEnumerator PlayStandalone(BattleAction a){yield break;}
    }

    private static void Drive(IEnumerator routine)
    {
        var stack=new Stack<IEnumerator>();
        stack.Push(routine);
        int steps=0;
        while(stack.Count>0)
        {
            if(++steps>10000)
                throw new InvalidOperationException("Coroutine test step guard exceeded.");
            var current=stack.Peek();
            if(!current.MoveNext()){stack.Pop();continue;}
            if(current.Current is IEnumerator nested)stack.Push(nested);
        }
    }

    private static ActionSlot Slot(Character owner,Character target,
        long id,string name,BattleContext context)
    {
        return new ActionSlot
        {
            ActionId=id,Owner=owner,TargetCharacter=target,
            TargetPart=target.GetTargetPoints(false).FirstOrDefault(p=>p.IsValid).Part,
            Part=owner.GetTargetPoints(false).FirstOrDefault(p=>p.IsValid).Part,
            Phase=ActionPhase.COMBAT,SlotId="R6-TEST-"+id,
            Skill=new ProbeSkill(name,owner,context._battleEvent),
            ResourceCostCommitted=false
        };
    }

    private static ReactionRequest Request(long id,long parent,
        long ownerId,string name,ReactionTimingCheckpoint cp,long cause=901)
    {
        return new ReactionRequest(id,parent,parent,cause,parent,ownerId,
            name,cp,priority:0,sequence:id);
    }

    [MenuItem("Game System Verification/R6-Ia - Live Reaction Checkpoints (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)
            throw new InvalidOperationException("Play Mode required.");
        var manager=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var context=manager?.BattleContext;
        var actor=context?.Player;
        var target=context?.Enemies?.FirstOrDefault(x=>x!=null && !x.IsDead &&
            !x.UsesBodyParts);
        if(actor==null || target==null || context.Services?.ClashManager==null)
            throw new InvalidOperationException("Battle Test Scene runtime roster required.");

        int pass=0,fail=0;
        void Check(string key,bool good,string detail="")
        {
            if(good){pass++;Debug.Log("[R6-Exec] PASS "+key+" "+detail);}
            else {fail++;Debug.LogError("[R6-Exec] FAIL "+key+" "+detail);}
        }

        long actorId=R4LiveSlotQuery.ActorRuntimeId(actor);
        var scheduler=new BattleReactionScheduler();
        var registry=new R6ReactionActionRegistry();
        var presenter=new TracePresenter();
        var resolver=new ActionResolver(context,context.Services.ClashManager,
            null,presenter);
        resolver.EnableR6ReactionExecution(scheduler,registry);

        var main=Slot(actor,target,976010,"R6-MAIN",context);
        var react1=Slot(actor,target,976011,"R6-REACT-A",context);
        var react2=Slot(actor,target,976012,"R6-REACT-B",context);
        var req1=Request(971,main.ActionId,actorId,"R6-REACT-A",
            ReactionTimingCheckpoint.AfterClashCompleted);
        var req2=Request(972,main.ActionId,actorId,"R6-REACT-B",
            ReactionTimingCheckpoint.AfterActionCompleted,cause:902);
        Check("E01 opt-in request and reaction action explicitly bound",
            registry.TryRegister(req1,react1,out var _) &&
            registry.TryRegister(req2,react2,out var _) &&
            scheduler.TrySchedule(req1) && scheduler.TrySchedule(req2));
        var bad=Slot(actor,target,main.ActionId,"R6-REACT-A",context);
        Check("E02 reaction may not reuse source ActionId",
            !registry.TryRegister(Request(973,main.ActionId,actorId,
                "R6-REACT-A",ReactionTimingCheckpoint.AfterActionCompleted),
                bad,out var _));

        var queue=new ActionExecutionQueue();
        queue.ClashQueue.Enqueue(new ClashPair(main));
        int hpBefore=target.CurrentHP;
        Drive(resolver.Resolve(queue));
        Check("E03 main then AfterClash then AfterAction presented",
            presenter.Shown.SequenceEqual(new long[]{main.ActionId,react1.ActionId,react2.ActionId}),
            "order="+string.Join(",",presenter.Shown));
        Check("E04 independent ActionIds recorded by coordinator",
            resolver.ReactionExecutionTrace.Count==2 &&
            resolver.ReactionExecutionTrace.All(x=>x.Executed) &&
            resolver.ReactionExecutionTrace[0].ExecutedActionId==react1.ActionId &&
            resolver.ReactionExecutionTrace[1].ExecutedActionId==react2.ActionId);
        Check("E05 both checkpoint requests drained, one shot bindings used",
            scheduler.PendingCount==0 && registry.PendingBindings==0 &&
            scheduler.DispatchTrace.Count==2);
        Check("E06 actual ClashManager changed or preserved HP safely",
            target.CurrentHP<=hpBefore && target.CurrentHP>=0 &&
            queue.ClashQueue.Count==0,
            "hp="+hpBefore+"->"+target.CurrentHP);

        var noBind=Request(974,main.ActionId,actorId,"UNBOUND",
            ReactionTimingCheckpoint.AfterActionCompleted,cause:903);
        scheduler.TrySchedule(noBind);
        var nextMain=Slot(actor,target,976013,"R6-NEXT",context);
        var nextQueue=new ActionExecutionQueue();
        nextQueue.ClashQueue.Enqueue(new ClashPair(nextMain));
        Drive(resolver.Resolve(nextQueue));
        Check("E07 unbound request never fabricates an attack",
            resolver.ReactionExecutionTrace.Count==3 &&
            !resolver.ReactionExecutionTrace[2].Executed &&
            resolver.ReactionExecutionTrace[2].Diagnostic.Contains("No explicit") &&
            presenter.Shown.Last()==nextMain.ActionId);
        Check("E08 no recursive game resolution inside enqueue",
            scheduler.DispatchTrace.Count==3 && registry.PendingBindings==0);

        resolver.DisableR6ReactionExecution();
        var last=Slot(actor,target,976014,"R6-DISABLED",context);
        var disabledQueue=new ActionExecutionQueue();
        disabledQueue.ClashQueue.Enqueue(new ClashPair(last));
        scheduler.TrySchedule(Request(975,last.ActionId,actorId,
            "R6-NOT-AUTO",ReactionTimingCheckpoint.AfterClashCompleted,cause:904));
        Drive(resolver.Resolve(disabledQueue));
        Check("E09 disabled path leaves request queued",
            scheduler.PendingCount==1 && resolver.ReactionExecutionTrace.Count==0);
        Check("E10 no automatic Hifumi reaction registry or subscriptions added",
            context.Services.ActionResolver!=resolver &&
            scheduler.PendingCount==1);

        Check("E11 actual BattleRuntimeFactory registered empty reaction services",
            context.Services.ReactionScheduler!=null &&
            context.Services.ReactionActionRegistry!=null &&
            context.Services.ActionResolver!=null &&
            context.Services.ReactionScheduler.PendingCount==0);
        var repeatedId=Slot(actor,target,react1.ActionId,"R6-REACT-A",context);
        Check("E12 previously used reaction ActionId cannot be bound twice",
            !registry.TryRegister(Request(976,last.ActionId,actorId,
                "R6-REACT-A",ReactionTimingCheckpoint.AfterActionCompleted,
                cause:905),repeatedId,out var _));

        var tamperRegistry=new R6ReactionActionRegistry();
        var trusted=Request(977,last.ActionId,actorId,"R6-REACT-A",
            ReactionTimingCheckpoint.AfterActionCompleted,cause:906);
        var tamperSlot=Slot(actor,target,976017,"R6-REACT-A",context);
        bool registered=tamperRegistry.TryRegister(trusted,tamperSlot,out var _);
        var changedCause=Request(977,last.ActionId,actorId,"R6-REACT-A",
            ReactionTimingCheckpoint.AfterActionCompleted,cause:999);
        bool rejectedTamper=!tamperRegistry.TryTake(changedCause,
            out var _,out var provenanceProblem);
        Check("E13 changed cause rejected against registered request",
            registered && rejectedTamper &&
            provenanceProblem.Contains("provenance"));

        var prepaid=Slot(actor,target,976018,"R6-REACT-A",context);
        prepaid.ResourceCostCommitted=true;
        Check("E14 forged prepaid reaction cost rejected",
            !tamperRegistry.TryRegister(Request(978,last.ActionId,actorId,
                "R6-REACT-A",ReactionTimingCheckpoint.AfterActionCompleted,
                cause:907),prepaid,out var _));

        Debug.Log("[R6-Exec] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6 checkpoint integration test failed "+fail);
    }
}
#endif
