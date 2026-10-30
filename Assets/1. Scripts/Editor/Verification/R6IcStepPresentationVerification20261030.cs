#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R6IcStepPresentationVerification20261030
{
    private sealed class SkillProbe : NormalSkill
    {
        public SkillProbe(string name,Character owner,BattleEvent events,int power,int rolls)
        {
            SkillName=name;
            BasePower=power;
            Resolver=new DiceResolver(1,1);
            Rolls=rolls;
            Initialize(owner,events);
        }
        private int Rolls {get;}
        public override int ExchangeRollCount=>Rolls;
        public override void Execute(BattleAction action){}
    }

    private sealed class TimedPresenter : IBattleActionPresentation,IBattleActionStepPresentation
    {
        public readonly List<string> Display=new List<string>();
        public int OriginalCompleteReplayCount;
        public long OriginalActionId;

        public IEnumerator PlayCommittedExchange(
            ClashResultContext root,ClashExchangeResult exchange)
        {
            if(root?.FirstAction==null||exchange==null)
                throw new InvalidOperationException("Incomplete committed step");
            Display.Add("step-"+exchange.ExchangeIndex+"-begin");
            // Deliberately yield a frame to prove the coordinator serializes
            // presentation before a reaction or a future original exchange.
            yield return null;
            Display.Add("step-"+exchange.ExchangeIndex+"-end");
        }
        public IEnumerator PlayClash(ClashResultContext root)
        {
            if(root?.FirstAction?.ActionId==OriginalActionId)
                OriginalCompleteReplayCount++;
            Display.Add("reaction-"+(root?.FirstAction?.ActionId??0));
            yield return null;
            Display.Add("reaction-done");
        }
        public IEnumerator PlayStandalone(BattleAction action){yield break;}
    }

    private sealed class LegacyPresenter : IBattleActionPresentation
    {
        public IEnumerator PlayClash(ClashResultContext root){yield break;}
        public IEnumerator PlayStandalone(BattleAction action){yield break;}
    }
    private static void Drive(IEnumerator root)
    {
        var stack=new Stack<IEnumerator>();
        stack.Push(root);
        int steps=0;
        while(stack.Count>0)
        {
            if(++steps>20000)throw new InvalidOperationException("Test coroutine overflow");
            var top=stack.Peek();
            if(!top.MoveNext()){stack.Pop();continue;}
            if(top.Current is IEnumerator nested)stack.Push(nested);
        }
    }
    private static ActionSlot Slot(Character owner,Character target,
        long id,string skill,int power,int rolls,BattleContext battle) =>
        new ActionSlot
        {
            Owner=owner,TargetCharacter=target,
            Part=owner.GetTargetPoints(false).FirstOrDefault(p=>p.IsValid).Part,
            TargetPart=target.GetTargetPoints(false).FirstOrDefault(p=>p.IsValid).Part,
            ActionId=id,SlotId="R6IC-"+id,Phase=ActionPhase.COMBAT,
            Skill=new SkillProbe(skill,owner,battle._battleEvent,power,rolls)
        };

    [MenuItem("Game System Verification/R6-Ic - Domain to Step Presentation (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var player=battle?.Player;
        var enemy=battle?.Enemies?.FirstOrDefault(c=>c!=null&&!c.IsDead&&!c.UsesBodyParts);
        if(player==null||enemy==null||battle.Services?.ClashManager==null)
            throw new InvalidOperationException("Live Battle Test Scene unavailable");
        int pass=0,fail=0;
        void Check(string id,bool ok,string info="")
        {
            if(ok){pass++;Debug.Log("[R6-Ic] PASS "+id+" "+info);}
            else{fail++;Debug.LogError("[R6-Ic] FAIL "+id+" "+info);}
        }
        var scheduling=new BattleReactionScheduler();
        var binding=new R6ReactionActionRegistry();
        var presentation=new TimedPresenter{OriginalActionId=996010};
        var runner=new ActionResolver(battle,battle.Services.ClashManager,null,presentation);
        runner.EnableR6ReactionExecution(scheduling,binding);
        runner.EnableR6StepwiseClashes();

        var original=Slot(player,enemy,996010,"R6-Ic-Root",6,3,battle);
        var defender=Slot(enemy,player,996011,"R6-Ic-Defender",1,3,battle);
        var response=Slot(enemy,player,996012,"R6-Ic-Response",2,1,battle);
        var request=new ReactionRequest(995011,original.ActionId,original.ActionId,
            995012,original.ActionId,R4LiveSlotQuery.ActorRuntimeId(enemy),
            "R6-Ic-Response",ReactionTimingCheckpoint.AfterExchangeSettled,0,995013);
        bool bound=false,queued=false;
        var domainOrder=new List<string>();
        runner.R6ExchangeSettled+=exchange=>
        {
            domainOrder.Add("commit-"+exchange.ExchangeIndex);
            if(exchange.ExchangeIndex==1)
            {
                bound=binding.TryRegister(request,response,out var _) ;
                queued=bound && scheduling.TrySchedule(request);
            }
        };
        var jobs=new ActionExecutionQueue();
        jobs.ClashQueue.Enqueue(new ClashPair(original,defender));
        Drive(runner.Resolve(jobs));
        Check("P01 reaction bound by second exchange",bound&&queued);
        Check("P02 original domain roll indices committed exactly once",
            domainOrder.SequenceEqual(new[]{"commit-0","commit-1","commit-2"}));
        Check("P03 displayed 0 1 reaction 2 with no overlap",
            presentation.Display.SequenceEqual(new[]{
                "step-0-begin","step-0-end",
                "step-1-begin","step-1-end",
                "reaction-996012","reaction-done",
                "step-2-begin","step-2-end"}),
            string.Join("|",presentation.Display));
        Check("P04 no duplicated final root PlayClash",
            presentation.OriginalCompleteReplayCount==0);
        var expected=new[]{
            R6StepTraceKind.ExchangeCommitted,
            R6StepTraceKind.ExchangeVisualCompleted,
            R6StepTraceKind.ExchangeCommitted,
            R6StepTraceKind.ExchangeVisualCompleted,
            R6StepTraceKind.ReactionCommitted,
            R6StepTraceKind.ReactionVisualCompleted,
            R6StepTraceKind.ExchangeCommitted,
            R6StepTraceKind.ExchangeVisualCompleted,
            R6StepTraceKind.OriginalActionCompleted
        };
        Check("P05 trace causal order matches visual sequence",
            runner.R6StepTrace.Select(x=>x.Kind).SequenceEqual(expected));
        Check("P06 trace exactly preserves root/cause for reaction",
            runner.R6StepTrace.Where(t=>
                t.Kind==R6StepTraceKind.ReactionCommitted ||
                t.Kind==R6StepTraceKind.ReactionVisualCompleted)
                .All(x=>x.ActionId==response.ActionId &&
                    x.RootActionId==original.ActionId &&
                    x.CauseEventId==request.CauseEventId));
        Check("P07 trace has consecutive unique sequence numbers",
            runner.R6StepTrace.Select((x,i)=>x.Sequence==i).All(x=>x));
        Check("P08 all three exchange indices reflected without replay",
            runner.R6StepTrace
                .Where(x=>x.Kind==R6StepTraceKind.ExchangeVisualCompleted)
                .Select(x=>x.ExchangeIndex)
                .SequenceEqual(new[]{0,1,2}));
        Check("P09 no pending reactions after presentation",
            scheduling.PendingCount==0 && binding.PendingBindings==0);

        bool rejectedLegacy=false;
        var unsupported=new ActionResolver(battle,battle.Services.ClashManager,
            null,new LegacyPresenter());
        unsupported.EnableR6ReactionExecution(
            new BattleReactionScheduler(),new R6ReactionActionRegistry());
        try{unsupported.EnableR6StepwiseClashes();}
        catch(InvalidOperationException){rejectedLegacy=true;}
        Check("P10 opt-in refuses legacy presenter before any combat",
            rejectedLegacy&&unsupported.R6StepTrace.Count==0);

        Check("P11 production presentation service implements step contract",
            new BattleActionPresentationService(null) is IBattleActionStepPresentation);

        Debug.Log("[R6-Ic] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6-Ic verification failed "+fail);
    }
}
#endif