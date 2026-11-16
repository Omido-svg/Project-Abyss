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
                var scope=string.IsNullOrEmpty(read.Snapshot.ActualDamageTarget.PartId)?ConditionScope.Actor:ConditionScope.Part;
                return new FactCondition(ConditionSubject.ActualDamageTarget,scope,
                    numeric?ConditionFact.StatusNumericTotal:ConditionFact.TimedStatusPresent,
                    status.ToString(),numeric?FactComparison.AtLeast:FactComparison.Present,
                    numeric?threshold:1).Evaluate(context,candidate);
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
            SkillEffectContext context,MixedOperationEntry op)
        {
            if(context?.Action==null)throw new ArgumentException("Action is required for condition capture.");
            long owner=LiveSlotQuery.ActorRuntimeId(context.Owner);
            var actual=new SubjectRef(LiveSlotQuery.ActorRuntimeId(context.Target),context.TargetPart?.PartId);
            var observations=new Dictionary<FactKey,int>();
            if(op.Condition==MixedAbilityConditionKind.TargetHasStatus ||
               op.Condition==MixedAbilityConditionKind.TargetNumericStatusAtLeast)
            {
                var fact=op.Condition==MixedAbilityConditionKind.TargetHasStatus
                    ?ConditionFact.TimedStatusPresent:ConditionFact.StatusNumericTotal;
                int value=op.Condition==MixedAbilityConditionKind.TargetHasStatus
                    ?(StatusEffectFactory.HasStatus(context.Target,context.TargetPart,op.RequiredStatus)?1:0)
                    :StatusEffectFactory.GetNumericTotal(context.Target,context.TargetPart,op.RequiredStatus);
                observations[new FactKey(actual.ActorId,string.IsNullOrEmpty(actual.PartId)?ConditionScope.Actor:ConditionScope.Part,
                    fact,op.RequiredStatus.ToString(),actual.PartId)]=value;
            }
            var action=context.Action;
            var aimed=new SubjectRef(LiveSlotQuery.ActorRuntimeId(action.DeclaredTarget),action.DeclaredTargetPart?.PartId);
            var snapshot=new ConditionSnapshot(new SubjectRef(owner,action.OwnerPart?.PartId),aimed,actual,
                new SubjectRef(owner),0,observations);
            var cause=action.GetEffectCause(trigger,context.RollIndex);
            return new AbilityExecutionFrame(ability,trigger,action.ActionId,cause,
                new CombatTargetRef(owner,action.OwnerPart?.PartId,ability.SourceSlotId),
                new CombatTargetRef(aimed.ActorId,aimed.PartId,""),
                new CombatTargetRef(actual.ActorId,actual.PartId,""),
                new AbilityEventFacts(context.RollNumber,context.RollSucceeded,context.RollResult?.IsCritical==true,
                    context.IsClash,context.IsOneSided),action.DuelEffectsEligible,snapshot:snapshot,
                unit:AbilityExecutionUnit.RollUse,turnId:context.BattleContext?.Services?.TurnManager?.CurrentTurn??0,
                skillExecutionId:action.ActionId,legacyProjection:false);
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
