#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class AbilityInteractionVerification
{
    public static string LastReport { get; private set; }
    private sealed class Match : IAbilityTriggerMatcher
    {
        public bool Matches(in AbilityEventContext context)=>true;
    }
    private sealed class Target : ICombatTargetSelector
    {
        public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext context)=>new[]{new CombatTargetRef(2,"arm","")};
    }
    private sealed class Backend : ICombatCommandBackend
    {
        public int Calls;
        public Func<CombatCommandEnvelope,CombatCommandReceipt> Handler;
        public bool CanApply(CombatCommandKind kind)=>true;
        public CombatCommandReceipt Apply(CombatCommandEnvelope request)
        {
            Calls++; return Handler?.Invoke(request)??new CombatCommandReceipt(CombatCommandOutcome.Applied,1,0,true,"");
        }
    }
    [MenuItem("Tools/Project Abyss/Verification/Ability Interaction Contracts")]
    public static void Run()
    {
        int pass=0,fail=0;
        var lines=new List<string>();
        void Check(string id,bool valid){lines.Add((valid?"PASS ":"FAIL ")+id);if(valid)pass++;else fail++;}
        bool Throws(Action action){try{action();return false;}catch(ArgumentException){return true;}}
        var ability=new AbilityIdentity("fixture.rule",1,"slot",0);
        var parent=new EffectCause(100,1,2,ability.DefinitionId,1,0,EffectOriginKind.DirectRoll);
        var direct=parent.Derive(3,EffectOriginKind.DirectRoll);
        Check("cause/direct parent",direct.IsDirectResultOf(parent));
        Check("cause/trap excluded",!parent.Derive(4,EffectOriginKind.Trap).IsDirectResultOf(parent));
        Check("cause/root alone insufficient",!new EffectCause(100,99,5,ability.DefinitionId,1,0,EffectOriginKind.DirectRoll).IsDirectResultOf(parent));
        Check("cause/unknown legacy excluded",!default(EffectCause).IsDirectResultOf(parent));
        Check("cause/reused event refused",Throws(()=>parent.Derive(2,EffectOriginKind.DirectRoll)));
        var sequence=new CombatEventSequence();
        long boundary=sequence.Boundary(100,AbilityEventKind.RollUsing,0);
        Check("events/reobserve stable",boundary==sequence.Boundary(100,AbilityEventKind.RollUsing,0));
        Check("events/new roll distinct",boundary!=sequence.Boundary(100,AbilityEventKind.RollUsing,1));
        var scheduler=new BattleReactionScheduler();
        ReactionRequest Request(long id,ReactionTimingCheckpoint at,long cause=2,int priority=0,long enqueue=0)=>
            new ReactionRequest(id,100,100,cause,100,1,"fixture.rule",at,priority,enqueue);
        foreach(ReactionTimingCheckpoint checkpoint in Enum.GetValues(typeof(ReactionTimingCheckpoint)))
        {
            var request=Request((int)checkpoint+1,checkpoint);
            Check("scheduler/accept "+checkpoint,scheduler.Queue(request).Accepted);
            Check("scheduler/dispatch "+checkpoint,scheduler.TryDequeue(checkpoint,out var dispatched)&&dispatched.RequestId==request.RequestId);
        }
        Check("scheduler/replay rejected",scheduler.Queue(Request(1,ReactionTimingCheckpoint.AfterExchangeSettled)).Status==ReactionScheduleStatus.Duplicate);
        Check("scheduler/invalid checkpoint rejected",scheduler.Queue(Request(10,(ReactionTimingCheckpoint)999)).Status==ReactionScheduleStatus.UnsupportedCheckpoint);
        Check("scheduler/same root distinct event",scheduler.Queue(Request(11,ReactionTimingCheckpoint.AfterDamageResultCommitted,7)).Accepted &&
            scheduler.Queue(Request(12,ReactionTimingCheckpoint.AfterDamageResultCommitted,8)).Accepted);
        Check("scheduler/nested explicit policy",scheduler.Queue(new ReactionRequest(13,101,100,9,100,1,"fixture.rule",ReactionTimingCheckpoint.AfterActionCompleted,0,0),true,2).Accepted);
        var limited=new BattleReactionScheduler{MaxPendingRequests=1};
        limited.Queue(Request(20,ReactionTimingCheckpoint.AfterActionCompleted));
        Check("scheduler/guard diagnosed",limited.Queue(Request(21,ReactionTimingCheckpoint.AfterActionCompleted)).Status==ReactionScheduleStatus.GuardExceeded && limited.LastDecision.Diagnostic.Contains("cause="));
        var facts=new Dictionary<string,long>{{"mark",0}};
        var skillFacts=new SkillExecutionFacts(100,new CombatTargetRef(2,"arm",""),"weapon-A",facts);
        facts["mark"]=44;
        Check("snapshot/action start immutable",skillFacts.TryGet("mark",out var mark)&&mark==0);
        var key=new FactKey(2,ConditionScope.Part,ConditionFact.KeywordStack,"mark","arm");
        var observations=new Dictionary<FactKey,int>{{key,3}};
        var snapshot=new ConditionSnapshot(new SubjectRef(1),new SubjectRef(2,"arm"),new SubjectRef(2,"arm"),new SubjectRef(1),0,observations);
        observations[key]=99;
        Check("snapshot/condition deep copy",snapshot.TryGet(new SubjectRef(2,"arm"),ConditionScope.Part,ConditionFact.KeywordStack,"mark",out int observed)&&observed==3);
        AbilityExecutionFrame Frame(AbilitySnapshotPolicy policy=AbilitySnapshotPolicy.LiveAtTrigger)=>new AbilityExecutionFrame(ability,AbilityEventKind.RollUsing,100,parent,
            new CombatTargetRef(1,"","slot"),new CombatTargetRef(2,"arm",""),new CombatTargetRef(2,"arm",""),
            new AbilityEventFacts(1,true,true,false,true),false,policy,snapshot);
        var instance=new AbilityInstance(new AbilityDefinitionRef(ability.DefinitionId,"fixture-guid"),ability);
        var runner=new AbilityRunner();
        var tree=CombatConditionNode.All(CombatConditionNode.Leaf(new CombatEventFactCondition(CombatEventFact.RollSucceeded)),
            CombatConditionNode.Not(CombatConditionNode.Leaf(new CombatEventFactCondition(CombatEventFact.ClashOnly))));
        var testFrame=Frame();
        var plan=runner.PlanWithFrame(instance,testFrame,new Match(),tree,null,new Target(),new IEffectOperation[]{new ChangeResourceOperation(1)});
        Check("runner/read-only plan",plan.Commands.Count==1 && !plan.Result.HasApplied && instance.RuntimeState.SuccessfulActivationCount==0);
        Check("runner/frame propagated",ReferenceEquals(plan.Frame,testFrame));
        Check("runner/after-damage blocked before commit",runner.PlanWithFrame(instance,Frame(AbilitySnapshotPolicy.AfterCommittedDamage),new Match(),null,null,new Target(),new IEffectOperation[]{new ChangeResourceOperation(1)}).Commands.Count==0);
        Check("tree/empty any fails",!CombatConditionNode.Any().Evaluate(null,default));
        Check("rules/internal event blocked",new AbilityRuleMetadata().Validate(AbilityEventKind.DamageSettled).Count>0);
        Check("rules/after-damage phase validated",new AbilityRuleMetadata{Snapshot=AbilitySnapshotPolicy.AfterCommittedDamage}.Validate(AbilityEventKind.RollUsing).Count>0);
        Check("rules/pending explicitly blocked",new AbilityRuleMetadata{RequiresConfiguredValue=true,PendingDesignReason="fixture"}.Validate(AbilityEventKind.RollUsing).Any(x=>x.StartsWith("BLOCKED_BY_UNDECIDED_RULE")));
        var store=instance.RuntimeState.ScopedState;
        var weaponA=new AbilityScopeKey(AbilityOwnerScope.WeaponInstance,1,"weapon-A");
        var weaponB=new AbilityScopeKey(AbilityOwnerScope.WeaponInstance,1,"weapon-B");
        var turn=new AbilityScopeKey(AbilityOwnerScope.Turn,1,executionId:1);
        var run=new AbilityScopeKey(AbilityOwnerScope.Run,1);
        store.Set(weaponA,"power",3);store.Set(weaponB,"power",7);store.Set(turn,"uses",1);store.Set(run,"growth",9);
        Check("scope/weapon ownership separate",store.Get(weaponA,"power")==3&&store.Get(weaponB,"power")==7);
        instance.RuntimeState.ResetAt(AbilityLifetimeBoundary.Turn);
        Check("scope/turn release only",store.Get(turn,"uses")==0&&store.Get(weaponA,"power")==3);
        instance.RuntimeState.ResetAt(AbilityLifetimeBoundary.Battle);
        Check("scope/run retained",store.Get(weaponA,"power")==0&&store.Get(run,"growth")==9);
        instance.RuntimeState.ResetAt(AbilityLifetimeBoundary.Run);
        Check("scope/run reset",store.Count==0);
        Check("scope/weapon instance required",Throws(()=>new AbilityScopeKey(AbilityOwnerScope.WeaponInstance,1)));
        var evt=Frame().ToEventContext();
        CombatCommandEnvelope Command(string id,int priority=0)=>new CombatCommandEnvelope(id,ability,CombatCommandKind.ChangeResource,
            evt.ActionId,evt.CauseId,new CombatTargetRef(2,"arm",""),1,0,priority,0,0,parent);
        var applier=new CombatCommandApplyService();var backend=new Backend();
        var applied=applier.Apply(instance,evt,new[]{Command("late",1),Command("early",0)},backend);
        Check("commit/stable priority order",applied.Records[0].Request.CommandId=="early"&&applied.Records[1].Request.CommandId=="late");
        Check("commit/monotonic sequence",applied.Records[0].CommitSequence==1&&applied.Records[1].CommitSequence==2);
        Check("commit/replay suppressed",applier.Apply(instance,evt,new[]{Command("early")},backend).CommittedCount==0&&backend.Calls==2);
        instance.RuntimeState.ResetAt(AbilityLifetimeBoundary.Battle);
        var unknownBackend=new Backend();unknownBackend.Handler=c=>unknownBackend.Calls==2?throw new Exception("fixture unknown commit"):
            new CombatCommandReceipt(CombatCommandOutcome.Applied,1,0,true,"");
        var unsafeApply=new CombatCommandApplyService();
        var partial=unsafeApply.Apply(instance,evt,new[]{Command("partial-1"),Command("partial-2"),Command("partial-3")},unknownBackend);
        Check("commit/partial success preserved",partial.HasUnknownCommit&&partial.CommittedCount==1&&unknownBackend.Calls==2);
        Check("commit/unknown activation quarantined",unsafeApply.Apply(instance,evt,new[]{Command("new-id")},unknownBackend).CommittedCount==0&&unknownBackend.Calls==2);
        instance.RuntimeState.ResetAt(AbilityLifetimeBoundary.Battle);
        var reentryApply=new CombatCommandApplyService();var reentry=new Backend();
        reentry.Handler=c=>{reentryApply.Apply(instance,evt,new[]{Command("nested-id")},reentry);return default;};
        Check("commit/reentrant batch quarantined",reentryApply.Apply(instance,evt,new[]{Command("outer-id")},reentry).HasUnknownCommit&&reentry.Calls==1);
        var impact=new DamageContext{WasApplied=false,GuardAbsorbed=10,AppliedHpDamage=0,StaggerDamageApplied=2,WasWeakened=true,WasBroken=true};
        impact.Result=DamageResult.FromContext(impact);var values=CombatAppliedValues.FromDamage(impact);
        Check("result/absorbed hit distinct from miss",values.HitLanded&&values.AppliedHp==0&&values.AppliedStagger==2);
        Check("result/one part transition",values.PartTransition==PartTransitionOutcome.Broken);
        Check("result/missing impact is not hit",!CombatAppliedValues.FromDamage(null).HitLanded);

        IReadOnlyList<CombatCommandAppliedRecord> Replay(long actorOffset)
        {
            var identity=new AbilityIdentity("trace.fixture",1+actorOffset,"slot",0);
            var state=new AbilityInstance(new AbilityDefinitionRef(identity.DefinitionId,"trace-guid"),identity);
            var context=new AbilityEventContext(AbilityEventKind.RollUsing,100,2,1+actorOffset,0,false,true);
            var provenance=new EffectCause(100,1,2,identity.DefinitionId,1+actorOffset,0,EffectOriginKind.DirectRoll);
            var commands=new[]{new CombatCommandEnvelope("trace-B",identity,CombatCommandKind.ChangeResource,100,2,
                new CombatTargetRef(2+actorOffset,"arm",""),1,0,1,0,0,provenance),
                new CombatCommandEnvelope("trace-A",identity,CombatCommandKind.ChangeResource,100,2,
                new CombatTargetRef(2+actorOffset,"arm",""),1,0,0,0,0,provenance)};
            var rng=new System.Random(1112);var writer=new Backend();
            writer.Handler=c=>new CombatCommandReceipt(CombatCommandOutcome.Applied,rng.Next(1,10),0,true,"");
            return new CombatCommandApplyService().Apply(state,context,commands,writer).Records;
        }
        Check("trace/fresh seeded runs normalize runtime handles",CombatCommandTrace.Hash(1112,Replay(0))==CombatCommandTrace.Hash(1112,Replay(500)));
        Check("trace/seed changes hash",CombatCommandTrace.Hash(1112,applied.Records)!=CombatCommandTrace.Hash(1113,applied.Records));
        var runState=new RunProgressionState{Seed=1112};
        var stableRun=AbilityScopeKey.ForRun("fixture.owner");
        runState.AbilityState.Set(stableRun,"fixture.rule/growth",9);
        Check("scope/stable run owner",runState.AbilityState.Get(AbilityScopeKey.ForRun("fixture.owner"),"fixture.rule/growth")==9);
        Check("scope/stable weapon across battles",runState.YujinWeaponInstanceId(YujinWeaponType.Baeku)==runState.YujinWeaponInstanceId(YujinWeaponType.Baeku) &&
            runState.YujinWeaponInstanceId(YujinWeaponType.Baeku)!=runState.YujinWeaponInstanceId(YujinWeaponType.Nakil));
        runState.Reset();Check("scope/run reset clears data",runState.AbilityState.Count==0);
        var fifo=new TurnStartEffectQueue();int committedReservations=0;
        fifo.Enqueue(()=>{committedReservations++;throw new Exception("fixture partial commit");});
        fifo.Enqueue(()=>committedReservations++);
        try{fifo.Drain();}catch(InvalidOperationException){}
        Check("reservation/partial commit faults",fifo.IsFaulted&&fifo.LastFailedSequence==1&&fifo.Count==1&&committedReservations==1);
        try{fifo.Drain();}catch(InvalidOperationException){}
        Check("reservation/no silent continuation",fifo.Count==1&&committedReservations==1);fifo.Clear();
        int afterCancellation=0;
        fifo.Enqueue(()=>fifo.Clear());fifo.Enqueue(()=>afterCancellation++);fifo.Drain();
        Check("reservation/battle-end cancellation during drain",fifo.Count==0&&!fifo.IsFaulted&&afterCancellation==0);
        VerifyNativeData(Check);
        LastReport="PASS="+pass+" FAIL="+fail+"\n"+string.Join("\n",lines);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/AbilityInteractionVerification.txt",LastReport);
        if(fail>0)Debug.LogError(LastReport);else Debug.Log(LastReport);
    }

    private static void VerifyNativeData(Action<string,bool> check)
    {
        var preview=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var objects=new List<UnityEngine.Object>();var random=UnityEngine.Random.state;
        var battle=new BattleContext{SuppressPresentation=true,RunProgression=new RunProgressionState{Seed=1112}};
        battle.Services=new BattleRuntimeServices();battle.EffectResolver=new BattleEffectResolver(battle);
        battle.Services.MomentumManager=new MomentumManager(battle);
        battle.Services.DamageManager=new DamageManager(battle,battle.Services.MomentumManager);
        Yujin Spawn()
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2. Data/TestEncounters/Prefabs/Yujin_Test.prefab"));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,preview);objects.Add(go);
            var actor=go.GetComponent<Yujin>();actor.Initialize(battle);return actor;
        }
        try
        {
            UnityEngine.Random.InitState(1112);var owner=Spawn();var target=Spawn();
            battle.Player=owner;battle.Enemies.Add(target);
            var definition=ScriptableObject.CreateInstance<SkillDefinition>();objects.Add(definition);
            definition.name="DataOnlyFixture";definition.ActionType=ActionType.NormalAttack;
            var serialized=new SerializedObject(definition);serialized.FindProperty("skillId").stringValue="fixture.data-only";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var skill=new DataNormalSkill(definition);skill.Initialize(owner,battle._battleEvent);
            var part=target.GetBodyPart(PartType.LEFT_HAND);
            var slot=new ActionSlot{ActionId=9001,Owner=owner,Part=owner.GetBodyPart(PartType.LEFT_HAND),Skill=skill,
                TargetCharacter=target,TargetPart=part,SlotId="fixture-slot",Phase=ActionPhase.COMBAT};
            var action=new BattleAction { Slot=slot };battle._battleEvent.RaiseActionStart(action);
            owner.YujinMechanic.ApplyMarkGain(target,part,1,action);
            var captured=owner.YujinMechanic.CurrentSkillFacts(action);
            check("integration/skill-start marks locked",captured!=null && captured.TryGet("yujin.mark.at_start",out var startMark)&&startMark==0);
            var gain=owner.YujinMechanic.ForceMarkIgnition(target,part,action);
            check("integration/mark burst direct parent",gain.Ignited&&owner.YujinMechanic.LastMarkIgnitionCause.IsDirectResultOf(gain.SourceCause));
            var graph=ScriptableObject.CreateInstance<NativeMixedAbilityDefinition>();objects.Add(graph);
            graph.SchemaVersion=2;graph.StableId=definition.SkillId;graph.SourceAssetGuid="fixture-guid";graph.SourceAssetName=definition.name;
            graph.Operations=new[]{new MixedOperationEntry{Kind=MixedOperationKind.ApplyBleeding,Trigger=AbilityEventKind.RollUsing,
                EntryIndex=0,StackAmount=3,Duration=2,Condition=MixedAbilityConditionKind.RollSucceeded}};
            check("integration/new data definition valid",graph.IsValid);
            SkillEffectContext Context(bool success)=>new SkillEffectContext(action,definition,SkillEffectTiming.OnExecute,null,null,null,null,1,0,null,false,true,success);
            using(var engine=new NativeMixedAbilityEngine(battle))
            {
                check("integration/failed condition cannot roll or apply",!engine.Execute(graph,0,Context(false))&&!action.HasRolled&&
                    part.StatusEffects.OfType<Bleeding>().Sum(x=>x.Stack)==0);
                check("integration/data-only ability commits",engine.Execute(graph,0,Context(true))&&
                    part.StatusEffects.OfType<Bleeding>().Sum(x=>x.Stack)==3);
                check("integration/native frame cause recorded",engine.LastAppliedBatch?.Records[0].Request.SourceCause.IsKnown==true);
                check("integration/native duplicate suppressed",!engine.Execute(graph,0,Context(true))&&
                    part.StatusEffects.OfType<Bleeding>().Sum(x=>x.Stack)==3);
            }

            graph.Operations[0].Condition=MixedAbilityConditionKind.None;
            using(var binding=new MixedAbilityBinding(battle,new[]{graph}))
            {
                battle.Services.MixedAbilities=binding;
                var routed=new BattleAction{Slot=new ActionSlot{ActionId=9010,Owner=owner,Part=slot.Part,Skill=skill,
                    TargetCharacter=target,TargetPart=part,SlotId="fixture-data-slot"}};
                int before=part.StatusEffects.OfType<Bleeding>().Sum(x=>x.Stack);
                skill.NotifyRollUsing(routed,null,0,false,true);
                check("integration/data-only card uses production dispatcher",!definition.HasEffectEntries && binding.NativeCommits==1 &&
                    part.StatusEffects.OfType<Bleeding>().Sum(x=>x.Stack)==before+3);
                battle.Services.MixedAbilities=null;
            }
            battle._battleEvent.RaiseActionEnd(action);
            check("integration/skill snapshot released",owner.YujinMechanic.CurrentSkillFacts(action)==null);
            var yujin=owner.YujinMechanic;yujin.SetWeaponForVerification(YujinWeaponType.Baeku);
            yujin.TrySpendSense(yujin.Sense);
            DamageContext extraDamage=null;
            Action<DamageContext> captureExtra=c=>{if(c.Request.SourceCause.OriginKind==EffectOriginKind.ExtraHit)extraDamage=c;};
            battle._battleEvent.OnDamageResolved+=captureExtra;
            int extraHits;
            try{extraHits=yujin.ResolvePursuitExtraHitsForVerification(action,new[]{true},new[]{false});}
            finally{battle._battleEvent.OnDamageResolved-=captureExtra;}
            check("integration/extra activation free at zero sense",extraHits==1&&yujin.Sense==0);
            check("integration/extra damage face independent",extraDamage!=null&&!extraDamage.WasCritical&&extraDamage.RawPower==yujin.GetSkillPower(skill,false));
            check("integration/extra excludes original damage gate",extraDamage!=null&&!extraDamage.Request.SourceCause.IsDirectResultOf(action.GetEffectCause(AbilityEventKind.RollUsing,0)));
            yujin.GrantSense(1);
            int retried=yujin.ResolvePursuitExtraHitsForVerification(action,new[]{false,true},new[]{false});
            check("integration/extra retries spend on failure",retried==1&&yujin.Sense==0);

            action.RolledPower=5;var request=DamageRequest.FromAction(action,DamageType.SkillPart,false,false,false);
            var damage=battle.Services.DamageManager.ApplyDamageContext(request);
            check("integration/damage has direct roll cause",damage!=null&&damage.Request.SourceCause.IsDirectResultOf(action.GetEffectCause(AbilityEventKind.RollUsing,0)));
            var hifumiPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2. Data/TestEncounters/Prefabs/Hifumi_Test.prefab");
            var hifumiGo=UnityEngine.Object.Instantiate(hifumiPrefab);objects.Add(hifumiGo);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hifumiGo,preview);
            var hifumi=hifumiGo.GetComponent<Hifumi>();hifumi.Initialize(battle);
            var grant=hifumi.GetMechanic<HifumiMechanic>().GrantBone(int.MaxValue,BoneGrantReason.DirectCard);
            check("integration/bone direct receipt and cap",grant.Reason==BoneGrantReason.DirectCard&&grant.After==500&&grant.Applied==500-grant.Before);
            var bone=hifumi.GetMechanic<HifumiMechanic>();
            bone.SetBoneForVerification(100);bone.SetAdvanceBoneForVerification(50);
            check("integration/bone advance pays first",bone.ConsumeBone(60)&&bone.AdvanceBone==0&&bone.Bone==90);
            bone.GrantBloodPriceForTurn(4);bone.SetBoneForVerification(0);
            var directGrant=bone.GrantBone(10,BoneGrantReason.DirectCard);
            check("integration/blood price excludes direct grant",directGrant.Applied==10&&bone.Bone==10);
            bone.SetBoneForVerification(400);bone.SetAdvanceBoneForVerification(100);
            var counterDef=ScriptableObject.CreateInstance<SkillDefinition>();objects.Add(counterDef);
            counterDef.ActionType=ActionType.Duel;counterDef.name="CounterBoundaryFixture";
            var counterSerialized=new SerializedObject(counterDef);counterSerialized.FindProperty("skillId").stringValue=HifumiSkillIds.Goldan;
            counterSerialized.ApplyModifiedPropertiesWithoutUndo();
            var counterSkill=new HifumiDuelRuntimeSkill(counterDef);counterSkill.Initialize(hifumi,battle._battleEvent);
            var counterAction=new BattleAction{Slot=new ActionSlot{ActionId=9002,Owner=hifumi,Part=hifumi.GetBodyPart(PartType.LEFT_HAND),
                Skill=counterSkill,TargetCharacter=target,TargetPart=part}};
            int firstStoredBone=-1;int counterRolls=0;
            Action<BattleReactiveRollEvent> starting=e=>{if(e.Owner==hifumi){if(firstStoredBone<0)firstStoredBone=bone.Bone;counterRolls++;}};
            Action<BattleReactiveRollEvent> completed=e=>{if(e.Owner==hifumi)bone.GrantBone(17,BoneGrantReason.DirectCard);};
            battle._battleEvent.OnReactiveRollStarted+=starting;battle._battleEvent.OnReactiveRollResolved+=completed;
            try
            {
                typeof(HifumiMechanic).GetMethod("ResolveCounterSequence",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                    .Invoke(bone,new object[]{counterAction,target,part,1,true,0});
            }
            finally{battle._battleEvent.OnReactiveRollStarted-=starting;battle._battleEvent.OnReactiveRollResolved-=completed;}
            check("integration/goldan pays at counter start",counterRolls>0&&firstStoredBone==0&&bone.AdvanceBone==0);
            check("integration/goldan preserves newly earned bone",bone.Bone>=17);

        }
        finally
        {
            battle.TurnStartEffects.Clear();
            foreach(var obj in objects.AsEnumerable().Reverse())if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);UnityEngine.Random.state=random;
        }
    }
}
#endif
