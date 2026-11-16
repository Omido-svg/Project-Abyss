#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>Production-path regression cases for extension contracts. Fixture amounts are not game balance defaults.</summary>
public static class AbilityExtensibilityVerification
{
    public static string LastReport { get; private set; }
    private sealed class Fixture : IDisposable
    {
        public readonly BattleContext Battle;
        public readonly List<UnityEngine.Object> Objects=new();
        private readonly UnityEngine.SceneManagement.Scene preview;
        private readonly UnityEngine.Random.State random;
        private long next=300000;
        public Yujin Player,Enemy;
        public BodyPart Part=>Enemy.GetBodyPart(PartType.LEFT_HAND);
        public Fixture(int seed=1112,RunProgressionState run=null)
        {
            random=UnityEngine.Random.state;UnityEngine.Random.InitState(seed);preview=EditorSceneManager.NewPreviewScene();
            Battle=new BattleContext{SuppressPresentation=true,RunProgression=run??new RunProgressionState{Seed=seed}};
            Battle.Services=new BattleRuntimeServices();Battle.EffectResolver=new BattleEffectResolver(Battle);
            Battle.Services.MomentumManager=new MomentumManager(Battle);Battle.Services.ActionManager=new ActionManager();
            Battle.Services.DamageManager=new DamageManager(Battle,Battle.Services.MomentumManager);
            Player=Spawn<Yujin>("Yujin_Test");Enemy=Spawn<Yujin>("Yujin_Test");Battle.Player=Player;Battle.Enemies.Add(Enemy);
        }
        public T Asset<T>() where T:ScriptableObject {var a=ScriptableObject.CreateInstance<T>();Objects.Add(a);return a;}
        public T Spawn<T>(string name) where T:Character
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2. Data/TestEncounters/Prefabs/"+name+".prefab"));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,preview);go.hideFlags=HideFlags.DontSave;Objects.Add(go);
            var actor=go.GetComponent<T>();var data=UnityEngine.Object.Instantiate(actor.Data);Objects.Add(data);
            actor.ConfigureAuthoringCore(data,actor.Data.CombatLoadout);actor.Initialize(Battle);return actor;
        }
        public SkillDefinition Skill(string id,ActionType type=ActionType.NormalAttack)
        {
            var d=Asset<SkillDefinition>();d.name=id;d.SkillName=id;d.ActionType=type;d.Color=SkillColor.Red;d.OverrideEnergyCost=true;d.EnergyCost=0;
            var so=new SerializedObject(d);so.FindProperty("skillId").stringValue=id;so.ApplyModifiedPropertiesWithoutUndo();return d;
        }
        public BattleAction Action(SkillDefinition definition,Character owner=null,Character target=null,BodyPart part=null)
        {
            owner??=Player;target??=Enemy;part??=Part;
            Skill skill=definition.ActionType==ActionType.Duel?(Skill)new DataDuelSkill(definition):new DataNormalSkill(definition);
            skill.Initialize(owner,Battle._battleEvent);
            return new BattleAction{Slot=new ActionSlot{ActionId=++next,Owner=owner,Part=owner.GetBodyPart(PartType.LEFT_HAND),Skill=skill,
                TargetCharacter=target,TargetPart=part,SlotId="fixture-"+next}};
        }
        public AbilityRuleSetDefinition Rules(SkillDefinition skill,params AbilityRuleData[] rules)
        {
            var d=Asset<AbilityRuleSetDefinition>();d.StableId=skill.SkillId;d.SourceAssetGuid="fixture/"+skill.SkillId;d.SourceAssetName=skill.name;d.Rules=rules;return d;
        }
        public AbilityExecutionFrame Frame(BattleAction a,AbilitySnapshotPolicy policy=AbilitySnapshotPolicy.LiveAtTrigger,AbilityEventKind kind=AbilityEventKind.RollUsing)=>
            Battle.Abilities.CaptureFrame(new AbilityIdentity(a.Skill.Definition.SkillId,LiveSlotQuery.ActorRuntimeId(a.Owner),"",0),a,
                new AbilityEventContext(kind,a.ActionId,0,LiveSlotQuery.ActorRuntimeId(a.Owner),a.CurrentRollIndex,a.DuelEffectsEligible,false),
                new AbilityRuleMetadata{Snapshot=policy,CardAuthored=false});
        public void Use(BattleAction action,int roll=0)
        {action.CurrentRollIndex=roll;action.Skill.NotifyRollUsing(action,null,roll,false,true);}
        public void Dispose()
        {
            Battle.TurnStartEffects.Clear();Battle.DisposeAbilities();
            foreach(var o in Objects.AsEnumerable().Reverse())if(o!=null)UnityEngine.Object.DestroyImmediate(o);
            EditorSceneManager.ClosePreviewScene(preview);UnityEngine.Random.state=random;
        }
    }
    private sealed class IndependentHandler : TargetedAbilityRuleHandler
    {
        public readonly List<AbilityExecutionFrame> Frames=new();
        public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
        {Frames.Add(c.Frame);c.Target.AddBlock(command.Operation.Amount);return Applied(command.Operation.Amount,"Independent extension");}
    }
    private static AbilityRuleData Rule(string id,string op,int amount=1,string target="actual",AbilityEventKind trigger=AbilityEventKind.RollUsing)=>
        new(){RuleId=id,Trigger=trigger,TargetSelectorId=target,Operations=new[]{new AbilityOperationData{TypeId=op,Amount=amount}}};
    [MenuItem("Tools/Project Abyss/Verification/Ability Extensibility")]
    public static void Run()
    {
        var lines=new List<string>();int pass=0,fail=0;
        void Check(string name,bool ok){lines.Add((ok?"PASS ":"FAIL ")+name);if(ok)pass++;else fail++;}
        void Case(string name,Action body){try{body();}catch(Exception e){Check(name+" / "+e.GetBaseException().Message,false);}}
        Case("serialized authoring assets",()=>
        {
            string folder="__AbilityVerification_"+Guid.NewGuid().ToString("N");string path="Assets/"+folder;
            AssetDatabase.CreateFolder("Assets",folder);
            try
            {
                var settings=ScriptableObject.CreateInstance<CombatModifierDefinition>();settings.Amount=7;
                AssetDatabase.CreateAsset(settings,path+"/Modifier.asset");
                Check("modifier asset has a serializable Unity script binding",MonoScript.FromScriptableObject(settings)?.GetClass()==typeof(CombatModifierDefinition));
                Resources.UnloadAsset(settings);var loaded=AssetDatabase.LoadAssetAtPath<CombatModifierDefinition>(path+"/Modifier.asset");
                Check("modifier authoring survives an asset reload",loaded!=null&&loaded.Amount==7);
                var graph=ScriptableObject.CreateInstance<AbilityRuleSetDefinition>();graph.Enabled=false;graph.StableId="fixture.serialized";
                AssetDatabase.CreateAsset(graph,path+"/Rules.asset");
                Check("rule-set asset has a serializable Unity script binding",MonoScript.FromScriptableObject(graph)?.GetClass()==typeof(AbilityRuleSetDefinition));
            }
            finally{AssetDatabase.DeleteAsset(path);}
        });
        Case("data and handler routes",()=>
        {
            using var f=new Fixture();var runtime=f.Battle.Abilities;
            var handler=new IndependentHandler();runtime.Registry.RegisterOperation("fixture.independent",handler);
            var skill=f.Skill("fixture.extension");var rule=Rule("block","fixture.independent",3);
            var graph=f.Rules(skill,rule);runtime.Register(graph);
            // Changes to SO authoring data after compilation must not change this battle's rules.
            rule.Operations[0].Amount=99;graph.StableId="mutated";graph.SourceAssetName="mutated";
            var action=f.Action(skill);int before=f.Part.Owner.RuntimeStatus.currentBlock;f.Use(action);
            Check("new handler runs through production Skill dispatcher",handler.Frames.Count==1&&f.Enemy.RuntimeStatus.currentBlock==before+3);
            Check("compiled authoring identity and values are frozen",handler.Frames[0].Ability.DefinitionId==skill.SkillId);
            f.Use(action);Check("same event replay has no second commit",handler.Frames.Count==1);
            f.Use(action,1);Check("new roll in same action is legal",handler.Frames.Count==2);
            Check("central trace contains registered handler cause",runtime.Trace.Count==2&&runtime.Trace.All(r=>r.Request.SourceCause.IsKnown));
            var invalid=f.Rules(f.Skill("fixture.invalid"),Rule("bad","fixture.missing"));
            Check("unknown handler fails authoring validation",runtime.Registry.Validate(invalid).Any(s=>s.Contains("Unregistered operation")));
            var cycle=new AbilityConditionData{Kind=CombatConditionNodeKind.All};cycle.Children=new[]{cycle};invalid.Rules[0].Condition=cycle;
            Check("condition cycle fails validation",runtime.Registry.Validate(invalid).Any(s=>s.Contains("Cyclic")));
        });
        Case("rule order, conditions and lifetimes",()=>
        {
            using var f=new Fixture();var r=f.Battle.Abilities;var skill=f.Skill("fixture.rule.order");
            var late=Rule("late","state.set",2,"self");late.Phase=2;late.Operations[0].Key="order";
            var early=Rule("early","state.set",1,"self");early.Phase=1;early.Operations[0].Key="order";
            r.Register(f.Rules(skill,late,early));var a=f.Action(skill);f.Use(a);
            Check("rule phase precedes array order",r.GetState(f.Frame(a),AbilityOwnerScope.Actor,"order")==2);
            var other=f.Skill("fixture.candidates");var handler=new IndependentHandler();r.Registry.RegisterOperation("fixture.candidate",handler);
            var candidate=Rule("candidate","fixture.candidate",1,"enemy_parts");candidate.Policy.ConditionPhase=CombatConditionPhase.CandidateTarget;
            candidate.Condition=new AbilityConditionData{Kind=CombatConditionNodeKind.All,Children=new[]{
                new AbilityConditionData{Predicate=new AbilityPredicateData{TypeId="fact",Subject=ConditionSubject.SelectedTarget,Scope=ConditionScope.Part,Fact=ConditionFact.KeywordStack,Key="yujin.mark",Value=1}},
                new AbilityConditionData{Kind=CombatConditionNodeKind.Not,Children=new[]{new AbilityConditionData{Predicate=new AbilityPredicateData{TypeId="fact",Subject=ConditionSubject.SelectedTarget,Scope=ConditionScope.Part,Fact=ConditionFact.KeywordStack,Key="yujin.mark",Value=2}}}}}};
            f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,1,a);r.Register(f.Rules(other,candidate));f.Use(f.Action(other));
            Check("All/Not evaluates each selected target after selection",handler.Frames.Count==1&&handler.Frames[0].ActualDamageTarget.ActorRuntimeId==LiveSlotQuery.ActorRuntimeId(f.Enemy));
            var cooldownSkill=f.Skill("fixture.cooldown");var cooldown=Rule("cooldown","fixture.candidate",1,"self");cooldown.Lifetime.OncePerTurn=true;cooldown.Lifetime.FullCooldownTurns=1;
            r.Register(f.Rules(cooldownSkill,cooldown));int before=handler.Frames.Count;f.Use(f.Action(cooldownSkill));f.Use(f.Action(cooldownSkill));
            Check("once per turn spans distinct skill actions",handler.Frames.Count==before+1);
            r.BeginTurn(2);f.Use(f.Action(cooldownSkill));Check("cooldown blocks a full intervening turn",handler.Frames.Count==before+1);
            r.BeginTurn(3);f.Use(f.Action(cooldownSkill));Check("cooldown becomes eligible after full turn elapses",handler.Frames.Count==before+2);
            var finalSkill=f.Skill("fixture.final");var first=Rule("first","state.set",1,"self",AbilityEventKind.TurnEnding);
            first.Operations[0].Key="value";first.Policy.CardAuthored=false;
            var second=Rule("second","fixture.candidate",1,"self",AbilityEventKind.TurnEnding);second.Policy.CardAuthored=false;second.Policy.Snapshot=AbilitySnapshotPolicy.TurnEndFinalSnapshot;
            second.Condition.Predicate=new AbilityPredicateData{TypeId="state.at_least",Key="value",Value=0,Comparison=FactComparison.Equals};
            var passive=f.Rules(finalSkill,first,second);passive.SourceKind=AbilityRuleSourceKind.Passive;r.BindPassive(passive,f.Player);int seen=handler.Frames.Count;
            r.Lifecycle(AbilityEventKind.TurnEnding);Check("final snapshot is frozen before earlier lifecycle commands",handler.Frames.Count==seen+1);
        });
        Case("start snapshots and growth",()=>
        {
            using var f=new Fixture();var y=f.Player.YujinMechanic;var runtime=f.Battle.Abilities;
            var settings=f.Asset<YujinGrowthRuleDefinition>();settings.UnmarkedAmount=2;settings.MarkedAmount=8;settings.Stage=1;
            var skill=f.Skill("fixture.growth");var rule=Rule("growth","yujin.growth");rule.Policy.Snapshot=AbilitySnapshotPolicy.LockAtActionStart;
            rule.Operations[0].Settings=settings;runtime.Register(f.Rules(skill,rule));
            var action=f.Action(skill);f.Battle._battleEvent.RaiseActionStart(action);settings.UnmarkedAmount=30;f.Use(action);f.Use(action,1);
            Check("later rolls use the unmarked start branch",y.GetMark(f.Enemy,f.Part)==4);
            var next=f.Action(skill);f.Battle._battleEvent.RaiseActionStart(next);f.Use(next);
            Check("new skill execution uses its own marked start branch",y.GetMark(f.Enemy,f.Part)==12);
            f.Battle._battleEvent.RaiseActionEnd(action);
            Check("finished action releases frozen boundaries",!runtime.Snapshots.TryGet(action.ActionId,AbilitySnapshotPolicy.LockAtActionStart,out _));
            y.TryConsumeMark(f.Enemy,f.Part,12);y.SetWeaponForVerification(YujinWeaponType.Nakil);
            y.MarkRules.NakilIgnitionDamage=4;y.MarkRules.ResonanceNakilExtraDamage=3;y.MarkRules.TrapLifetimeTurns=2;y.MarkRules.ExplosionDamagePerTrap=2;
            y.ApplyMarkGain(f.Enemy,f.Part,40,next);y.RegisterMarkDeadline(f.Enemy,f.Part,2,1,next);y.RegisterMarkTrap(f.Enemy,f.Part,2,next);
            int impacts=0;var causes=new List<EffectCause>();
            Action<DamageContext> capture=d=>{if(d.Attacker==f.Player){impacts++;causes.Add(d.Request.SourceCause);}};
            f.Battle._battleEvent.OnDamageResolved+=capture;
            YujinMarkGainResult gain;
            try{gain=y.ApplyGrowthMarkGain(f.Enemy,f.Part,8,next,next.GetEffectCause(AbilityEventKind.RollUsing,0),new YujinMarkGainPolicy(true,true,2));}
            finally{f.Battle._battleEvent.OnDamageResolved-=capture;}
            Check("growth preserves overflow plus configured reapplication",gain.Ignited&&y.GetMark(f.Enemy,f.Part)==6);
            Check("stage four repeats enhanced ignition but consumes traps once",impacts==3&&y.LastMarkBurst.Profile.Enhanced);
            var burst=y.LastMarkBurst;
            Check("burst retains exact direct mark gain parent",burst.Cause.IsDirectResultOf(burst.GainCause));
            Check("explosion has trap identity and installer contributors",causes.Any(c=>c.SourceAbilityId=="yujin.trap.explosion"&&
                f.Battle.EventSequence.Provenance.Contributors(c.ParentEventId).Count==1));
            Check("repeated effect is not a new direct mark burst",y.Growth.Progress(YujinGrowthAxis.Execution)==1);
            var unrelated=burst.Cause.Derive(f.Battle.EventSequence.Next(),EffectOriginKind.Trap);
            Check("trap correlation cannot claim direct reward",!unrelated.IsDirectResultOf(burst.GainCause));
            y.GrantSense(5);long progress=y.Growth.Progress(YujinGrowthAxis.Judgment);y.TrySpendSense(3);
            Check("growth records actual sense spent",y.Growth.Progress(YujinGrowthAxis.Judgment)==progress+3);
            Check("undecided progression threshold stays blocked",!y.Growth.TryAdvance(YujinGrowthAxis.Execution,0));
            Check("stage advance resets its separate gauge",y.Growth.TryAdvance(YujinGrowthAxis.Execution,1)&&y.Growth.Stage(YujinGrowthAxis.Execution)==2&&y.Growth.Progress(YujinGrowthAxis.Execution)==0);
        });
        Case("snapshot policies",()=>
        {
            using var f=new Fixture();var a=f.Action(f.Skill("fixture.boundaries"));var y=f.Player.YujinMechanic;var r=f.Battle.Abilities;
            int Mark(AbilityExecutionFrame frame){frame.Snapshot.TryGet(frame.Snapshot.OriginalTarget,ConditionScope.Part,ConditionFact.KeywordStack,"yujin.mark",out int n);return n;}
            r.CaptureBoundary(a,AbilitySnapshotPolicy.LockAtTargetReveal);y.ApplyMarkGain(f.Enemy,f.Part,2,a);
            r.ActionStarting(a);y.ApplyMarkGain(f.Enemy,f.Part,3,a);
            r.CaptureBoundary(a,AbilitySnapshotPolicy.LockAtReactionStart);y.ApplyMarkGain(f.Enemy,f.Part,4,a);
            r.CaptureBoundary(a,AbilitySnapshotPolicy.TurnEndFinalSnapshot);y.ApplyMarkGain(f.Enemy,f.Part,1,a);
            Check("target reveal reads original boundary",Mark(f.Frame(a,AbilitySnapshotPolicy.LockAtTargetReveal))==0);
            Check("action start reads original boundary",Mark(f.Frame(a,AbilitySnapshotPolicy.LockAtActionStart))==2);
            Check("reaction start reads original boundary",Mark(f.Frame(a,AbilitySnapshotPolicy.LockAtReactionStart))==5);
            Check("turn final reads named final boundary",Mark(f.Frame(a,AbilitySnapshotPolicy.TurnEndFinalSnapshot,AbilityEventKind.TurnEnding))==9);
            Check("live trigger reads current observations",Mark(f.Frame(a))==10);
            var frame=f.Frame(a);r.SetState(frame,AbilityOwnerScope.SkillExecution,"captured",4);r.CaptureBoundary(a,AbilitySnapshotPolicy.LockAtActionStart,true);
            r.SetState(frame,AbilityOwnerScope.SkillExecution,"captured",9);
            Check("frozen state view is copied with execution identity",f.Frame(a,AbilitySnapshotPolicy.LockAtActionStart).State.Get(AbilityOwnerScope.SkillExecution,"captured")==4);
        });
        Case("target locked schedules",()=>
        {
            using var f=new Fixture();var r=f.Battle.Abilities;var y=f.Player.YujinMechanic;var handler=new IndependentHandler();r.Registry.RegisterOperation("fixture.scheduled",handler);
            var skill=f.Skill("fixture.schedule");var delayed=Rule("later","fixture.scheduled",2);delayed.ScheduledOnly=true;delayed.Policy.Snapshot=AbilitySnapshotPolicy.LockAtScheduleCreation;
            var graph=f.Rules(skill,delayed);var settings=f.Asset<AbilityScheduleDefinition>();settings.Definition=graph;settings.RuleId="later";
            var now=Rule("queue","schedule.next_turn");now.Operations[0].Settings=settings;now.Lifetime.OncePerTurn=true;now.Lifetime.ConsumeOnSchedule=true;
            graph.Rules=new[]{now,delayed};r.Register(graph);delayed.Policy.Snapshot=AbilitySnapshotPolicy.AfterCommittedDamage;var action=f.Action(skill);y.ApplyMarkGain(f.Enemy,f.Part,3,action);
            f.Use(action);f.Use(action,1);
            Check("queued outcome is distinct from committed effect",r.Trace.Count(t=>t.Receipt.Outcome==CombatCommandOutcome.Scheduled)==1&&f.Battle.TurnStartEffects.Count==1);
            var targetAtCreation=f.Part;action.Slot.TargetPart=f.Enemy.GetBodyPart(PartType.HEAD);y.ApplyMarkGain(f.Enemy,targetAtCreation,2,action);
            r.ActionEnded(action);r.BeginTurn(2);f.Battle.TurnStartEffects.Drain();
            Check("reservation keeps selected part after source retargeting",handler.Frames.Count==1&&handler.Frames[0].ActualDamageTarget.PartId==targetAtCreation.PartId);
            var frame=handler.Frames[0];frame.Snapshot.TryGet(new SubjectRef(LiveSlotQuery.ActorRuntimeId(f.Enemy),targetAtCreation.PartId),ConditionScope.Part,ConditionFact.KeywordStack,"yujin.mark",out int mark);
            Check("reservation retains creation observations after action release",mark==3);
            Check("scheduled execution keeps original causal root",frame.Cause.RootActionId==action.ActionId&&frame.Cause.ParentEventId>0);
        });
        Case("scheduled snapshot policy matrix",()=>
        {
            foreach(var policy in new[]{AbilitySnapshotPolicy.LiveAtTrigger,AbilitySnapshotPolicy.LockAtActionStart,AbilitySnapshotPolicy.LockAtTargetReveal,
                AbilitySnapshotPolicy.LockAtScheduleCreation,AbilitySnapshotPolicy.LockAtReactionStart,AbilitySnapshotPolicy.AfterCommittedDamage,AbilitySnapshotPolicy.TurnEndFinalSnapshot})
            {
                using var f=new Fixture();var r=f.Battle.Abilities;var handler=new IndependentHandler();r.Registry.RegisterOperation("fixture.snapshot",handler);
                var skill=f.Skill("fixture.schedule."+policy);var rule=Rule("later","fixture.snapshot");rule.ScheduledOnly=true;rule.Policy.Snapshot=policy;rule.Policy.CardAuthored=false;
                if(policy==AbilitySnapshotPolicy.AfterCommittedDamage)rule.Trigger=AbilityEventKind.DamageSettled;
                if(policy==AbilitySnapshotPolicy.TurnEndFinalSnapshot)rule.Trigger=AbilityEventKind.TurnEnding;
                var graph=f.Rules(skill,rule);r.Register(graph);var a=f.Action(skill);var y=f.Player.YujinMechanic;
                y.ApplyMarkGain(f.Enemy,f.Part,2,a);r.CaptureBoundary(a,AbilitySnapshotPolicy.LockAtActionStart);r.CaptureBoundary(a,AbilitySnapshotPolicy.LockAtTargetReveal);
                r.CaptureBoundary(a,AbilitySnapshotPolicy.TurnEndFinalSnapshot);AbilityExecutionFrame source=f.Frame(a);
                if(policy==AbilitySnapshotPolicy.AfterCommittedDamage)
                {
                    a.RolledPower=1;var damage=f.Battle.Services.DamageManager.ApplyDamageContext(DamageRequest.FromAction(a,DamageType.SkillPart,false,false,false));
                    var context=new SkillEffectContext(a,skill,SkillEffectTiming.AfterDamage,null,damage,null,null,1,0,null,false,true,true);
                    source=r.CaptureFrame(new AbilityIdentity(skill.SkillId,LiveSlotQuery.ActorRuntimeId(f.Player),"",0),a,
                        new AbilityEventContext(AbilityEventKind.DamageSettled,a.ActionId,0,LiveSlotQuery.ActorRuntimeId(f.Player),0,true,true),
                        new AbilityRuleMetadata{Snapshot=policy,CardAuthored=false},context);
                }
                r.Schedule(graph,"later",source,BattleAbilityRuntime.Reference(f.Enemy,f.Part));y.ApplyMarkGain(f.Enemy,f.Part,3,a);r.ActionEnded(a);r.BeginTurn(2);f.Battle.TurnStartEffects.Drain();
                int mark=-1;if(handler.Frames.Count==1)handler.Frames[0].Snapshot.TryGet(new SubjectRef(LiveSlotQuery.ActorRuntimeId(f.Enemy),f.Part.PartId),ConditionScope.Part,ConditionFact.KeywordStack,"yujin.mark",out mark);
                int expected=policy==AbilitySnapshotPolicy.LiveAtTrigger||policy==AbilitySnapshotPolicy.LockAtReactionStart?5:2;
                Check("scheduled policy uses its named boundary / "+policy,handler.Frames.Count==1&&mark==expected);
            }
        });
        Case("weapon ownership",()=>
        {
            using var f=new Fixture();var y=f.Player.YujinMechanic;var r=f.Battle.Abilities;y.SetWeaponForVerification(YujinWeaponType.Baeku);
            Check("sharpen is configured explicitly",y.SharpenWeapon(3)&&y.SharpenBonus==0);r.BeginTurn(2);
            Check("sharpen activates next turn",y.SharpenBonus==3);y.SetWeaponForVerification(YujinWeaponType.Jeokseol);
            Check("weapon change does not transfer sharpen",y.SharpenBonus==0);y.SetWeaponForVerification(YujinWeaponType.Baeku);
            Check("original weapon retains sharpen",y.SharpenBonus==3);
            var a=f.Action(f.Skill("fixture.nakjeom"));var other=f.Action(f.Skill("fixture.other"),f.Enemy,f.Player,f.Player.GetBodyPart(PartType.LEFT_HAND));
            int before=y.GetMark(f.Enemy,f.Part);f.Battle._battleEvent.RaiseExchangeResolved(new ClashExchangeResult{FirstAction=a,SecondAction=other,WinnerAction=a,LoserAction=other});
            Check("weapon win applies sharpen to actual nakjeom",y.GetMark(f.Enemy,f.Part)==before+y.CurrentWeaponProfile.NormalMarkAmount+3);
            r.BeginTurn(3);Check("weapon bonus expires after its active turn",y.SharpenBonus==0);
        });
        Case("committed passive events",()=>
        {
            using var f=new Fixture();var r=f.Battle.Abilities;var handler=new IndependentHandler();r.Registry.RegisterOperation("fixture.observe",handler);
            var passive=f.Rules(f.Skill("fixture.passive"),Rule("damage","fixture.observe",1,"self",AbilityEventKind.DamageSettled),Rule("clash","fixture.observe",2,"self",AbilityEventKind.ClashCompleted));
            passive.SourceKind=AbilityRuleSourceKind.Passive;foreach(var rule in passive.Rules){rule.Policy.CardAuthored=false;rule.Policy.CausePolicy=AbilityCausePolicy.DerivedEvent;}
            passive.Rules[0].Policy.Snapshot=AbilitySnapshotPolicy.AfterCommittedDamage;r.BindPassive(passive,f.Enemy);
            var action=f.Action(f.Skill("fixture.impact"));action.RolledPower=4;
            f.Enemy.YujinMechanic.ApplyMarkGain(f.Player,f.Player.GetBodyPart(PartType.LEFT_HAND),2);
            var damage=f.Battle.Services.DamageManager.ApplyDamageContext(DamageRequest.FromAction(action,DamageType.SkillPart,false,true,true));
            Check("damage result publishes to owner-bound passive",handler.Frames.Count==1&&handler.Frames[0].Source.ActorRuntimeId==LiveSlotQuery.ActorRuntimeId(f.Enemy));
            var observer=handler.Frames[0];observer.Snapshot.TryGet(new SubjectRef(LiveSlotQuery.ActorRuntimeId(f.Player),f.Player.GetBodyPart(PartType.LEFT_HAND).PartId),ConditionScope.Part,ConditionFact.KeywordStack,"yujin.mark",out int observerMark);
            Check("committed passive snapshot uses its own mechanic facts",observerMark==2&&observer.WeaponInstanceId==f.Enemy.YujinMechanic.WeaponInstanceId);
            Check("damage event preserves committed target and damage ID",handler.Frames[0].DamageResolutionId==damage.Request.SourceCause.EventId&&handler.Frames[0].ActualDamageTarget.PartId==f.Part.PartId);
            var unknown=DamageRequest.Custom(DamageType.Direct,null,f.Enemy,null,1,1f,false,false,false);
            var unknownDamage=f.Battle.Services.DamageManager.ApplyDamageContext(unknown);
            Check("unknown origin remains unknown but observable",unknownDamage.Request.SourceCause.EventId>0&&!unknownDamage.Request.SourceCause.IsKnown&&handler.Frames.Count==2);
            f.Battle._battleEvent.RaiseClashResolved(new ClashResultContext{FirstAction=action,IsClash=true});
            Check("clash completion publishes to bound passive",handler.Frames.Count==3&&handler.Frames.Last().EventKind==AbilityEventKind.ClashCompleted);
        });
        Case("reservation generations and save boundary",()=>
        {
            using var f=new Fixture();var r=f.Battle.Abilities;var q=f.Battle.TurnStartEffects;var order=new List<int>();
            q.EnqueueLocked(f.Enemy,f.Part,()=>{order.Add(1);q.EnqueueLocked(f.Enemy,f.Part,()=>order.Add(3),dueTurn:3);},dueTurn:2);
            q.EnqueueLocked(f.Enemy,f.Part,()=>order.Add(2),dueTurn:2);
            q.Drain();Check("future reservations do not run early",order.Count==0&&q.Count==2);
            r.BeginTurn(2);q.Drain();q.Drain();Check("nested next-turn reservation survives repeated same-turn drains",order.SequenceEqual(new[]{1,2})&&q.Count==1);
            r.BeginTurn(3);q.Drain();Check("nested reservation executes on its own due turn",order.SequenceEqual(new[]{1,2,3}));
            var state=r.RunState;var owner=AbilityScopeKey.ForRun("player/yujin");state.Set(owner,"fixture/save",17);
            var snapshot=state.CaptureRunSnapshot();string json=JsonUtility.ToJson(snapshot);var restored=new AbilityStateStore();
            Check("run snapshot round trips through serializable format",restored.TryRestoreRunSnapshot(JsonUtility.FromJson<AbilityRunStateSnapshot>(json))&&restored.Get(owner,"fixture/save")==17);
            snapshot.Entries.Add(snapshot.Entries[0]);Check("corrupt save fails atomically",!restored.TryRestoreRunSnapshot(snapshot)&&restored.Get(owner,"fixture/save")==17);
            var reset=new RunProgressionState();reset.TryRestoreAbilityState(JsonUtility.FromJson<AbilityRunStateSnapshot>(json));reset.Reset();
            Check("new run clears all persistent ability progress",reset.AbilityState.Count==0);
            var a=f.Action(f.Skill("fixture.reserved.mark"));f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,40,a);
            f.Player.YujinMechanic.QueueMarkForNextTurn(f.Enemy,f.Part,4,a);a.CurrentRollIndex=2;r.BeginTurn(4);q.Drain();
            var burst=f.Player.YujinMechanic.LastMarkBurst;
            Check("reserved mark retains creation roll after mutable action advances",burst.Cause.SourceRollIndex==0&&burst.Cause.IsDirectResultOf(burst.GainCause));
        });
        Case("nested ignition provenance",()=>
        {
            using var f=new Fixture();var y=f.Player.YujinMechanic;var extra=f.Spawn<Yujin>("Yujin_Test");f.Battle.Enemies.Add(extra);
            var part=extra.GetBodyPart(PartType.LEFT_HAND);var a=f.Action(f.Skill("fixture.nested"));
            y.SetWeaponForVerification(YujinWeaponType.Nakil);y.MarkRules.NakilIgnitionDamage=1;y.MarkRules.TrapLifetimeTurns=2;y.MarkRules.ExplosionDamagePerTrap=1;
            y.RegisterMarkTrap(f.Enemy,f.Part,1,a);long outer=0;DamageContext explosion=null;bool nested=false;
            Action<DamageContext> callback=d=>
            {
                if(d.Attacker!=f.Player)return;
                if(!nested)
                {
                    nested=true;outer=d.Request.SourceCause.ParentEventId;
                    var cause=new EffectCause(a.ActionId,d.Request.SourceCause.EventId,f.Battle.EventSequence.Next(),"fixture.secondary.trap",LiveSlotQuery.ActorRuntimeId(f.Player),0,EffectOriginKind.Trap);
                    y.ApplyMarkGain(extra,part,44,a,cause);
                }
                if(d.Request.SourceCause.SourceAbilityId=="yujin.trap.explosion")explosion=d;
            };
            f.Battle._battleEvent.OnDamageResolved+=callback;
            try{y.ApplyMarkGain(f.Enemy,f.Part,44,a);}finally{f.Battle._battleEvent.OnDamageResolved-=callback;}
            bool found=explosion!=null&&f.Battle.EventSequence.Provenance.TryGet(explosion.Request.SourceCause.ParentEventId,out var trap)&&trap.ParentEventId==outer;
            Check("outer explosion keeps its own parent across nested mark ignition",nested&&found);
        });
        Case("counter-owned context",()=>
        {
            using var f=new Fixture();var h=f.Spawn<Hifumi>("Hifumi_Test");var bone=h.GetMechanic<HifumiMechanic>();f.Battle.Enemies.Add(h);
            bone.SetBoneForVerification(400);bone.SetAdvanceBoneForVerification(100);
            var rule=Rule("start","hifumi.bone.grant",17,"self",AbilityEventKind.CounterStarting);rule.Policy.CardAuthored=false;
            rule.Policy.Snapshot=AbilitySnapshotPolicy.LockAtReactionStart;rule.Policy.OwnerScope=AbilityOwnerScope.CounterExecution;
            rule.Condition.Predicate=new AbilityPredicateData{TypeId="fact",Subject=ConditionSubject.SourceActor,Scope=ConditionScope.Actor,Fact=ConditionFact.ResourceAmount,
                Key="counter.start/hifumi.bone",Value=400};
            var graph=f.Rules(f.Skill("fixture.counter.passive"),rule);graph.SourceKind=AbilityRuleSourceKind.Passive;f.Battle.Abilities.BindPassive(graph,h);
            var def=f.Skill(HifumiSkillIds.Goldan,ActionType.Duel);var skill=new HifumiDuelRuntimeSkill(def);skill.Initialize(h,f.Battle._battleEvent);
            var a=f.Action(def,h);a.Slot.Skill=skill;int atRoll=-1,tier=-1;bool bloom=false;long id=0;
            Action<BattleReactiveRollEvent> starting=e=>{if(e.Owner==h){atRoll=bone.Bone;tier=bone.CurrentCounterExecution.BoneTier;bloom=bone.CurrentCounterExecution.Bloom;id=bone.CurrentCounterExecution.ExecutionId;}};
            f.Battle._battleEvent.OnReactiveRollStarted+=starting;
            try{typeof(HifumiMechanic).GetMethod("ResolveCounterSequence",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bone,new object[]{a,f.Enemy,f.Part,1,true,0});}
            finally{f.Battle._battleEvent.OnReactiveRollStarted-=starting;}
            Check("counter condition reads start pool after payment",atRoll>=17&&id>0);
            Check("counter locks tier and bloom from starting resources",tier==4&&bloom);
            Check("counter end releases context and retains newly granted bone",bone.CurrentCounterExecution==null&&a.CounterExecution==null&&bone.Bone>=17);
        });
        Case("legacy status condition compatibility",()=>
        {
            using var f=new Fixture();f.Enemy.AddStatus(StatusEffectFactory.CreateCanonical0922(StatusEffectId.Weakness,2,1),f.Player);
            var skill=f.Skill("fixture.mixed.actor.status");var action=f.Action(skill);
            var graph=f.Asset<NativeMixedAbilityDefinition>();graph.SchemaVersion=2;graph.StableId=skill.SkillId;graph.SourceAssetGuid="fixture-mixed";graph.SourceAssetName=skill.name;
            graph.Operations=new[]{new MixedOperationEntry{Kind=MixedOperationKind.ApplyBleeding,Trigger=AbilityEventKind.RollUsing,EntryIndex=0,StackAmount=1,Duration=1,
                Condition=MixedAbilityConditionKind.TargetNumericStatusAtLeast,RequiredStatus=StatusEffectId.Weakness,RequiredNumericValue=2}};
            using var engine=new NativeMixedAbilityEngine(f.Battle);var context=new SkillEffectContext(action,skill,SkillEffectTiming.OnExecute,null,null,null,null,1,0,null,false,true,true);
            Check("native mixed part target also observes common actor statuses",engine.Execute(graph,0,context));
        });
        Case("independent combat trace replay",()=>
        {
            string Scenario()
            {
                using var f=new Fixture(7123);var skill=f.Skill("fixture.replay");var rule=Rule("resource","resource.block",3,"self");
                var graph=f.Rules(skill,rule);f.Battle.Abilities.Register(graph);var action=f.Action(skill);f.Use(action);f.Use(action,1);
                return f.Player.RuntimeStatus.currentBlock+"/"+CombatCommandTrace.Hash(7123,f.Battle.Abilities.Trace);
            }
            Check("fresh actors reproduce command order values and cause hash",Scenario()==Scenario());
        });
        Case("typed modifiers and run state",()=>
        {
            var run=new RunProgressionState{Seed=1112};
            using(var f=new Fixture(run:run))
            {
                var r=f.Battle.Abilities;var a=f.Action(f.Skill("fixture.modifier"));var frame=f.Frame(a);var self=r.Scope(frame,AbilityOwnerScope.Actor);
                r.Modifiers.Add(self,new CombatModifierSpec(CombatModifierStage.HpDamageDealt,CombatModifierKind.AddFlat,3,0,1),frame.Cause,1);
                r.Modifiers.Add(self,new CombatModifierSpec(CombatModifierStage.HpDamageDealt,CombatModifierKind.ReplaceBase,5,0,1),frame.Cause,1);
                Check("replacement precedes additive modifiers",r.Modifiers.Apply(CombatModifierStage.HpDamageDealt,f.Player,99,a)==8);
                r.Modifiers.Add(self,new CombatModifierSpec(CombatModifierStage.HpDamageDealt,CombatModifierKind.OverrideFinal,2,0,1),frame.Cause,1);
                Check("final override applies after additions",r.Modifiers.Apply(CombatModifierStage.HpDamageDealt,f.Player,99,a)==2);
                Check("modifier does not leak to another owner",r.Modifiers.Apply(CombatModifierStage.HpDamageDealt,f.Enemy,99,a)==99);
                r.BeginTurn(2);Check("modifier duration expires at turn boundary",r.Modifiers.Count==0);
                var settings=f.Asset<CombatModifierDefinition>();settings.Stage=CombatModifierStage.HpDamageTaken;settings.Kind=CombatModifierKind.AddFlat;settings.Amount=4;
                var rule=Rule("target.modifier","modifier.add");rule.Operations[0].Settings=settings;r.Register(f.Rules(a.Skill.Definition,rule));f.Use(a);
                Check("targeted modifier belongs to its target",r.Modifiers.Apply(CombatModifierStage.HpDamageTaken,f.Enemy,1)==5&&r.Modifiers.Apply(CombatModifierStage.HpDamageTaken,f.Player,1)==1);
                r.SetState(frame,AbilityOwnerScope.Run,"progress",9);f.Player.YujinMechanic.Growth.Record(YujinGrowthAxis.Execution,3);
            }
            using(var f=new Fixture(run:run))
            {
                var a=f.Action(f.Skill("fixture.modifier"));Check("stable run state survives actor recreation",f.Battle.Abilities.GetState(f.Frame(a),AbilityOwnerScope.Run,"progress")==9);
                Check("growth progression survives battle disposal",f.Player.YujinMechanic.Growth.Progress(YujinGrowthAxis.Execution)==3);
                Check("battle modifiers do not persist into next battle",f.Battle.Abilities.Modifiers.Count==0);
            }
        });
        Case("contagion and deterministic chains",()=>
        {
            string Scenario(int seed)
            {
                using var f=new Fixture(seed);var y=f.Player.YujinMechanic;var second=f.Spawn<Yujin>("Yujin_Test");f.Battle.Enemies.Add(second);
                var p1=second.GetBodyPart(PartType.LEFT_HAND);var p2=second.GetBodyPart(PartType.HEAD);var action=f.Action(f.Skill("fixture.contagion"));
                y.ApplyMarkGain(second,p1,43,action);y.ApplyMarkGain(second,p2,42,action);y.RegisterContagionTrap(f.Enemy,f.Part,1,action);
                y.ApplyMarkGain(f.Enemy,f.Part,44,action);
                Check("contagion considers all parts of another enemy",y.GetMark(second,p1)==0&&y.GetMark(second,p2)==43&&y.ContagionTrapCount(second,p2)==1);
                Check("chain can reinfect earlier actor under same root",y.ContagionTrapCount(f.Enemy,f.Part)>0||f.Enemy.BodyParts.Any(p=>y.ContagionTrapCount(f.Enemy,p)>0));
                var result=string.Join("/",f.Enemy.BodyParts.Select(p=>y.GetMark(f.Enemy,p)+":"+y.ContagionTrapCount(f.Enemy,p)))+"|"+
                    string.Join("/",second.BodyParts.Select(p=>y.GetMark(second,p)+":"+y.ContagionTrapCount(second,p)));
                f.Battle._battleEvent.RaiseTurnEnd(1);
                Check("derived contagion expires on creation turn",second.BodyParts.All(p=>y.ContagionTrapCount(second,p)==0)&&f.Enemy.BodyParts.All(p=>y.ContagionTrapCount(f.Enemy,p)==0));
                y.RegisterContagionTrap(second,p1,1,action);f.Battle._battleEvent.RaiseTurnEnd(2);
                Check("original contagion retains its independent second turn",y.ContagionTrapCount(second,p1)==1);f.Battle._battleEvent.RaiseTurnEnd(3);
                Check("original contagion expires after two settlements",y.ContagionTrapCount(second,p1)==0);
                return result;
            }
            var first=Scenario(12345);var second=Scenario(12345);Check("independent seeded executions reproduce tie choices",first==second);
            var queue=new AbilityEffectWorkQueue{ProcessingBudget=3};int count=0;var cause=new EffectCause(1,0,1,"fixture.loop",1,0,EffectOriginKind.Trap);
            Action loop=null;loop=()=>{count++;queue.Enqueue(cause,loop);};queue.Enqueue(cause,loop);bool fault=false;
            try{queue.Drain(99);}catch(InvalidOperationException){fault=true;}
            Check("technical guard reports root and seed without silently discarding work",fault&&queue.IsFaulted&&queue.Count==1&&count==3&&queue.Fault.Contains("root=1")&&queue.Fault.Contains("seed=99"));
        });
        LastReport="PASS="+pass+" FAIL="+fail+"\n"+string.Join("\n",lines);
        System.IO.Directory.CreateDirectory("Logs");System.IO.File.WriteAllText("Logs/AbilityExtensibilityVerification.txt",LastReport);
        if(fail>0)Debug.LogError(LastReport);else Debug.Log(LastReport);
    }
}
#endif
