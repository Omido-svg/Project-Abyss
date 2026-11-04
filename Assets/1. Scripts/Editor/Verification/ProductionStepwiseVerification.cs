#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// Real BattleManager.ActionResolver (not a test-only ActionResolver) executes
/// three exchanges, a one-shot reaction inserted after exchange 1, then resumes
/// the same original frame. Presentation suppressed only for deterministic
/// in-editor automated simulation, not in production.
/// </summary>
public static class ProductionStepwiseVerification
{
    private sealed class SkillProbe : NormalSkill
    {
        private readonly int rolls;
        public SkillProbe(string label,Character owner,BattleContext battle,
            int power,int count)
        {
            rolls=count;SkillName=label;BasePower=power;
            Resolver=new DiceResolver(1,1);
            Initialize(owner,battle._battleEvent);
        }
        public override int ExchangeRollCount => rolls;
        public override void Execute(BattleAction action){}
    }

    private static ActionSlot Slot(Character owner,Character opponent,
        long id,string name,int power,int rolls,BattleContext battle)=>
        new ActionSlot
        {
            ActionId=id,Owner=owner,TargetCharacter=opponent,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            TargetPart=opponent.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Phase=ActionPhase.COMBAT,SlotId="R6-PRODUCTION-"+id,
            Skill=new SkillProbe(name,owner,battle,power,rolls),
            ResourceCostCommitted=true
        };

    private static void Drive(IEnumerator routine)
    {
        var stack=new Stack<IEnumerator>();stack.Push(routine);
        int max=25000;
        while(stack.Count>0)
        {
            if(--max<=0)throw new InvalidOperationException("R6 production coroutine stalled");
            var top=stack.Peek();
            if(!top.MoveNext()){stack.Pop();continue;}
            if(top.Current is IEnumerator next)stack.Push(next);
        }
    }

    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode required.");
        var mgr=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var battle=mgr?.BattleContext;
        var services=battle?.Services;
        var enemy=battle?.Enemies?.FirstOrDefault(e=>e!=null&&!e.IsDead&&!e.UsesBodyParts);
        if(battle?.Player==null||enemy==null||services?.ActionResolver==null)
            throw new InvalidOperationException("Production battle manager unavailable.");
        int pass=0,fail=0;
        void Check(string id,bool ok,string description="")
        {
            if(ok){pass++;Debug.Log("[R6-Production] PASS "+id+" | "+description);}
            else{fail++;Debug.LogError("[R6-Production] FAIL "+id+" | "+description);}
        }
        Check("P01 real scene startup enables production stepwise path",
            services.ActionResolver.IsR6StepwiseActive &&
            services.ReactionScheduler!=null &&
            services.ReactionActionRegistry!=null &&
            services.ReactionLifetimeRegistry!=null,
            "ActionResolver bound by BattleRuntimeFactory");

        var resolver=services.ActionResolver;
        var requests=services.ReactionScheduler;
        var registry=services.ReactionActionRegistry;
        var lifetime=services.ReactionLifetimeRegistry;
        const long firstId=1176101,secondId=1176102,reactionId=1176103;
        var attack=Slot(battle.Player,enemy,firstId,"R6-PROD-ROOT",7,3,battle);
        var oppose=Slot(enemy,battle.Player,secondId,"R6-PROD-OTHER",1,3,battle);
        var counter=Slot(enemy,battle.Player,reactionId,"R6-PROD-COUNTER",1,1,battle);
        counter.ResourceCostCommitted=false;
        var request=new ReactionRequest(1176110,firstId,firstId,1176120,
            firstId,R4LiveSlotQuery.ActorRuntimeId(enemy),"R6-PROD-COUNTER",
            ReactionTimingCheckpoint.AfterExchangeSettled,0,1176130);
        bool bound=false,queued=false, lifetimeBound=false;
        int exchanges=0, resumes=0;
        var observed=new List<int>();
        Action<ClashExchangeResult> observer=exchange=>
        {
            if(exchange==null)return;
            exchanges++;observed.Add(exchange.ExchangeIndex);
            if(exchange.ExchangeIndex==2 &&
               resolver.ReactionExecutionTrace.Any(x=>x.Request.RequestId==request.RequestId &&
                    x.Executed&&x.ExecutedActionId==reactionId))
                resumes++;
            if(exchange.ExchangeIndex==1)
            {
                bound=registry.TryRegister(request,counter,out _);
                var decision=requests.Queue(request);
                queued=decision.Accepted;
                lifetimeBound=queued &&
                    lifetime.TryBindAccepted(decision,
                        new AbilityLifetimeRules(oncePerTurn:true),
                        new AbilityLifetimeEvent(17,firstId,1,1,request.CauseEventId),
                        R3ReactionConsumeOn.Started,null,out _);
            }
        };
        int oldHp=enemy.CurrentHP;
        resolver.R6ExchangeSettled+=observer;
        BattleSimulationRuntime.BeginBatch(true);
        try
        {
            var queue=new ActionExecutionQueue();
            queue.ClashQueue.Enqueue(new ClashPair(attack,oppose));
            Drive(resolver.Resolve(queue));
        }
        finally
        {
            BattleSimulationRuntime.EndBatch();
            resolver.R6ExchangeSettled-=observer;
        }
        Check("P02 all original exchanges performed in original order",
            exchanges==3 && observed.SequenceEqual(new[]{0,1,2}));
        Check("P03 second exchange schedules independent reaction once",
            bound&&queued&&lifetimeBound);
        Check("P04 original resumes AFTER real reaction",
            resumes==1&&
            resolver.ReactionExecutionTrace.Any(x=>x.Request.RequestId==request.RequestId &&
                x.Executed && x.ExecutedActionId==reactionId));
        Check("P05 execution queue and one-shot binding drained",
            requests.PendingCount==0&&registry.PendingBindings==0);
        Check("P06 registered Started lifetime committed exactly one activation",
            lifetime.TryGetMemory(R4LiveSlotQuery.ActorRuntimeId(enemy),
                "R6-PROD-COUNTER",out var stored) &&
            stored.SuccessfulCauseCount==1);
        var kinds=resolver.R6StepTrace.Select(x=>x.Kind).ToArray();
        var expected=new[]{
            R6StepTraceKind.ExchangeCommitted,R6StepTraceKind.ExchangeVisualCompleted,
            R6StepTraceKind.ExchangeCommitted,R6StepTraceKind.ExchangeVisualCompleted,
            R6StepTraceKind.ReactionCommitted,R6StepTraceKind.ReactionVisualCompleted,
            R6StepTraceKind.ExchangeCommitted,R6StepTraceKind.ExchangeVisualCompleted,
            R6StepTraceKind.OriginalActionCompleted
        };
        Check("P07 real production trace contains no replayed root visual",
            kinds.SequenceEqual(expected),
            string.Join("|",kinds.Select(k=>k.ToString())));
        Check("P08 R6 actual battle state only receives permitted damage",
            enemy.CurrentHP<=oldHp &&
            resolver.IsR6StepwiseActive &&
            !BattleSimulationRuntime.IsBatchSimulation);
        var nextRequest=new ReactionRequest(1176111,firstId,firstId,1176121,
            firstId,R4LiveSlotQuery.ActorRuntimeId(enemy),"R6-PROD-COUNTER",
            ReactionTimingCheckpoint.AfterExchangeSettled,0,1176131);
        var nextDecision=requests.Queue(nextRequest);
        bool rejected=!lifetime.TryBindAccepted(nextDecision,
            new AbilityLifetimeRules(oncePerTurn:true),
            new AbilityLifetimeEvent(17,firstId,2,2,nextRequest.CauseEventId),
            R3ReactionConsumeOn.Started,null,out _);
        Check("P09 new RequestId cannot bypass same-turn cooldown",
            nextDecision.Accepted&&rejected&&!lifetime.MayStart(nextRequest,out _));
        // R6 production memory/queue is battle-local; this test does not
        // intentionally kill the real runtime or save its scene.
        services.ReactionScheduler.ClearForBattleEnd();
        services.ReactionActionRegistry.ClearForBattleEnd();
        services.ReactionLifetimeRegistry.ClearForBattleEnd();
        Check("P10 empty testing queues, production mode remains enabled",
            requests.PendingCount==0 && registry.PendingBindings==0 &&
            lifetime.PendingBindings==0 && resolver.IsR6StepwiseActive);
        Debug.Log("[R6-Production] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6 production integration failed "+fail);
    }
}
#endif
