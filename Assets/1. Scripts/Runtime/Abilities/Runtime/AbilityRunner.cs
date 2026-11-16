using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Phase-safe R2/R3 ability planner. No BattleManager mutation or DomainEvents.
    /// Snapshot overloads freeze "who" and "which result"; R5 alone can commit.
    /// </summary>
    public sealed class AbilityRunner
    {
        private sealed class ReadContext :
            IConditionSnapshotContext, IReadOnlyDamageAbilityContext, IReadOnlyScopedAbilityContext
        {
            public AbilityEventContext Event { get; }
            public AbilityIdentity Ability { get; }
            public AbilityExecutionFrame Frame { get; }
            public IReadOnlyScopedAbilityState State=>Frame.State;
            public ConditionSnapshot Snapshot { get; }
            public bool HasCommittedDamageResult { get; }
            public AppliedDamageEvidence DamageEvidence { get; }
            public ReadContext(AbilityIdentity identity, AbilityEventContext e,
                ConditionSnapshot snapshot, bool committed, AppliedDamageEvidence damage)
            {
                Ability=identity;Event=e;Snapshot=snapshot;
                Frame=e.ExecutionFrame ?? AbilityExecutionFrame.FromLegacy(identity,e,snapshot);
                HasCommittedDamageResult=committed;DamageEvidence=damage;
            }
        }

        public sealed class PlannedExecution
        {
            public AbilityExecutionFrame Frame { get; }
            public AbilityExecutionResult Result { get; }
            public IReadOnlyList<CombatCommandIntent> Commands { get; }
            internal PlannedExecution(List<CombatCommandIntent> commands,string failure,AbilityExecutionFrame frame=null)
            {
                Frame=frame;
                Commands=commands.AsReadOnly();
                Result=new AbilityExecutionResult(commands.Count,0,failure);
            }
        }

        private sealed class Buffer : ICombatCommandSink
        {
            public readonly List<CombatCommandIntent> Commands=new List<CombatCommandIntent>();
            public void Enqueue(CombatCommandIntent command)=>Commands.Add(command);
        }

        // Original 6-argument R2 call and 7-argument R3 call remain unchanged.
        public PlannedExecution Plan(AbilityInstance instance,AbilityEventContext context,
            IAbilityTriggerMatcher matcher,ICombatCondition condition,
            ICombatTargetSelector selector,IReadOnlyList<IEffectOperation> operations)
        {
            return Plan(instance,context,matcher,null,condition,selector,operations);
        }

        public PlannedExecution Plan(AbilityInstance instance,AbilityEventContext context,
            IAbilityTriggerMatcher matcher,ICombatCondition preTargetCondition,
            ICombatCondition candidateCondition,ICombatTargetSelector selector,
            IReadOnlyList<IEffectOperation> operations)
        {
            return PlanCore(instance,context,matcher,preTargetCondition,
                candidateCondition,selector,operations,null,false,default);
        }

        /// <summary>
        /// R3: OriginalTarget, SelectedTarget, ActualDamageTarget and SourceActor
        /// are stable separately; candidate conditions run after selection.
        /// </summary>
        public PlannedExecution PlanWithSnapshot(AbilityInstance instance,
            AbilityEventContext context,IAbilityTriggerMatcher matcher,
            ICombatCondition preTargetCondition,ICombatCondition candidateCondition,
            ICombatTargetSelector selector,IReadOnlyList<IEffectOperation> operations,
            ConditionSnapshot snapshot)
        {
            if(snapshot==null)
                return Fail("Missing R3 condition snapshot");
            return PlanCore(instance,context,matcher,preTargetCondition,
                candidateCondition,selector,operations,snapshot,false,default);
        }

        /// <summary>
        /// R3 PostDamageResult: committed evidence is required. The old HP of a
        /// current actor can never fabricate historic damage, target or cause.
        /// Candidate/result scopes are evaluated after the named commit.
        /// </summary>
        public PlannedExecution PlanCommittedDamage(AbilityInstance instance,
            AbilityEventContext context,IAbilityTriggerMatcher matcher,
            ICombatCondition preTargetCondition,ICombatCondition candidateCondition,
            ICombatTargetSelector selector,IReadOnlyList<IEffectOperation> operations,
            ConditionSnapshot snapshot,AppliedDamageEvidence committedEvidence)
        {
            if(context.Kind!=AbilityEventKind.DamageSettled ||
               context.CauseId<=0 ||
               committedEvidence.CauseEventId!=context.CauseId ||
               committedEvidence.OriginHitId<=0 ||
               committedEvidence.OriginRollUseId<=0 && !(context.ExecutionFrame?.DamageResolutionId==committedEvidence.OriginHitId && context.ExecutionFrame.ObservedCause.EventId>0))
                return Fail("Uncommitted or unrelated damage evidence");
            if(snapshot==null)
                return Fail("Missing committed R3 snapshot");
            if(snapshot.OriginHitId>0 &&
               snapshot.OriginHitId!=committedEvidence.OriginHitId)
                return Fail("Damage origin hit snapshot mismatch");
            if(committedEvidence.TargetActorRuntimeId>0 &&
               committedEvidence.TargetActorRuntimeId!=snapshot.ActualDamageTarget.ActorId)
                return Fail("Damage actor snapshot mismatch");
            if(!string.IsNullOrEmpty(committedEvidence.TargetPartId) &&
               !string.Equals(committedEvidence.TargetPartId,
                   snapshot.ActualDamageTarget.PartId,StringComparison.Ordinal))
                return Fail("Damage part snapshot mismatch");

            return PlanCore(instance,context,matcher,preTargetCondition,
                candidateCondition,selector,operations,snapshot,true,committedEvidence);
        }

        public PlannedExecution PlanWithFrame(AbilityInstance instance, AbilityExecutionFrame frame,
            IAbilityTriggerMatcher matcher, ICombatCondition preTargetCondition,
            ICombatCondition candidateCondition, ICombatTargetSelector selector,
            IReadOnlyList<IEffectOperation> operations)
        {
            if(frame==null)return Fail("Missing execution frame");
            return PlanCore(instance,frame.ToEventContext(),matcher,preTargetCondition,
                candidateCondition,selector,operations,frame.Snapshot,false,default);
        }

        private static PlannedExecution Fail(string message)=>
            new PlannedExecution(new List<CombatCommandIntent>(),message);

        private PlannedExecution PlanCore(AbilityInstance instance,AbilityEventContext context,
            IAbilityTriggerMatcher matcher,ICombatCondition preTargetCondition,
            ICombatCondition candidateCondition,ICombatTargetSelector selector,
            IReadOnlyList<IEffectOperation> operations,ConditionSnapshot snapshot,
            bool hasCommittedDamage,AppliedDamageEvidence committedDamage)
        {
            snapshot??=context.ExecutionFrame?.Snapshot;
            if(instance==null||matcher==null||selector==null)
                return Fail("Invalid contracts");
            if(context.ExecutionFrame!=null && (!context.ExecutionFrame.Ability.Equals(instance.Identity) ||
                context.ExecutionFrame.ActionId!=context.ActionId || context.ExecutionFrame.Cause.EventId!=context.CauseId ||
                context.ExecutionFrame.EventKind!=context.Kind ||
                context.ExecutionFrame.Cause.SourceActorId!=context.OwnerActorRuntimeId ||
                context.ExecutionFrame.Cause.SourceAbilityId!=instance.Identity.DefinitionId ||
                context.ExecutionFrame.Cause.SourceRollIndex!=context.RollUseIndex ||
                context.ExecutionFrame.Source.ActorRuntimeId!=context.OwnerActorRuntimeId ||
                context.ExecutionFrame.SnapshotPolicy==AbilitySnapshotPolicy.AfterCommittedDamage && !hasCommittedDamage ||
                context.ExecutionFrame.SnapshotPolicy!=AbilitySnapshotPolicy.LiveAtTrigger && context.ExecutionFrame.Snapshot==null))
                return Fail("Execution frame identity or snapshot boundary mismatch");
            if(!matcher.Matches(in context))
                return Fail("Trigger mismatch");
            var key=new ActivationKey(context.ActionId,context.Kind,
                context.RollUseIndex,context.CauseId);
            if(!instance.RuntimeState.CanActivate(key))
                return Fail("Already committed");

            var view=new ReadContext(instance.Identity,context,snapshot,
                hasCommittedDamage,committedDamage);
            if(preTargetCondition!=null &&
               !preTargetCondition.Evaluate(view,default))
                return Fail("PreTarget condition failed");

            var candidates=selector.Select(view);
            var valid=new List<CombatTargetRef>();
            if(candidates!=null)
                foreach(var target in candidates)
                    if(candidateCondition==null || candidateCondition.Evaluate(view,target))
                        valid.Add(target);

            // Existing R2 contract allows a targetless operation to plan.
            // R3 condition snapshot path never silently converts all rejected
            // candidate targets into a self/actor effect.
            if(snapshot!=null && candidates!=null && candidates.Count>0 && valid.Count==0)
                return Fail("No eligible candidate targets");

            var buffer=new Buffer();
            if(operations!=null)
                foreach(var op in operations)
                    op?.Plan(view,valid.AsReadOnly(),buffer);
            return new PlannedExecution(buffer.Commands,string.Empty,view.Frame);
        }
    }
}
