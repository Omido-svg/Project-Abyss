using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>Battle-owned composition of the existing planner, command ledger, snapshots and registered handlers.</summary>
    public sealed class BattleAbilityRuntime : IDisposable
    {
        private sealed class Matcher : IAbilityTriggerMatcher { public bool Matches(in AbilityEventContext e)=>true; }
        private sealed class Selector : ICombatTargetSelector
        {
            private readonly IReadOnlyList<CombatTargetRef> targets;
            public Selector(IReadOnlyList<CombatTargetRef> targets){this.targets=targets;}
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext context)=>targets;
        }
        private sealed class Operation : IEffectOperation,IAbilityRuleCommandSink
        {
            private readonly AbilityRuleReadContext view;
            private readonly AbilityRuleRegistry registry;
            private readonly AbilityOperationSpec[] operations;
            public readonly List<AbilityRuleCommand> Planned=new();
            public Operation(AbilityRuleReadContext view,AbilityRuleRegistry registry,AbilityOperationSpec[] operations)
            {this.view=view;this.registry=registry;this.operations=operations;}
            public void Plan(IReadOnlyAbilityContext _,IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
            {
                foreach(var op in operations)registry.Operation(op.TypeId).Plan(view,op,targets,this);
                foreach(var command in Planned)sink.Enqueue(new CombatCommandIntent(nameof(CombatCommandKind.RegisteredEffect),command.Target,command.Operation.Amount));
            }
            public void Enqueue(AbilityRuleCommand command)
            {
                if(!command.Cause.IsSameEvent(view.Frame.Cause))throw new InvalidOperationException("A planned command cannot forge its cause.");
                Planned.Add(command);
            }
        }
        private sealed class Backend : ICombatCommandBackend
        {
            private readonly BattleAbilityRuntime runtime;
            private readonly AbilityExecutionFrame frame;
            private readonly string ruleId;
            private readonly Dictionary<string,AbilityRuleCommand> commands=new(StringComparer.Ordinal);
            public Backend(BattleAbilityRuntime runtime,AbilityExecutionFrame frame,string ruleId,
                IReadOnlyList<CombatCommandEnvelope> envelopes,IReadOnlyList<AbilityRuleCommand> planned)
            {this.runtime=runtime;this.frame=frame;this.ruleId=ruleId;for(int i=0;i<envelopes.Count;i++)commands.Add(envelopes[i].CommandId,planned[i]);}
            public bool CanApply(CombatCommandKind kind)=>kind==CombatCommandKind.RegisteredEffect;
            public CombatCommandReceipt Apply(CombatCommandEnvelope envelope)
            {
                if(!commands.Remove(envelope.CommandId,out var command))return CombatCommandReceipt.Reject("Missing registered command payload");
                if(!runtime.TryResolve(command.Target,out var target,out var part))
                    return CombatCommandReceipt.Reject("Captured target is unavailable",CombatFailureReason.TargetUnavailable);
                var source=runtime.ResolveActor(frame.Source.ActorRuntimeId);
                if(source==null)return CombatCommandReceipt.Reject("Source actor unavailable",CombatFailureReason.TargetUnavailable);
                var previous=runtime.CurrentCause;runtime.CurrentCause=command.Cause;
                runtime.commitDepth++;
                try{return runtime.Registry.Operation(command.Operation.TypeId).Commit(
                    new AbilityRuleCommitContext(runtime,frame,source,target,part,ruleId),command);}
                finally{runtime.commitDepth--;runtime.CurrentCause=previous;}
            }
        }
        private sealed class StateView : IReadOnlyScopedAbilityState
        {
            private readonly BattleAbilityRuntime runtime;
            private readonly AbilityExecutionFrame frame;
            private readonly CapturedAbilityBoundary frozen;
            public StateView(BattleAbilityRuntime runtime,AbilityExecutionFrame frame,CapturedAbilityBoundary frozen)
            {this.runtime=runtime;this.frame=frame;this.frozen=frozen;}
            public long Get(AbilityOwnerScope scope,string key)
            {
                var resolved=runtime.Scope(frame,scope);
                if(frozen!=null && scope!=AbilityOwnerScope.Run && frozen.Source.ActorRuntimeId==frame.Source.ActorRuntimeId)
                    resolved=new AbilityScopeKey(scope,frozen.Source.ActorRuntimeId,
                        scope==AbilityOwnerScope.WeaponInstance?frozen.WeaponInstanceId:frozen.Source.PartId,
                        scope==AbilityOwnerScope.SkillExecution?frozen.SkillExecutionId:scope==AbilityOwnerScope.CounterExecution?frozen.CounterExecutionId:
                        scope==AbilityOwnerScope.ClashExecution?frozen.ClashId:scope==AbilityOwnerScope.Turn?frozen.TurnId:0);
                string qualified=Qualify(frame.Ability.DefinitionId,key);
                if(frozen==null)return runtime.Store(scope).Get(resolved,qualified);
                var bank=scope==AbilityOwnerScope.Run?frozen.RunState:frozen.BattleState;
                return bank.TryGetValue((resolved,qualified),out var value)?value:0;
            }
        }
        private sealed class CompiledRule
        {
            public AbilityRuleSetDefinition Definition;
            public string Id,Selector,StableId,SourceGuid,SourceName;
            public AbilityRuleSourceKind SourceKind;
            public AbilityEventKind Trigger;
            public AbilityRuleMetadata Policy;
            public CombatConditionNode Condition;
            public AbilityOperationSpec[] Operations;
            public AbilityRuleLifetime Lifetime;
            public int Index,Phase,Priority;
            public bool DuelGate,ScheduledOnly;
        }
        private readonly Dictionary<AbilityRuleSetDefinition,HashSet<Character>> passiveOwners=new();
        public System.Random Random { get; }
        private readonly AbilityRunner runner=new();
        private readonly CombatCommandApplyService apply=new();
        private readonly Dictionary<AbilityRuleSetDefinition,CompiledRule[]> definitions=new();
        private readonly Dictionary<AbilityIdentity,AbilityInstance> instances=new();
        private readonly Dictionary<(AbilityIdentity,string),AbilityLifetimeMemory> lifetimes=new();
        private readonly Dictionary<long,CapturedAbilityBoundary> damageBoundaries=new();
        private readonly Dictionary<long,DamageContext> damageResults=new();
        private readonly Dictionary<long,BattleAction> actions=new();
        public CombatCommandBatchResult LastAppliedBatch { get; private set; }
        private readonly Dictionary<(long,string,AbilityEventKind,int,long),EffectCause> ruleCauses=new();
        private readonly Dictionary<long,Character> actors=new();
        private readonly Dictionary<long,string> persistentOwners=new();
        private readonly Dictionary<(long,string,long),bool> completed=new();
        private readonly List<CombatCommandAppliedRecord> trace=new();
        private int commitDepth;
        private long virtualActionId=2000000000000000L;
        private int syntheticTurn=1;
        public EffectCause CurrentCause { get; private set; }
        public BattleContext Battle { get; }
        public AbilityRuleRegistry Registry { get; }=new();
        public AbilitySnapshotStore Snapshots { get; }=new();
        public AbilityStateStore State { get; }=new();
        public AbilityStateStore RunState=>Battle.RunProgression?.AbilityState??localRunState;
        private readonly AbilityStateStore localRunState=new();
        public AbilityEffectWorkQueue Effects { get; }=new();
        public CombatModifierRegistry Modifiers { get; }=new();
        public IReadOnlyList<CombatCommandAppliedRecord> Trace=>trace.AsReadOnly();
        public int CurrentTurn=>Battle.Services?.TurnManager?.CurrentTurn>0?Battle.Services.TurnManager.CurrentTurn:syntheticTurn;
        public BattleAbilityRuntime(BattleContext battle)
        {
            Battle=battle??throw new ArgumentNullException(nameof(battle));
            Random=new System.Random(battle.RunProgression?.Seed??0);
            Battle.TurnStartEffects.CurrentTurn=()=>CurrentTurn;
            AbilityRuleModules.Configure(Registry);
            foreach(var definition in Resources.LoadAll<AbilityRuleSetDefinition>("ProjectAbyss/AbilityRules").Where(d=>d.Enabled))Register(definition);
        }
        public static CombatTargetRef Reference(Character actor,BodyPart part=null,string slot=null)=>
            new(LiveSlotQuery.ActorRuntimeId(actor),part?.PartId,slot);
        public BattleAction Action(long id)=>actions.TryGetValue(id,out var action)?action:null;
        public Character ResolveActor(long id)
        {
            if(actors.TryGetValue(id,out var known)&&known!=null)return known;
            var actor=Battle.AllCharacters.FirstOrDefault(a=>a!=null&&LiveSlotQuery.ActorRuntimeId(a)==id);
            if(actor!=null)actors[id]=actor;return actor;
        }
        public bool TryResolve(CombatTargetRef target,out Character actor,out BodyPart part)
        {
            actor=ResolveActor(target.ActorRuntimeId);part=null;
            if(actor==null||actor.IsDead)return false;
            if(string.IsNullOrEmpty(target.PartId))return true;
            part=actor.BodyParts?.FirstOrDefault(p=>p!=null&&p.PartId==target.PartId);
            return part!=null&&!part.IsBroken;
        }
        public void SetPersistentOwnerId(Character actor,string id)
        {if(actor==null||string.IsNullOrWhiteSpace(id))throw new ArgumentException("Stable owner ID required");persistentOwners[LiveSlotQuery.ActorRuntimeId(actor)]=id;}
        public AbilityScopeKey Scope(AbilityExecutionFrame frame,AbilityOwnerScope scope)
        {
            if(scope==AbilityOwnerScope.Run)
            {
                if(!persistentOwners.TryGetValue(frame.Source.ActorRuntimeId,out var owner))
                {
                    var actor=ResolveActor(frame.Source.ActorRuntimeId);
                    if(actor!=Battle.Player)throw new InvalidOperationException("Run state requires an explicitly registered persistent owner");
                    owner="player/"+(actor.Data?.CharacterName??"player");
                }
                return AbilityScopeKey.ForRun(owner);
            }
            return new AbilityScopeKey(scope,frame.Source.ActorRuntimeId,
                scope==AbilityOwnerScope.WeaponInstance?frame.WeaponInstanceId:frame.Source.PartId,
                scope==AbilityOwnerScope.SkillExecution?frame.SkillExecutionId:
                scope==AbilityOwnerScope.CounterExecution?frame.CounterExecutionId:
                scope==AbilityOwnerScope.ClashExecution?frame.ClashId:
                scope==AbilityOwnerScope.Turn?frame.TurnId:0);
        }
        public AbilityScopeKey ScopeForTarget(AbilityExecutionFrame frame,AbilityOwnerScope scope,CombatTargetRef target)
        {
            if(scope==AbilityOwnerScope.Run || scope==AbilityOwnerScope.SkillExecution || scope==AbilityOwnerScope.CounterExecution || scope==AbilityOwnerScope.ClashExecution)
            {if(target.ActorRuntimeId!=frame.Source.ActorRuntimeId)throw new ArgumentException("Execution/run modifiers must target their owner");return Scope(frame,scope);}
            string instance=target.PartId;
            if(scope==AbilityOwnerScope.WeaponInstance)
                instance=ResolveActor(target.ActorRuntimeId)?.Mechanics?.OfType<IAbilityExecutionScopeProvider>().FirstOrDefault()?.WeaponInstanceId;
            return new AbilityScopeKey(scope,target.ActorRuntimeId,instance,scope==AbilityOwnerScope.Turn?frame.TurnId:0);
        }
        public AbilityStateStore Store(AbilityOwnerScope scope)=>scope==AbilityOwnerScope.Run?RunState:State;
        public static string Qualify(string definition,string key)=>key?.StartsWith("@",StringComparison.Ordinal)==true?key.Substring(1):definition+"/"+key;
        public void SetState(AbilityExecutionFrame frame,AbilityOwnerScope scope,string key,long value)=>
            Store(scope).Set(Scope(frame,scope),Qualify(frame.Ability.DefinitionId,key),value);
        public long GetState(AbilityExecutionFrame frame,AbilityOwnerScope scope,string key)=>
            Store(scope).Get(Scope(frame,scope),Qualify(frame.Ability.DefinitionId,key));
        public void BeginTurn(int turn)
        {syntheticTurn=turn;State.ResetAt(AbilityLifetimeBoundary.Turn);Modifiers.BeginTurn(turn);}
        public void CaptureBoundary(BattleAction action,AbilitySnapshotPolicy policy,bool replace=false)
        {if(action?.Owner!=null)actors[LiveSlotQuery.ActorRuntimeId(action.Owner)]=action.Owner;Snapshots.Capture(this,action,policy,replace);}
        public void ActionStarting(BattleAction action)
        {
            CaptureBoundary(action,AbilitySnapshotPolicy.LockAtActionStart);
            if(action?.Slot?.ReactionCause.OriginKind==EffectOriginKind.Reaction)
                CaptureBoundary(action,AbilitySnapshotPolicy.LockAtReactionStart);
        }
        public void ActionEnded(BattleAction action)
        {if(action?.Owner==null||action.ActionId<=0)return;Snapshots.Release(action.ActionId);Modifiers.ReleaseExecution(action.ActionId,AbilityOwnerScope.SkillExecution);State.Release(new AbilityScopeKey(AbilityOwnerScope.SkillExecution,LiveSlotQuery.ActorRuntimeId(action.Owner),executionId:action.ActionId));}
        public AbilityExecutionFrame CaptureFrame(AbilityIdentity identity,BattleAction action,AbilityEventContext evt,
            AbilityRuleMetadata policy=null,SkillEffectContext context=null,CapturedAbilityBoundary scheduled=null,
            EffectCause observed=default,EffectCause parent=default,CombatTargetRef? capturedTarget=null)
        {
            if(action?.Owner==null)throw new ArgumentException("Execution requires an owner and action");
            actors[LiveSlotQuery.ActorRuntimeId(action.Owner)]=action.Owner;actions[action.ActionId]=action;
            var mode=policy?.Snapshot??AbilitySnapshotPolicy.LiveAtTrigger;
            var target=context?.DamageContext!=null?Reference(context.DamageContext.Target,context.DamageContext.TargetPart):
                capturedTarget??Reference(context?.Target??action.Target,context?.TargetPart??action.TargetPart);
            var damage=context?.DamageContext;
            if(observed.EventId<=0&&damage?.Request.SourceCause.EventId>0)observed=damage.Request.SourceCause;
            CapturedAbilityBoundary boundary=scheduled;
            if(boundary==null)
            {
                if(mode==AbilitySnapshotPolicy.LiveAtTrigger)boundary=new CapturedAbilityBoundary(this,action);
                else if(mode==AbilitySnapshotPolicy.AfterCommittedDamage && observed.EventId>0 && damageBoundaries.TryGetValue(observed.EventId,out var committed))boundary=committed;
                else if(!Snapshots.TryGet(action.ActionId,mode,out boundary))
                    throw new InvalidOperationException("Snapshot boundary not captured: "+mode+",action="+action.ActionId);
            }
            var cause=action.GetEffectCause(evt.Kind,evt.RollUseIndex);
            if(observed.IsKnown && observed.SourceAbilityId==identity.DefinitionId && observed.SourceActorId==identity.OwnerActorRuntimeId && policy?.CausePolicy!=AbilityCausePolicy.DerivedEvent)cause=observed;
            else if(observed.EventId>0 || !cause.IsKnown || cause.SourceAbilityId!=identity.DefinitionId)
            {
                var key=(action.ActionId,identity.DefinitionId,evt.Kind,evt.RollUseIndex,observed.EventId);
                if(!ruleCauses.TryGetValue(key,out cause))
                {
                    cause=new EffectCause(observed.RootActionId>0?observed.RootActionId:action.ActionId,observed.EventId,
                        Battle.EventSequence.Next(),identity.DefinitionId,identity.OwnerActorRuntimeId,evt.RollUseIndex,
                        observed.EventId>0||action.Skill==null?EffectOriginKind.Reaction:EffectOriginKind.DirectRoll);
                    ruleCauses.Add(key,cause);
                }
            }
            long rollUse=damage!=null&&damage.Request.SourceAction==null?0:Battle.EventSequence.Boundary(action.ActionId,AbilityEventKind.RollUsing,evt.RollUseIndex);
            if(damage?.Request.SourceAction!=null && damage.Request.SourceCause.EventId>0)
                rollUse=damage.Request.SourceAction.GetEffectCause(AbilityEventKind.RollUsing,damage.Request.SourceCause.SourceRollIndex).EventId;
            var frame=new AbilityExecutionFrame(identity,evt.Kind,action.ActionId,cause,Reference(action.Owner,action.OwnerPart,action.Slot?.SlotId),boundary.AimedTarget,target,
                new AbilityEventFacts(context?.RollNumber??(evt.RollUseIndex>=0?evt.RollUseIndex+1:0),context?.RollSucceeded??false,
                    context?.RollResult?.IsCritical??action.LastRollResult?.IsCritical==true,context?.IsClash??false,evt.IsOneSided,context!=null&&(context.RollResult!=null||context.RollSucceeded||evt.Kind==AbilityEventKind.ExchangeWon||evt.Kind==AbilityEventKind.ExchangeLost)),
                evt.DuelEffectsEligible,mode,boundary.Snapshot(target,damage?.Request.SourceCause.EventId??0,Reference(action.Owner,action.OwnerPart,action.Slot?.SlotId)),
                evt.Kind==AbilityEventKind.DamageSettled?AbilityExecutionUnit.DamageResolution:AbilityExecutionUnit.Action,
                CurrentTurn,action.ClashExecutionId,0,rollUse,damage?.Request.SourceCause.EventId??0,action.ActionId,
                action.CounterExecution?.ExecutionId??0,boundary.WeaponFor(identity.OwnerActorRuntimeId),observedCause:observed,observedParent:parent);
            frame.AttachState(new StateView(this,frame,mode==AbilitySnapshotPolicy.LiveAtTrigger?null:boundary));
            frame.Provenance=Battle.EventSequence.Provenance;Battle.EventSequence.Record(cause);
            if(policy!=null)
            {
                Scope(frame,policy.OwnerScope);
                if(policy.CausePolicy==AbilityCausePolicy.DerivedEvent)
                    Battle.EventSequence.Record(cause);
            }
            return frame;
        }
        public AbilityRuleReadContext ReadView(AbilityExecutionFrame frame,AppliedDamageEvidence? evidence=null)
        {
            bool player=ResolveActor(frame.Source.ActorRuntimeId)==Battle.Player;
            var allies=Battle.AllCharacters.Where(a=>a!=null&&!a.IsDead&&(a==Battle.Player)==player).Select(a=>Reference(a)).ToArray();
            var enemies=Battle.AllCharacters.Where(a=>a!=null&&!a.IsDead&&(a==Battle.Player)!=player).Select(a=>Reference(a)).ToArray();
            var view=new AbilityRuleReadContext(frame,Array.AsReadOnly(allies),Array.AsReadOnly(enemies),evidence);
            view.EnemyParts=Array.AsReadOnly(enemies.SelectMany(e=>{var actor=ResolveActor(e.ActorRuntimeId);return actor.IsSingleHpTarget?new[]{e}:actor.BodyParts.Where(p=>p!=null&&!p.IsBroken).Select(p=>Reference(actor,p)).ToArray();}).ToArray());
            return view;
        }
        public bool ExecuteSpecialist(string definitionId,string sourceGuid,int entry,SkillEffectContext context,
            bool gate,Func<AbilityRuleCommitContext,bool> applyHandler)
        {
            if(context?.Action==null||context.Owner==null||!gate)return false;
            if(!AbilityTimingMap.TryEvent(context.Timing,out var kind))return false;
            var identity=new AbilityIdentity(definitionId,LiveSlotQuery.ActorRuntimeId(context.Owner),context.Action.Slot?.SlotId,entry);
            if(!instances.TryGetValue(identity,out var instance))instances.Add(identity,instance=new AbilityInstance(new AbilityDefinitionRef(definitionId,sourceGuid),identity));
            var frame=CaptureFrame(identity,context.Action,new AbilityEventContext(kind,context.Action.ActionId,0,identity.OwnerActorRuntimeId,
                context.RollIndex,context.Action.DuelEffectsEligible,context.IsOneSided),context:context);
            var op=new AbilityOperationSpec("compat.specialist",new SpecialistRuleSettings(applyHandler));
            var view=ReadView(frame);
            return ExecuteOperations(instance,frame,"specialist/"+entry,CombatConditionNode.Leaf(new CapturedGateCondition(gate)),view,
                new[]{new CombatTargetRef(identity.OwnerActorRuntimeId,"","")},new[]{op});
        }
        public void Register(AbilityRuleSetDefinition definition)
        {
            if(definition!=null&&definitions.ContainsKey(definition))return;
            var errors=Registry.Validate(definition);if(errors.Count>0)throw new ArgumentException(string.Join("; ",errors));
            if(definitions.Values.Any(r=>r[0].StableId==definition.StableId||r[0].SourceGuid==definition.SourceAssetGuid))
                throw new ArgumentException("Duplicate rule-set source identity");
            definitions[definition]=definition.Rules.Select((r,i)=>new CompiledRule{Definition=definition,StableId=definition.StableId,SourceGuid=definition.SourceAssetGuid,SourceName=definition.SourceAssetName,SourceKind=definition.SourceKind,Id=r.RuleId,Index=i,Trigger=r.Trigger,
                Selector=r.TargetSelectorId,Policy=new AbilityRuleMetadata{Snapshot=r.Policy.Snapshot,OwnerScope=r.Policy.OwnerScope,
                    CausePolicy=r.Policy.CausePolicy,ConditionPhase=r.Policy.ConditionPhase,CardAuthored=r.Policy.CardAuthored},
                Condition=Registry.Compile(r.Condition),Operations=r.Operations.Select(o=>new AbilityOperationSpec(o)).ToArray(),
                Lifetime=new AbilityRuleLifetime{OncePerBattle=r.Lifetime.OncePerBattle,OncePerTurn=r.Lifetime.OncePerTurn,
                    OncePerSkillExecution=r.Lifetime.OncePerSkillExecution,OncePerCounterExecution=r.Lifetime.OncePerCounterExecution,
                    OncePerRollUse=r.Lifetime.OncePerRollUse,ConsumeOnSchedule=r.Lifetime.ConsumeOnSchedule,FullCooldownTurns=r.Lifetime.FullCooldownTurns},
                Phase=r.Phase,Priority=r.Priority,DuelGate=r.RequiresDuelGate,ScheduledOnly=r.ScheduledOnly}).ToArray();
            try{foreach(var spec in definitions[definition].SelectMany(r=>r.Operations).Select(o=>o.Settings).OfType<AbilityScheduleSpec>())Register(spec.Definition);}
            catch{definitions.Remove(definition);throw;}
        }
        public bool CanRoute(SkillDefinition definition)=>definition!=null&&definitions.Values.Any(r=>r[0].SourceKind==AbilityRuleSourceKind.Skill&&r[0].StableId==definition.SkillId&&r[0].SourceName==definition.name);
        public IReadOnlyList<SkillEffectResult> Execute(SkillDefinition definition,SkillEffectContext context,SkillEffectTiming timing)
        {
            var source=definitions.Single(p=>p.Value[0].SourceKind==AbilityRuleSourceKind.Skill&&p.Value[0].StableId==definition.SkillId&&p.Value[0].SourceName==definition.name).Key;
            if(!AbilityTimingMap.TryEvent(timing,out var kind))return Array.Empty<SkillEffectResult>();var results=new List<SkillEffectResult>();
            foreach(var rule in definitions[source].OrderBy(r=>r.Phase).ThenBy(r=>r.Priority).ThenBy(r=>r.Index))
            {
                if(rule.ScheduledOnly||rule.Trigger!=kind)continue;
                if(rule.DuelGate&&context.Action.ActionType==ActionType.Duel&&!context.Action.DuelEffectsEligible)continue;
                if(context.IsOneSided&&(kind==AbilityEventKind.ExchangeWon||kind==AbilityEventKind.ExchangeLost))continue;
                var identity=new AbilityIdentity(rule.StableId,LiveSlotQuery.ActorRuntimeId(context.Owner),"",rule.Index);
                var frame=CaptureFrame(identity,context.Action,new AbilityEventContext(kind,context.Action.ActionId,0,identity.OwnerActorRuntimeId,
                    context.RollIndex,context.Action.DuelEffectsEligible,context.IsOneSided),rule.Policy,context);
                bool committed=ExecuteRule(rule,frame,context.DamageContext);
                results.Add(committed?SkillEffectResult.Applied(null,timing,context):LastAppliedBatch?.ScheduledCount>0?SkillEffectResult.Scheduled(null,timing,context):SkillEffectResult.ConditionFailed(null,timing,context,"Rule did not commit"));
            }
            return results;
        }
        private bool ExecuteRule(CompiledRule rule,AbilityExecutionFrame frame,DamageContext damage=null,IReadOnlyList<CombatTargetRef> lockedTargets=null)
        {
            LastAppliedBatch=null;
            var replay=(frame.Ability.OwnerActorRuntimeId,rule.StableId+"/"+rule.Id,
                frame.ObservedCause.EventId>0?frame.ObservedCause.EventId:frame.Cause.EventId);
            if(completed.ContainsKey(replay))return false;
            if(!instances.TryGetValue(frame.Ability,out var instance))instances.Add(frame.Ability,instance=new AbilityInstance(
                new AbilityDefinitionRef(rule.StableId,rule.SourceGuid),frame.Ability));
            var lifeKey=(frame.Ability,rule.Id);
            if(!lifetimes.TryGetValue(lifeKey,out var lifetime))lifetimes.Add(lifeKey,lifetime=new AbilityLifetimeMemory(new AbilityLifetimeRules(
                oncePerTurn:rule.Lifetime.OncePerTurn,oncePerRollUse:rule.Lifetime.OncePerRollUse,fullCooldownTurns:rule.Lifetime.FullCooldownTurns)));
            var lifetimeEvent=new AbilityLifetimeEvent(frame.TurnId,frame.ClashId,frame.RollUseId,frame.DamageResolutionId,frame.Cause.EventId);
            if(!lifetime.CanActivate(lifetimeEvent))return false;
            string once=rule.Id+"/activated";
            var onceScope=rule.Lifetime.OncePerBattle?AbilityOwnerScope.Battle:rule.Lifetime.OncePerCounterExecution?
                AbilityOwnerScope.CounterExecution:AbilityOwnerScope.SkillExecution;
            if((rule.Lifetime.OncePerBattle||rule.Lifetime.OncePerSkillExecution||rule.Lifetime.OncePerCounterExecution)&&GetState(frame,onceScope,once)>0)return false;
            AppliedDamageEvidence? evidence=damage?.Result==null?null:new AppliedDamageEvidence(frame.Cause.EventId,frame.RollUseId,
                frame.DamageResolutionId,damage.Request.SourceAction!=null && !damage.Request.SourceCause.IsDirectResultOf(damage.Request.SourceAction.GetEffectCause(AbilityEventKind.RollUsing,damage.Request.SourceCause.SourceRollIndex)),damage.AppliedHpDamage,damage.WasWeakened,damage.WasBroken,damage.WasKilled,
                frame.ActualDamageTarget.ActorRuntimeId,frame.ActualDamageTarget.PartId,CombatAppliedValues.FromDamage(damage));
            var view=ReadView(frame,evidence);var targets=lockedTargets??Registry.Select(rule.Selector,view);
            return ExecuteOperations(instance,frame,rule.Id,rule.Condition,view,targets,rule.Operations,rule.Phase,rule.Priority,evidence,
                result=>{completed[replay]=true;lifetime.CommitIfApplied(lifetimeEvent,result);
                    if(rule.Lifetime.OncePerBattle||rule.Lifetime.OncePerSkillExecution||rule.Lifetime.OncePerCounterExecution)SetState(frame,onceScope,once,1);},rule.Policy.ConditionPhase);
        }
        public bool ExecuteOperations(AbilityInstance instance,AbilityExecutionFrame frame,string ruleId,CombatConditionNode condition,
            AbilityRuleReadContext view,IReadOnlyList<CombatTargetRef> targets,AbilityOperationSpec[] operations,int phase=0,int priority=0,
            AppliedDamageEvidence? evidence=null,Action<AbilityExecutionResult> committed=null,CombatConditionPhase conditionPhase=CombatConditionPhase.PreTarget)
        {
            var operation=new Operation(view,Registry,operations);
            var pre=conditionPhase==CombatConditionPhase.PreTarget?condition:null;
            var candidate=conditionPhase==CombatConditionPhase.PreTarget?null:condition;
            var plan=evidence.HasValue?runner.PlanCommittedDamage(instance,frame.ToEventContext(),new Matcher(),pre,candidate,
                new Selector(targets),new IEffectOperation[]{operation},frame.Snapshot,evidence.Value):
                runner.PlanWithFrame(instance,frame,new Matcher(),pre,candidate,new Selector(targets),new IEffectOperation[]{operation});
            if(plan.Commands.Count==0)return false;
            var commands=PlannedCommandBridge.Materialize(instance,frame.ToEventContext(),plan,
                "rule:"+frame.Ability.DefinitionId+":"+ruleId+":"+frame.ActionId+":"+frame.Cause.EventId,phase,priority);
            var result=apply.Apply(instance,frame.ToEventContext(),commands,new Backend(this,frame,ruleId,commands,operation.Planned));
            trace.AddRange(result.Records);
            if(result.HasUnknownCommit)throw new InvalidOperationException("Rule commit indeterminate; reconciliation required: "+ruleId);
            if(result.CommittedCount>0)committed?.Invoke(new AbilityExecutionResult(plan.Commands.Count,result.CommittedCount,null));
            else if(result.ScheduledCount>0 && view.Frame!=null)
            {
                var rule=definitions.Values.SelectMany(r=>r).FirstOrDefault(r=>r.StableId==frame.Ability.DefinitionId&&r.Id==ruleId);
                if(rule?.Lifetime.ConsumeOnSchedule==true)committed?.Invoke(new AbilityExecutionResult(plan.Commands.Count,result.ScheduledCount,null));
            }
            DrainEffects();LastAppliedBatch=result;return result.CommittedCount>0;
        }
        public void Schedule(AbilityRuleSetDefinition definition,string ruleId,AbilityExecutionFrame sourceFrame,CombatTargetRef target)
        {
            if(!definitions.ContainsKey(definition))Register(definition);
            var rule=definitions[definition].Single(r=>r.Id==ruleId);
            if(!rule.ScheduledOnly)throw new ArgumentException("Scheduled rules must declare ScheduledOnly");
            if(!actions.TryGetValue(sourceFrame.ActionId,out var source))throw new InvalidOperationException("Missing scheduling execution");
            if(!TryResolve(target,out var actor,out var part))throw new InvalidOperationException("Scheduling target unavailable");
            CapturedAbilityBoundary captured=null;
            var mode=rule.Policy.Snapshot;
            DamageContext historical=null;
            if(mode==AbilitySnapshotPolicy.LockAtScheduleCreation)captured=new CapturedAbilityBoundary(this,source);
            else if(mode==AbilitySnapshotPolicy.AfterCommittedDamage)
            {
                if(!damageBoundaries.TryGetValue(sourceFrame.DamageResolutionId,out captured)||!damageResults.TryGetValue(sourceFrame.DamageResolutionId,out historical))
                    throw new InvalidOperationException("Missing committed damage snapshot");
            }
            else if(mode!=AbilitySnapshotPolicy.LiveAtTrigger && mode!=AbilitySnapshotPolicy.LockAtReactionStart)
            {if(!Snapshots.TryGet(source.ActionId,mode,out captured))throw new InvalidOperationException("Scheduling snapshot unavailable: "+mode);}
            var scheduledCause=sourceFrame.Cause.Derive(Battle.EventSequence.Next(),EffectOriginKind.Reaction);
            var execution=new BattleAction{Slot=new ActionSlot{ActionId=++virtualActionId,Owner=source.Owner,Part=source.OwnerPart,Skill=source.Skill,
                TargetCharacter=actor,TargetPart=part,SlotId=source.Slot?.SlotId}};
            execution.SetDuelEffectsEligible(sourceFrame.DuelEffectsEligible);
            Battle.TurnStartEffects.EnqueueLocked(actor,part,()=>
            {
                if(mode==AbilitySnapshotPolicy.LockAtReactionStart)CaptureBoundary(execution,mode,true);
                var identity=new AbilityIdentity(rule.StableId,LiveSlotQuery.ActorRuntimeId(source.Owner),"",rule.Index);
                SkillEffectContext context=historical==null?null:new SkillEffectContext(execution,source.Skill?.Definition,SkillEffectTiming.AfterDamage,
                    null,historical,null,null,1,scheduledCause.SourceRollIndex,null,historical.IsClashDamage,!historical.IsClashDamage,true);
                var frame=CaptureFrame(identity,execution,new AbilityEventContext(rule.Trigger,execution.ActionId,0,identity.OwnerActorRuntimeId,
                    scheduledCause.SourceRollIndex,sourceFrame.DuelEffectsEligible,false),rule.Policy,context,captured,
                    scheduledCause,sourceFrame.Cause,target);
                try{ExecuteRule(rule,frame,historical,new[]{target});}
                finally{ActionEnded(execution);}
            },scheduledCause,checked(CurrentTurn+1));
        }
        public void BindPassive(AbilityRuleSetDefinition definition,Character owner)
        {
            if(definition==null||owner==null||definition.SourceKind!=AbilityRuleSourceKind.Passive)throw new ArgumentException("Passive binding requires definition and owner");
            if(!definitions.ContainsKey(definition))Register(definition);
            if(!passiveOwners.TryGetValue(definition,out var owners))passiveOwners.Add(definition,owners=new HashSet<Character>());
            owners.Add(owner);actors[LiveSlotQuery.ActorRuntimeId(owner)]=owner;
        }
        public void BattleStarted()
        {
            foreach(var actor in Battle.AllCharacters.Where(a=>a!=null))
                foreach(var definition in actor.Data?.AbilityRules??Array.Empty<AbilityRuleSetDefinition>())
                    if(definition!=null&&definition.Enabled){if(definition.SourceKind==AbilityRuleSourceKind.Passive)BindPassive(definition,actor);else if(!definitions.ContainsKey(definition))Register(definition);}
            Lifecycle(AbilityEventKind.GlobalBattleStarted);
        }
        private int OwnerOrder(Character owner)=>Battle.AllCharacters.ToList().IndexOf(owner);
        public void Lifecycle(AbilityEventKind kind)
        {
            var applicable=passiveOwners.SelectMany(p=>p.Value.Where(o=>o!=null&&(!o.IsDead||kind==AbilityEventKind.GlobalBattleEnded))
                .SelectMany(o=>definitions[p.Key].Where(r=>!r.ScheduledOnly&&r.Trigger==kind).Select(r=>(Rule:r,Owner:o))))
                .OrderBy(i=>i.Rule.Phase).ThenBy(i=>i.Rule.Priority).ThenBy(i=>i.Rule.StableId,StringComparer.Ordinal)
                .ThenBy(i=>OwnerOrder(i.Owner)).ThenBy(i=>i.Rule.Index).ToArray();
            var executions=applicable.Select(i=>i.Owner).Distinct().ToDictionary(o=>o,o=>new BattleAction{Slot=new ActionSlot{
                ActionId=++virtualActionId,Owner=o,TargetCharacter=o}});
            // Every frozen lifecycle view is captured before the first rule can mutate the battle.
            foreach(var item in applicable)
                if(item.Rule.Policy.Snapshot!=AbilitySnapshotPolicy.LiveAtTrigger)CaptureBoundary(executions[item.Owner],item.Rule.Policy.Snapshot);
            try
            {
                foreach(var item in applicable)
                {
                    var rule=item.Rule;var owner=item.Owner;var action=executions[owner];
                    var identity=new AbilityIdentity(rule.StableId,LiveSlotQuery.ActorRuntimeId(owner),"",rule.Index);
                    var frame=CaptureFrame(identity,action,new AbilityEventContext(kind,action.ActionId,0,identity.OwnerActorRuntimeId,-1,false,false),rule.Policy);
                    ExecuteRule(rule,frame);
                }
            }
            finally{foreach(var action in executions.Values)ActionEnded(action);}
        }
        public void PublishCommitted(AbilityEventKind kind,BattleAction action,EffectCause cause,EffectCause parent,CombatTargetRef target,DamageContext damage=null)
        {
            if(action?.Owner==null||cause.EventId<=0)return;
            var applicable=new List<(CompiledRule Rule,Character Owner)>();
            foreach(var pair in definitions)
                if(pair.Value[0].SourceKind==AbilityRuleSourceKind.Skill && pair.Value[0].StableId==action.Skill?.Definition?.SkillId)
                    applicable.AddRange(pair.Value.Where(r=>!r.ScheduledOnly&&r.Trigger==kind).Select(r=>(r,action.Owner)));
                else if(passiveOwners.TryGetValue(pair.Key,out var owners))
                    foreach(var owner in owners.Where(o=>o!=null&&!o.IsDead))
                        applicable.AddRange(pair.Value.Where(r=>!r.ScheduledOnly&&r.Trigger==kind).Select(r=>(r,owner)));
            foreach(var item in applicable.OrderBy(i=>i.Rule.Phase).ThenBy(i=>i.Rule.Priority).ThenBy(i=>i.Rule.StableId,StringComparer.Ordinal).ThenBy(i=>OwnerOrder(i.Owner)).ThenBy(i=>i.Rule.Index))
            {
                var rule=item.Rule;var owner=item.Owner;
                if(rule.Policy.CardAuthored && kind==AbilityEventKind.DamageSettled &&
                    (!cause.IsDirectResultOf(action.GetEffectCause(AbilityEventKind.RollUsing,cause.SourceRollIndex))))continue;
                // Passive reactions own a separate execution, correlated to the observed cause.
                var execution=owner==action.Owner?action:new BattleAction{Slot=new ActionSlot{ActionId=++virtualActionId,Owner=owner,TargetCharacter=ResolveActor(target.ActorRuntimeId),
                    TargetPart=ResolveActor(target.ActorRuntimeId)?.BodyParts?.FirstOrDefault(p=>p.PartId==target.PartId)}};
                execution.ClashExecutionId=action.ClashExecutionId;execution.CounterExecution=action.CounterExecution;
                if(owner!=action.Owner && rule.Policy.Snapshot!=AbilitySnapshotPolicy.LiveAtTrigger && rule.Policy.Snapshot!=AbilitySnapshotPolicy.AfterCommittedDamage)
                    CaptureBoundary(execution,rule.Policy.Snapshot,true);
                var identity=new AbilityIdentity(rule.StableId,LiveSlotQuery.ActorRuntimeId(owner),"",rule.Index);
                SkillEffectContext context=damage==null?null:new SkillEffectContext(execution,execution.Skill?.Definition,SkillEffectTiming.AfterDamage,
                    null,damage,null,null,1,cause.SourceRollIndex,null,damage.IsClashDamage,!damage.IsClashDamage,true);
                var frame=CaptureFrame(identity,execution,new AbilityEventContext(kind,execution.ActionId,0,identity.OwnerActorRuntimeId,cause.SourceRollIndex,
                    action.DuelEffectsEligible,false),rule.Policy,context,observed:cause,parent:parent,capturedTarget:target);
                Effects.Enqueue(cause,()=>{try{ExecuteRule(rule,frame,damage);}finally{if(execution!=action)ActionEnded(execution);}});
            }
        }
        public void DrainEffects(){if(commitDepth==0)Effects.Drain(Battle.RunProgression?.Seed??0);}
        public void CaptureDamageResult(DamageContext damage)
        {
            if(damage==null||damage.Request.SourceCause.EventId<=0)return;
            var source=DamageSource(damage);
            if(source.ClashExecutionId>0&&damage.Target!=null)
            {
                var scope=new AbilityScopeKey(AbilityOwnerScope.ClashExecution,LiveSlotQuery.ActorRuntimeId(damage.Target),executionId:source.ClashExecutionId);
                State.Set(scope,"combat/hp_received",checked(State.Get(scope,"combat/hp_received")+damage.AppliedHpDamage));
                State.Set(scope,"combat/stagger_received",checked(State.Get(scope,"combat/stagger_received")+damage.StaggerDamageApplied));
            }
            damageBoundaries[damage.Request.SourceCause.EventId]=new CapturedAbilityBoundary(this,source);
            damageResults[damage.Request.SourceCause.EventId]=damage;
        }
        private BattleAction DamageSource(DamageContext damage)=>damage.Action??new BattleAction{Slot=new ActionSlot{
            ActionId=++virtualActionId,Owner=damage.Attacker??damage.Target,TargetCharacter=damage.Target,TargetPart=damage.TargetPart}};
        public void DamageCommitted(DamageContext damage)
        {
            if(damage==null)return;
            var cause=damage.Request.SourceCause;
            Battle.EventSequence.Provenance.TryGet(cause.ParentEventId,out var parent);
            PublishCommitted(AbilityEventKind.DamageSettled,DamageSource(damage),cause,parent,Reference(damage.Target,damage.TargetPart),damage);
        }
        public void ClashCompleted(ClashResultContext result)
        {
            foreach(var action in new[]{result?.FirstAction,result?.SecondAction}.Where(a=>a?.Owner!=null).Distinct())
            {
                var cause=action.GetEffectCause(AbilityEventKind.ClashCompleted,-1);
                PublishCommitted(AbilityEventKind.ClashCompleted,action,cause,default,Reference(action.Target,action.TargetPart));
            }
            DrainEffects();
            foreach(var action in new[]{result?.FirstAction,result?.SecondAction}.Where(a=>a?.Owner!=null).Distinct())
            {
                if(action.ClashExecutionId<=0)continue;
                State.Release(new AbilityScopeKey(AbilityOwnerScope.ClashExecution,LiveSlotQuery.ActorRuntimeId(action.Owner),executionId:action.ClashExecutionId));
                Modifiers.ReleaseExecution(action.ClashExecutionId,AbilityOwnerScope.ClashExecution);
            }
        }
        public void Dispose()
        {Battle.TurnStartEffects.Clear();Battle.TurnStartEffects.CurrentTurn=null;Effects.Cancel();Snapshots.Clear();passiveOwners.Clear();State.ResetAt(AbilityLifetimeBoundary.Run);Modifiers.Clear();ruleCauses.Clear();instances.Clear();lifetimes.Clear();completed.Clear();damageBoundaries.Clear();damageResults.Clear();actions.Clear();actors.Clear();}
    }
}
