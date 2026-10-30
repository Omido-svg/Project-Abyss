#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R6HifumiDeferredParityVerification20261030
{
    private sealed class ProbeSkill : NormalSkill
    {
        public ProbeSkill(Character owner,BattleEvent evt)
        {
            SkillName="R6-HIFUMI-CHECKPOINT-PUMP";
            BasePower=1;Resolver=new DiceResolver(1,1);
            Initialize(owner,evt);
        }
        public override void Execute(BattleAction action){}
    }
    private static void Drive(System.Collections.IEnumerator routine)
    {
        var stack=new System.Collections.Generic.Stack<System.Collections.IEnumerator>();
        stack.Push(routine);int guard=0;
        while(stack.Count>0)
        {
            if(++guard>10000)throw new InvalidOperationException("Coroutine test guard");
            var frame=stack.Peek();
            if(!frame.MoveNext()){stack.Pop();continue;}
            if(frame.Current is System.Collections.IEnumerator child)stack.Push(child);
        }
    }
    private static ClashResultContext FakeLoss(Hifumi h,Character enemy,long id,int losses=1)
    {
        var skill=h.RuntimeSkills.First(x=>x?.Definition?.SkillId==HifumiSkillIds.SmallChange);
        var own=new ActionSlot{
            ActionId=id,Owner=h,Part=h.BodyParts.First(x=>!x.IsBroken),
            TargetCharacter=enemy,TargetPart=null,Skill=skill,
            Phase=ActionPhase.COMBAT,SlotId="R6-HIFUMI-"+id};
        var foe=new ActionSlot{
            ActionId=id+1,Owner=enemy,TargetCharacter=h,
            TargetPart=h.BodyParts.First(x=>!x.IsBroken),
            Skill=enemy.RuntimeSkills.FirstOrDefault(),Phase=ActionPhase.COMBAT,
            SlotId="R6-FOE-"+id};
        var myAction=new BattleAction{Slot=own};
        var foeAction=new BattleAction{Slot=foe};
        var clash=new ClashResultContext{
            FirstAction=myAction,SecondAction=foeAction,IsClash=true,
            PairedExchangeCount=losses};
        for(int index=0;index<losses;index++)
            clash.Exchanges.Add(new ClashExchangeResult{
                ExchangeIndex=index,FirstAction=myAction,SecondAction=foeAction,
                WinnerAction=foeAction,LoserAction=myAction,IsTie=false,
                IsOneSided=false,WasCancelled=false});
        return clash;
    }

    [MenuItem("Game System Verification/R6 - Hifumi Deferred Counter Parity (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode required");
        var ctx=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var normals=ctx?.Enemies?.Where(e=>e!=null&&!e.UsesBodyParts&&!e.IsDead).Take(2).ToArray();
        var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2. Data/TestEncounters/Prefabs/Hifumi_Test.prefab");
        if(prefab==null||normals==null||normals.Length<2)
            throw new InvalidOperationException("Two normal enemies + Hifumi prefab required");
        int pass=0,fail=0;
        void Check(string name,bool yes,string note="")
        {
            if(yes){pass++;Debug.Log("[R6-Hifumi] PASS "+name+" "+note);}
            else{fail++;Debug.LogError("[R6-Hifumi] FAIL "+name+" "+note);}
        }
        var goA=UnityEngine.Object.Instantiate(prefab);
        var goB=UnityEngine.Object.Instantiate(prefab);
        goA.name="R6_HIFUMI_LEGACY_RUNTIME_ONLY";
        goB.name="R6_HIFUMI_DEFERRED_RUNTIME_ONLY";
        var hA=goA.GetComponent<Hifumi>();
        var hB=goB.GetComponent<Hifumi>();
        hA.Initialize(ctx);hB.Initialize(ctx);
        var legacyRolls=new System.Collections.Generic.List<string>();
        var deferredRolls=new System.Collections.Generic.List<string>();
        var multiLegacyRolls=new System.Collections.Generic.List<string>();
        var multiDeferredRolls=new System.Collections.Generic.List<string>();
        Action<BattleReactiveRollEvent> rollObserver=e=>
        {
            if(e==null)return;
            var signature=e.SequenceIndex+":"+e.SequenceCount+":"+
                e.Power+":"+e.SourceKind;
            if(e.SourceAction?.ActionId==996100)legacyRolls.Add(signature);
            if(e.SourceAction?.ActionId==996200)deferredRolls.Add(signature);
            if(e.SourceAction?.ActionId==996110)multiLegacyRolls.Add(signature);
            if(e.SourceAction?.ActionId==996210)multiDeferredRolls.Add(signature);
        };
        ctx._battleEvent.OnReactiveRollResolved += rollObserver;
        try
        {
            Check("H01 two runtime Hifumi instances are initialized",
                hA.IsInitialized&&hB.IsInitialized&&
                hA.HifumiMechanic.IsRegistered&&hB.HifumiMechanic.IsRegistered);
            Check("H02 new opt-in enables only deferred Hifumi",
                hB.HifumiMechanic.TryEnableR6DeferredCounters());
            int beforeA=normals[0].CurrentHP,beforeB=normals[1].CurrentHP;
            Check("H03 normal enemies start same HP",beforeA==beforeB);
            UnityEngine.Random.InitState(12819);
            var state=UnityEngine.Random.state;
            ctx._battleEvent.RaiseClashResolved(FakeLoss(hA,normals[0],996100));
            int damageLegacy=beforeA-normals[0].CurrentHP;
            Check("H04 legacy counterpart executes counter synchronously",
                ctx.Services.SpecialistReactionRegistry.PendingCount==0 &&
                damageLegacy>=0);
            UnityEngine.Random.state=state;
            ctx._battleEvent.RaiseClashResolved(FakeLoss(hB,normals[1],996200));
            Check("H05 deferred counterpart does NOT damage before checkpoint",
                normals[1].CurrentHP==beforeB &&
                ctx.Services.ReactionScheduler.PendingCount==1 &&
                ctx.Services.SpecialistReactionRegistry.PendingCount==1);
            Check("H18 reactive roll event not emitted before scheduled checkpoint",
                deferredRolls.Count==0);
            bool has=ctx.Services.ReactionScheduler.TryDequeue(
                ReactionTimingCheckpoint.AfterClashCompleted,out var req);
            Check("H06 real Hifumi request has exact owner/root provenance",
                has && req.RootActionId==996200 &&
                req.OwnerActorRuntimeId==R4LiveSlotQuery.ActorRuntimeId(hB));
            bool executed=has && ctx.Services.SpecialistReactionRegistry.TryExecute(
                req,out var _);
            int damageDeferred=beforeB-normals[1].CurrentHP;
            Check("H07 queued callback executes real original Hifumi sequence",
                executed && ctx.Services.SpecialistReactionRegistry.PendingCount==0);
            Check("H08 same random state yields same legacy/deferred HP effect",
                damageLegacy==damageDeferred,
                "legacy="+damageLegacy+" deferred="+damageDeferred);
            Check("H17 reactive roll event power/sequence parity",
                legacyRolls.Count>0 && legacyRolls.SequenceEqual(deferredRolls),
                "legacy="+string.Join("|",legacyRolls)+
                " deferred="+string.Join("|",deferredRolls));
            Check("H09 both Hifumi actors remain independent",
                hA.CurrentHP==hB.CurrentHP &&
                hA.HifumiMechanic.Bone==hB.HifumiMechanic.Bone);
            int after=normals[1].CurrentHP;
            Check("H10 same request cannot apply again",
                !ctx.Services.SpecialistReactionRegistry.TryExecute(req,out var _) &&
                normals[1].CurrentHP==after);
            Check("H11 scheduler has no orphaned request",
                ctx.Services.ReactionScheduler.PendingCount==0);
            // Exercise the real ActionResolver checkpoint, not just manual
            // scheduler/registry dequeue.
            ctx._battleEvent.RaiseClashResolved(FakeLoss(hB,normals[1],996300));
            Check("H12 second genuine Hifumi request is queued",
                ctx.Services.ReactionScheduler.PendingCount==1 &&
                ctx.Services.SpecialistReactionRegistry.PendingCount==1);
            var coordinator=new ActionResolver(
                ctx,ctx.Services.ClashManager,null,null);
            coordinator.EnableR6ReactionExecution(
                ctx.Services.ReactionScheduler,ctx.Services.ReactionActionRegistry);
            coordinator.EnableR6SpecialistReactions(
                ctx.Services.SpecialistReactionRegistry);
            var probe=new ActionSlot{
                ActionId=996350,Owner=ctx.Player,TargetCharacter=normals[1],
                Part=ctx.Player.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
                TargetPart=null,Skill=new ProbeSkill(ctx.Player,ctx._battleEvent),
                Phase=ActionPhase.COMBAT,SlotId="R6-HIFUMI-PUMP"};
            var queue=new ActionExecutionQueue();
            queue.ClashQueue.Enqueue(new ClashPair(probe));
            Drive(coordinator.Resolve(queue));
            Check("H13 ActionResolver consumed deferred Hifumi request",
                ctx.Services.ReactionScheduler.PendingCount==0 &&
                ctx.Services.SpecialistReactionRegistry.PendingCount==0);
            Check("H14 specialist trace records execution without fake ActionId",
                coordinator.ReactionExecutionTrace.Count==1 &&
                coordinator.ReactionExecutionTrace[0].Executed &&
                coordinator.ReactionExecutionTrace[0].ExecutedActionId==0);
            Check("H15 no synthetic normal action registration",
                ctx.Services.ReactionActionRegistry.PendingBindings==0);
            hB.HifumiMechanic.DisableR6DeferredCounters();
            Check("H16 explicit disable retains legacy event registration",
                hB.HifumiMechanic.IsRegistered);

            // Two separate lost exchanges create two real Chinchiro counter
            // attempts; prove deterministic sequence/data parity beyond a
            // single reaction. Original specialist code is untouched.
            Check("H19 deferred mode can be enabled again explicitly",
                hB.HifumiMechanic.TryEnableR6DeferredCounters());
            int multiStartA=normals[0].CurrentHP;
            int multiStartB=normals[1].CurrentHP;
            UnityEngine.Random.InitState(22829);
            var multiRandomState=UnityEngine.Random.state;
            ctx._battleEvent.RaiseClashResolved(
                FakeLoss(hA,normals[0],996110,losses:2));
            int legacyMultiDamage=multiStartA-normals[0].CurrentHP;
            UnityEngine.Random.state=multiRandomState;
            ctx._battleEvent.RaiseClashResolved(
                FakeLoss(hB,normals[1],996210,losses:2));
            Check("H20 multi-roll deferred target still untouched before checkpoint",
                normals[1].CurrentHP==multiStartB &&
                ctx.Services.ReactionScheduler.PendingCount==1);
            bool multiQueued=ctx.Services.ReactionScheduler.TryDequeue(
                ReactionTimingCheckpoint.AfterClashCompleted,out var multiReq);
            bool multiApplied=multiQueued && ctx.Services.SpecialistReactionRegistry.TryExecute(
                multiReq,out var _);
            int deferredMultiDamage=multiStartB-normals[1].CurrentHP;
            Check("H21 two-counter request applied only once",multiApplied &&
                ctx.Services.ReactionScheduler.PendingCount==0 &&
                ctx.Services.SpecialistReactionRegistry.PendingCount==0);
            Check("H22 two-roll specialist target damage parity",
                legacyMultiDamage==deferredMultiDamage,
                "legacy="+legacyMultiDamage+" deferred="+deferredMultiDamage);
            Check("H23 paired reactive roll event sequence/power parity",
                multiLegacyRolls.Count==2 &&
                multiLegacyRolls.SequenceEqual(multiDeferredRolls),
                string.Join("|",multiLegacyRolls));
            Check("H24 specialist multi-roll cannot replay",
                !ctx.Services.SpecialistReactionRegistry.TryExecute(multiReq,out var _));
            hB.HifumiMechanic.DisableR6DeferredCounters();
        }
        finally
        {
            ctx._battleEvent.OnReactiveRollResolved -= rollObserver;
            UnityEngine.Object.Destroy(goA);
            UnityEngine.Object.Destroy(goB);
        }
        Debug.Log("[R6-Hifumi] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6 Hifumi parity failed "+fail);
    }
}
#endif