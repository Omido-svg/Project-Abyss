using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectAbyss.Abilities
{
    // R3-K: "who" and "where" are part of each condition, not inferred from
    // CurrentTarget or the currently displayed UI selection.
    public enum ConditionSubject
    {
        AbilityOwner,
        OriginalTarget,
        SelectedTarget,
        ActualDamageTarget,
        SourceActor
    }
    public enum ConditionScope { Actor, Part, Slot }

    public enum ConditionFact
    {
        CharacterContent,
        GameplayTag,
        SkillTag,
        KeywordPresent,
        KeywordStack,
        TimedStatusPresent,
        StatusNumericTotal,
        StatusEffectiveAxis,
        ResourceAmount,
        PartStatusPresent
    }
    public enum FactComparison { Present, AtLeast, AtMost, Equals }

    public readonly struct SubjectRef
    {
        public readonly long ActorId;
        public readonly string PartId;
        public readonly string SlotId;
        public SubjectRef(long actorId,string partId=null,string slotId=null)
        {
            ActorId=actorId;
            PartId=partId??string.Empty;
            SlotId=slotId??string.Empty;
        }
    }

    /// <summary>
    /// A captured key uses independent Actor, Part and Slot scopes. It never
    /// conflates character tags, N/T status presence, numeric totals, effective
    /// axis, resource values or character-specific stack ledgers.
    /// </summary>
    public readonly struct FactKey : IEquatable<FactKey>
    {
        public readonly long ActorId;
        public readonly string PartId,SlotId,StableId;
        public readonly ConditionScope Scope;
        public readonly ConditionFact Fact;
        public FactKey(long actorId,ConditionScope scope,
            ConditionFact fact,string stableId,string partId=null,string slotId=null)
        {
            ActorId=actorId;Scope=scope;Fact=fact;
            StableId=stableId??string.Empty;
            PartId=scope==ConditionScope.Part ? partId??string.Empty : string.Empty;
            SlotId=scope==ConditionScope.Slot ? slotId??string.Empty : string.Empty;
        }
        public bool Equals(FactKey other) =>
            ActorId==other.ActorId&&Scope==other.Scope&&Fact==other.Fact &&
            string.Equals(StableId,other.StableId,StringComparison.Ordinal) &&
            string.Equals(PartId,other.PartId,StringComparison.Ordinal) &&
            string.Equals(SlotId,other.SlotId,StringComparison.Ordinal);
        public override bool Equals(object o)=>o is FactKey other&&Equals(other);
        public override int GetHashCode()=>
            HashCode.Combine(ActorId,(int)Scope,(int)Fact,StableId,PartId,SlotId);
    }

    public sealed class ConditionSnapshot
    {
        private readonly ReadOnlyDictionary<FactKey,int> facts;
        public SubjectRef Owner {get;}
        public SubjectRef OriginalTarget {get;}
        public SubjectRef ActualDamageTarget {get;}
        public SubjectRef SourceActor {get;}
        public long OriginHitId {get;}

        public ConditionSnapshot(SubjectRef owner,SubjectRef original,
            SubjectRef actualDamage,SubjectRef source,long originHitId,
            IDictionary<FactKey,int> observations)
        {
            Owner=owner;OriginalTarget=original;ActualDamageTarget=actualDamage;
            SourceActor=source;OriginHitId=originHitId;
            // Deep value copy. The producer's dictionary cannot mutate history.
            facts=new ReadOnlyDictionary<FactKey,int>(
                new Dictionary<FactKey,int>(
                    observations??new Dictionary<FactKey,int>()));
        }

        public SubjectRef Resolve(ConditionSubject subject,CombatTargetRef candidate)
        {
            switch(subject)
            {
                case ConditionSubject.AbilityOwner: return Owner;
                case ConditionSubject.OriginalTarget: return OriginalTarget;
                case ConditionSubject.ActualDamageTarget: return ActualDamageTarget;
                case ConditionSubject.SourceActor: return SourceActor;
                case ConditionSubject.SelectedTarget:
                    return new SubjectRef(candidate.ActorRuntimeId,candidate.PartId,candidate.SlotId);
                default: return default;
            }
        }

        public bool TryGet(SubjectRef subject,ConditionScope scope,
            ConditionFact fact,string stableId,out int value)
        {
            value=0;
            if(subject.ActorId<=0 ||
                scope==ConditionScope.Part && string.IsNullOrEmpty(subject.PartId) ||
                scope==ConditionScope.Slot && string.IsNullOrEmpty(subject.SlotId))
                return false;
            return facts.TryGetValue(new FactKey(subject.ActorId,scope,fact,stableId,
                subject.PartId,subject.SlotId),out value);
        }
    }

    public interface IConditionSnapshotContext : IReadOnlyAbilityContext
    {
        ConditionSnapshot Snapshot {get;}
    }

    /// <summary>
    /// One condition type for each typed fact. Missing subject, missing part or
    /// missing fact always FAILS (even when threshold is zero).
    /// </summary>
    public sealed class FactCondition : ICombatCondition
    {
        public readonly ConditionSubject Subject;
        public readonly ConditionScope Scope;
        public readonly ConditionFact Fact;
        public readonly string StableId;
        public readonly FactComparison Comparison;
        public readonly int Value;
        public FactCondition(ConditionSubject subject,ConditionScope scope,
            ConditionFact fact,string stableId,FactComparison comparison,int value=1)
        {
            if(string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("A stable content/status/keyword/tag ID is required.");
            Subject=subject;Scope=scope;Fact=fact;StableId=stableId;
            Comparison=comparison;Value=value;
        }
        public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef candidate)
        {
            var read=context as IConditionSnapshotContext;
            if(read?.Snapshot==null)return false;
            var subject=read.Snapshot.Resolve(Subject,candidate);
            if(!read.Snapshot.TryGet(subject,Scope,Fact,StableId,out int observed))
                return false;
            switch(Comparison)
            {
                case FactComparison.Present:return observed>0;
                case FactComparison.AtLeast:return observed>=Value;
                case FactComparison.AtMost:return observed<=Value;
                case FactComparison.Equals:return observed==Value;
                default:return false;
            }
        }
    }

    public sealed class DamageOriginCondition : ICombatCondition
    {
        private readonly long expectedCauseId;
        private readonly long expectedOriginHitId;
        public DamageOriginCondition(long causeId,long originHitId=0)
        {
            expectedCauseId=causeId;expectedOriginHitId=originHitId;
        }
        public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef candidate)
        {
            if(!(context is IReadOnlyDamageAbilityContext damage)||
               context.Event.Kind!=AbilityEventKind.DamageSettled||
               !damage.HasCommittedDamageResult)return false;
            var evidence=damage.DamageEvidence;
            return expectedCauseId>0 && context.Event.CauseId==expectedCauseId &&
                   evidence.CauseEventId==expectedCauseId &&
                   evidence.OriginHitId>0 &&
                   (expectedOriginHitId==0||evidence.OriginHitId==expectedOriginHitId);
        }
    }
}
