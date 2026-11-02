using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectAbyss.Abilities
{
    // R3-K: "who" and "where" are part of each condition, not inferred from
    // CurrentTarget or the currently displayed UI selection.
    public enum R3ConditionSubject
    {
        AbilityOwner,
        OriginalTarget,
        SelectedTarget,
        ActualDamageTarget,
        SourceActor
    }
    public enum R3ConditionScope { Actor, Part, Slot }

    public enum R3ConditionFact
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
    public enum R3FactComparison { Present, AtLeast, AtMost, Equals }

    public readonly struct R3SubjectRef
    {
        public readonly long ActorId;
        public readonly string PartId;
        public readonly string SlotId;
        public R3SubjectRef(long actorId,string partId=null,string slotId=null)
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
    public readonly struct R3FactKey : IEquatable<R3FactKey>
    {
        public readonly long ActorId;
        public readonly string PartId,SlotId,StableId;
        public readonly R3ConditionScope Scope;
        public readonly R3ConditionFact Fact;
        public R3FactKey(long actorId,R3ConditionScope scope,
            R3ConditionFact fact,string stableId,string partId=null,string slotId=null)
        {
            ActorId=actorId;Scope=scope;Fact=fact;
            StableId=stableId??string.Empty;
            PartId=scope==R3ConditionScope.Part ? partId??string.Empty : string.Empty;
            SlotId=scope==R3ConditionScope.Slot ? slotId??string.Empty : string.Empty;
        }
        public bool Equals(R3FactKey other) =>
            ActorId==other.ActorId&&Scope==other.Scope&&Fact==other.Fact &&
            string.Equals(StableId,other.StableId,StringComparison.Ordinal) &&
            string.Equals(PartId,other.PartId,StringComparison.Ordinal) &&
            string.Equals(SlotId,other.SlotId,StringComparison.Ordinal);
        public override bool Equals(object o)=>o is R3FactKey other&&Equals(other);
        public override int GetHashCode()=>
            HashCode.Combine(ActorId,(int)Scope,(int)Fact,StableId,PartId,SlotId);
    }

    public sealed class R3ConditionSnapshot
    {
        private readonly ReadOnlyDictionary<R3FactKey,int> facts;
        public R3SubjectRef Owner {get;}
        public R3SubjectRef OriginalTarget {get;}
        public R3SubjectRef ActualDamageTarget {get;}
        public R3SubjectRef SourceActor {get;}
        public long OriginHitId {get;}

        public R3ConditionSnapshot(R3SubjectRef owner,R3SubjectRef original,
            R3SubjectRef actualDamage,R3SubjectRef source,long originHitId,
            IDictionary<R3FactKey,int> observations)
        {
            Owner=owner;OriginalTarget=original;ActualDamageTarget=actualDamage;
            SourceActor=source;OriginHitId=originHitId;
            // Deep value copy. The producer's dictionary cannot mutate history.
            facts=new ReadOnlyDictionary<R3FactKey,int>(
                new Dictionary<R3FactKey,int>(
                    observations??new Dictionary<R3FactKey,int>()));
        }

        public R3SubjectRef Resolve(R3ConditionSubject subject,CombatTargetRef candidate)
        {
            switch(subject)
            {
                case R3ConditionSubject.AbilityOwner: return Owner;
                case R3ConditionSubject.OriginalTarget: return OriginalTarget;
                case R3ConditionSubject.ActualDamageTarget: return ActualDamageTarget;
                case R3ConditionSubject.SourceActor: return SourceActor;
                case R3ConditionSubject.SelectedTarget:
                    return new R3SubjectRef(candidate.ActorRuntimeId,candidate.PartId,candidate.SlotId);
                default: return default;
            }
        }

        public bool TryGet(R3SubjectRef subject,R3ConditionScope scope,
            R3ConditionFact fact,string stableId,out int value)
        {
            value=0;
            if(subject.ActorId<=0 ||
                scope==R3ConditionScope.Part && string.IsNullOrEmpty(subject.PartId) ||
                scope==R3ConditionScope.Slot && string.IsNullOrEmpty(subject.SlotId))
                return false;
            return facts.TryGetValue(new R3FactKey(subject.ActorId,scope,fact,stableId,
                subject.PartId,subject.SlotId),out value);
        }
    }

    public interface IR3ConditionSnapshotContext : IReadOnlyAbilityContext
    {
        R3ConditionSnapshot Snapshot {get;}
    }

    /// <summary>
    /// One condition type for each typed fact. Missing subject, missing part or
    /// missing fact always FAILS (even when threshold is zero).
    /// </summary>
    public sealed class R3FactCondition : ICombatCondition
    {
        public readonly R3ConditionSubject Subject;
        public readonly R3ConditionScope Scope;
        public readonly R3ConditionFact Fact;
        public readonly string StableId;
        public readonly R3FactComparison Comparison;
        public readonly int Value;
        public R3FactCondition(R3ConditionSubject subject,R3ConditionScope scope,
            R3ConditionFact fact,string stableId,R3FactComparison comparison,int value=1)
        {
            if(string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("A stable content/status/keyword/tag ID is required.");
            Subject=subject;Scope=scope;Fact=fact;StableId=stableId;
            Comparison=comparison;Value=value;
        }
        public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef candidate)
        {
            var read=context as IR3ConditionSnapshotContext;
            if(read?.Snapshot==null)return false;
            var subject=read.Snapshot.Resolve(Subject,candidate);
            if(!read.Snapshot.TryGet(subject,Scope,Fact,StableId,out int observed))
                return false;
            switch(Comparison)
            {
                case R3FactComparison.Present:return observed>0;
                case R3FactComparison.AtLeast:return observed>=Value;
                case R3FactComparison.AtMost:return observed<=Value;
                case R3FactComparison.Equals:return observed==Value;
                default:return false;
            }
        }
    }

    public sealed class R3DamageOriginCondition : ICombatCondition
    {
        private readonly long expectedCauseId;
        private readonly long expectedOriginHitId;
        public R3DamageOriginCondition(long causeId,long originHitId=0)
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
