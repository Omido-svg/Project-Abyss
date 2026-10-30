#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R6SpecialistLiveDispatcherVerification20261030
{
    private sealed class ProbeSkill : NormalSkill
    {
        public ProbeSkill(Character owner,BattleEvent evt,string name)
        {
            SkillName=name;BasePower=1;Resolver=new DiceResolver(1,1);
            Initialize(owner,evt);
        }
        public override void Execute(BattleAction action){}
    }
    private sealed class NoVisuals : IBattleActionPresentation
    {
        public IEnumerator PlayClash(ClashResultContext _) { yield break; }
        public IEnumerator PlayStandalone(BattleAction _) { yield break; }
    }
    private static void Drive(IEnumerator routine)
    {
        var stack=new Stack<IEnumerator>();stack.Push(routine);int guard=0;
        while(stack.Count>0)
        {
            if(++guard>10000)throw new InvalidOperationException("Coroutine runaway");
            var top=stack.Peek();
            if(!top.MoveNext()){stack.Pop();continue;}
            if(top.Current is IEnumerator nested)stack.Push(nested);
        }
    }
    private static ActionSlot Slot(BattleContext ctx,Character from,Character to,
        long id,string name) => new ActionSlot
        {
            ActionId=id,Owner=from,TargetCharacter=to,
            Part=from.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            TargetPart=to.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Skill=new ProbeSkill(from,ctx._battleEvent,name),
            Phase=ActionPhase.COMBAT,SlotId="R6-SPECIALIST-"+id
        };
    private static ReactionRequest Request(long id,long root,long ownerId,
        long cause,int priority,string skill,long chain)
        => new ReactionRequest(id,root,root,cause,chain,ownerId,skill,
            ReactionTimingCheckpoint.AfterClashCompleted,priority,id);

    [MenuItem("Game System Verification/R6-Ia - Specialist Coordinator Live (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var owner=battle?.Player;
        var target=battle?.Enemies?.FirstOrDefault(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts);
        if(owner==null||target==null||battle.Services?.ClashManager==null)
            throw new InvalidOperationException("Battle Test Scene runtime missing");
        var scheduler=new BattleReactionScheduler();
        var registry=new R6ReactionActionRegistry();
        var specialists=new R6SpecialistReactionRegistry();
        var coordinator=new ActionResolver(battle,battle.Services.ClashManager,
            null,new NoVisuals());
        coordinator.EnableR6ReactionExecution(scheduler,registry);
        coordinator.EnableR6SpecialistReactions(specialists);
        var sequence=new List<string>();
        int pass=0,fail=0;
        void Check(string id,bool ok,string extra="")
        {
            if(ok){pass++;Debug.Log("[R6-SpecLive] PASS "+id+" "+extra);}
            else{fail++;Debug.LogError("[R6-SpecLive] FAIL "+id+" "+extra);}
        }

        const long root=996400;
        var reqA=Request(scheduler.AllocateRequestId(),root,
            R4LiveSlotQuery.ActorRuntimeId(owner),996401,1,"SPECIAL-A",root);
        var reqB=Request(scheduler.AllocateRequestId(),root,
            R4LiveSlotQuery.ActorRuntimeId(owner),996402,0,"SPECIAL-B",root);
        int before=target.CurrentHP;
        bool bindA=specialists.TryRegister(reqA,()=>!target.IsDead,
            ()=>{sequence.Add("A");battle.ResolveDamageManager().ApplyDamageContext(
                DamageRequest.Direct(owner,target,1));},out var _);
        bool bindB=specialists.TryRegister(reqB,()=>!target.IsDead,
            ()=>{sequence.Add("B");battle.ResolveDamageManager().ApplyDamageContext(
                DamageRequest.Direct(owner,target,1));},out var _);
        Check("D01 specialist payloads registered without execution",
            bindA&&bindB&&sequence.Count==0);
        Check("D02 both queued at AfterClash",
            scheduler.TrySchedule(reqA)&&scheduler.TrySchedule(reqB)&&
            scheduler.PendingCount==2);
        var queue=new ActionExecutionQueue();
        queue.ClashQueue.Enqueue(new ClashPair(Slot(battle,owner,target,root,"S-ROOT")));
        Drive(coordinator.Resolve(queue));
        Check("D03 coordinator drained two specialist intents",
            scheduler.PendingCount==0&&specialists.PendingCount==0);
        Check("D04 stable priority dispatch B before A",
            sequence.SequenceEqual(new[]{"B","A"}));
        Check("D05 specialist delegate applied real DamageManager HP",
            target.CurrentHP<=before-2,"hp="+before+"->"+target.CurrentHP);
        Check("D06 action trace explicitly distinguishes special from ActionSlot",
            coordinator.ReactionExecutionTrace.Count==2 &&
            coordinator.ReactionExecutionTrace.All(x=>x.Executed &&
                x.ExecutedActionId==0));
        Check("D07 successful requests cannot be replayed",
            !specialists.TryExecute(reqA,out var _) &&
            !scheduler.TrySchedule(reqB));
        Check("D08 no ordinary reaction action was forged",registry.PendingBindings==0);

        // Exception after a partial specialist write poisons only this chain.
        const long poisonedRoot=996410;
        var bad=Request(scheduler.AllocateRequestId(),poisonedRoot,
            R4LiveSlotQuery.ActorRuntimeId(owner),996411,0,"BAD",poisonedRoot);
        var later=Request(scheduler.AllocateRequestId(),poisonedRoot,
            R4LiveSlotQuery.ActorRuntimeId(owner),996412,1,"BLOCKED-SKILL",poisonedRoot);
        int badCalls=0;
        specialists.TryRegister(bad,()=>true,()=>{
            badCalls++;
            battle.ResolveDamageManager().ApplyDamageContext(
                DamageRequest.Direct(owner,target,1));
            throw new InvalidOperationException("Applied but outcome indeterminate");
        },out var _);
        var sibling=Slot(battle,owner,target,996413,"BLOCKED-SKILL");
        bool ordinaryRegistered=registry.TryRegister(later,sibling,out var _);
        scheduler.TrySchedule(bad);
        scheduler.TrySchedule(later);
        int hpBeforeBad=target.CurrentHP;
        var second=new ActionExecutionQueue();
        second.ClashQueue.Enqueue(new ClashPair(
            Slot(battle,owner,target,poisonedRoot,"ROOT-POISON")));
        Drive(coordinator.Resolve(second));
        Check("D09 specialist can fail after applying real HP",badCalls==1 &&
            target.CurrentHP<hpBeforeBad);
        Check("D10 entire origin chain quarantined",specialists.IsChainQuarantined(poisonedRoot));
        Check("D11 ordinary sibling is skipped after unknown commit",
            ordinaryRegistered && coordinator.ReactionExecutionTrace.Count==4 &&
            !coordinator.ReactionExecutionTrace[3].Executed &&
            coordinator.ReactionExecutionTrace[3].Diagnostic.Contains("quarantined"));
        Check("D12 poison is never replayed",
            !specialists.TryExecute(bad,out var _) && badCalls==1);
        Check("D13 all queued intents consumed",scheduler.PendingCount==0);
        Check("D14 production default resolver was not rebound",
            battle.Services.ActionResolver!=coordinator);
        Check("D15 battle runtime has its own specialist registry",
            battle.Services.SpecialistReactionRegistry!=null &&
            battle.Services.SpecialistReactionRegistry!=specialists);

        Debug.Log("[R6-SpecLive] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6 Specialist live failed "+fail);
    }
}
#endif