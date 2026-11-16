using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>Schema-compatible adapter into the single condition-tree evaluator.</summary>
    public static class MixedAbilityConditionCompiler
    {
        private sealed class RollNumberCondition : ICombatCondition
        {
            private readonly int number;
            public RollNumberCondition(int number){this.number=number;}
            public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef candidate)=>
                context is IReadOnlyAbilityExecutionContext read && read.Frame?.Facts.RollNumber==number;
        }
        private sealed class TargetStatusCondition : ICombatCondition
        {
            private readonly StatusEffectId status;
            private readonly bool numeric;
            private readonly int threshold;
            public TargetStatusCondition(MixedOperationEntry entry)
            {status=entry.RequiredStatus;numeric=entry.Condition==MixedAbilityConditionKind.TargetNumericStatusAtLeast;threshold=entry.RequiredNumericValue;}
            public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef candidate)
            {
                if(!(context is IConditionSnapshotContext read) || read.Snapshot==null)return false;
                var subject=read.Snapshot.ActualDamageTarget;
                var fact=numeric?ConditionFact.StatusNumericTotal:ConditionFact.TimedStatusPresent;
                read.Snapshot.TryGet(new SubjectRef(subject.ActorId),ConditionScope.Actor,fact,status.ToString(),out int actorValue);
                int partValue=0;
                if(!string.IsNullOrEmpty(subject.PartId))read.Snapshot.TryGet(subject,ConditionScope.Part,fact,status.ToString(),out partValue);
                // The legacy gate observes an actor's common statuses together with the aimed part's statuses.
                return numeric?(long)actorValue+partValue>=threshold:actorValue>0||partValue>0;
            }
        }
        private sealed class ReadView : IReadOnlyAbilityExecutionContext, IConditionSnapshotContext
        {
            public AbilityExecutionFrame Frame { get; }
            public AbilityIdentity Ability=>Frame.Ability;
            public AbilityEventContext Event=>Frame.ToEventContext();
            public ConditionSnapshot Snapshot=>Frame.Snapshot;
            public ReadView(AbilityExecutionFrame frame){Frame=frame;}
        }
        public static CombatConditionNode Compile(MixedOperationEntry op)
        {
            if(!MixedAbilityConditionGate.IsValid(op))throw new ArgumentException("Invalid mixed operation condition.");
            var nodes=new List<CombatConditionNode>();
            if(op.RestrictToRoll)nodes.Add(CombatConditionNode.Leaf(new RollNumberCondition(op.RollNumber)));
            ICombatCondition predicate=op.Condition switch
            {
                MixedAbilityConditionKind.None=>null,
                MixedAbilityConditionKind.RollSucceeded=>new CombatEventFactCondition(CombatEventFact.RollSucceeded),
                MixedAbilityConditionKind.RollFailed=>new CombatEventFactCondition(CombatEventFact.RollFailed),
                MixedAbilityConditionKind.CriticalRoll=>new CombatEventFactCondition(CombatEventFact.CriticalRoll),
                MixedAbilityConditionKind.ClashOnly=>new CombatEventFactCondition(CombatEventFact.ClashOnly),
                MixedAbilityConditionKind.OneSidedOnly=>new CombatEventFactCondition(CombatEventFact.OneSidedOnly),
                MixedAbilityConditionKind.TargetHasStatus=>new TargetStatusCondition(op),
                MixedAbilityConditionKind.TargetNumericStatusAtLeast=>new TargetStatusCondition(op),
                _=>throw new ArgumentException("Unregistered mixed condition.")
            };
            if(predicate!=null)nodes.Add(CombatConditionNode.Leaf(predicate));
            return CombatConditionNode.All(nodes.ToArray());
        }

        public static AbilityExecutionFrame Capture(AbilityIdentity ability,AbilityEventKind trigger,
            SkillEffectContext context,MixedOperationEntry op,AbilityRuleMetadata policy=null)
        {
            if(context?.Action==null)throw new ArgumentException("Action is required for condition capture.");
            var evt=new AbilityEventContext(trigger,context.Action.ActionId,0,ability.OwnerActorRuntimeId,
                context.RollIndex,context.Action.DuelEffectsEligible,context.IsOneSided);
            return context.BattleContext.Abilities.CaptureFrame(ability,context.Action,evt,policy,context);
        }
        public static bool Evaluate(MixedOperationEntry op,SkillEffectContext context)
        {
            if(context?.Action==null)return op.Condition==MixedAbilityConditionKind.None && !op.RestrictToRoll;
            var ability=new AbilityIdentity(context.SkillDefinition?.SkillId??"",LiveSlotQuery.ActorRuntimeId(context.Owner),
                context.Action.Slot?.SlotId,op.EntryIndex);
            return Compile(op).Evaluate(new ReadView(Capture(ability,op.Trigger,context,op)),default);
        }
    }
}
