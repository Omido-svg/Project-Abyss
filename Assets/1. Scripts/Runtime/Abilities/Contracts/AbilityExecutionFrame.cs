using System;

namespace ProjectAbyss.Abilities
{
    public enum AbilityExecutionUnit { Turn, Action, Clash, Exchange, RollUse, DamageResolution, Reaction }
    public enum AbilitySnapshotPolicy
    {
        LiveAtTrigger, LockAtActionStart, LockAtTargetReveal, LockAtScheduleCreation,
        LockAtReactionStart, AfterCommittedDamage, TurnEndFinalSnapshot
    }
    public enum EffectOriginKind { Unknown, DirectRoll, ExtraHit, Trap, Dot, Reaction }

    /// <summary>Root identity is correlation only. Direct ownership requires an exact parent event.</summary>
    public readonly struct EffectCause
    {
        public readonly long RootActionId, ParentEventId, EventId, SourceActorId;
        public readonly string SourceAbilityId;
        public readonly int SourceRollIndex;
        public readonly EffectOriginKind OriginKind;
        public bool IsKnown => RootActionId > 0 && EventId > 0 && SourceActorId > 0 &&
            !string.IsNullOrWhiteSpace(SourceAbilityId) && OriginKind != EffectOriginKind.Unknown;
        public EffectCause(long rootActionId, long parentEventId, long eventId,
            string abilityId, long actorId, int rollIndex, EffectOriginKind origin)
        {
            RootActionId=rootActionId; ParentEventId=parentEventId; EventId=eventId;
            SourceAbilityId=abilityId??string.Empty; SourceActorId=actorId;
            SourceRollIndex=rollIndex; OriginKind=origin;
        }
        public bool IsDirectResultOf(EffectCause parent) => IsKnown && parent.IsKnown &&
            (OriginKind==EffectOriginKind.DirectRoll || OriginKind==EffectOriginKind.Reaction) &&
            OriginKind==parent.OriginKind && ParentEventId==parent.EventId &&
            RootActionId==parent.RootActionId && SourceActorId==parent.SourceActorId &&
            SourceAbilityId==parent.SourceAbilityId && SourceRollIndex==parent.SourceRollIndex;
        public bool IsSameEvent(EffectCause other) => RootActionId==other.RootActionId &&
            ParentEventId==other.ParentEventId && EventId==other.EventId &&
            SourceAbilityId==other.SourceAbilityId && SourceActorId==other.SourceActorId &&
            SourceRollIndex==other.SourceRollIndex && OriginKind==other.OriginKind;
        public EffectCause Derive(long eventId, EffectOriginKind origin) =>
            eventId>0 && eventId!=EventId && IsKnown
                ? new EffectCause(RootActionId,EventId,eventId,SourceAbilityId,
                    SourceActorId,SourceRollIndex,origin)
                : throw new ArgumentException("A derived cause needs a distinct event ID and known parent.");
    }

    public readonly struct AbilityEventFacts
    {
        public readonly int RollNumber;
        public readonly bool RollSucceeded, CriticalRoll, IsClash, IsOneSided;
        public AbilityEventFacts(int rollNumber,bool succeeded,bool critical,bool clash,bool oneSided)
        {
            RollNumber=rollNumber; RollSucceeded=succeeded; CriticalRoll=critical;
            IsClash=clash; IsOneSided=oneSided;
        }
    }

    /// <summary>Immutable execution envelope. Unavailable legacy IDs stay zero rather than being invented.</summary>
    public sealed class AbilityExecutionFrame
    {
        public AbilityExecutionUnit Unit { get; }
        public long TurnId { get; }
        public long ActionId { get; }
        public long ClashId { get; }
        public long ExchangeId { get; }
        public long RollUseId { get; }
        public long DamageResolutionId { get; }
        public long SkillExecutionId { get; }
        public long CounterExecutionId { get; }
        public string WeaponInstanceId { get; }
        public AbilityEventKind EventKind { get; }
        public AbilityIdentity Ability { get; }
        public CombatTargetRef Source { get; }
        public CombatTargetRef AimedTarget { get; }
        public CombatTargetRef ActualDamageTarget { get; }
        public EffectCause Cause { get; }
        public AbilitySnapshotPolicy SnapshotPolicy { get; }
        public ConditionSnapshot Snapshot { get; }
        public AbilityEventFacts Facts { get; }
        public bool DuelEffectsEligible { get; }
        public bool IsLegacyProjection { get; }
        public AbilityExecutionFrame(AbilityIdentity ability, AbilityEventKind kind,
            long actionId, EffectCause cause, CombatTargetRef source, CombatTargetRef aimed,
            CombatTargetRef actual, AbilityEventFacts facts, bool duelEligible,
            AbilitySnapshotPolicy snapshotPolicy=AbilitySnapshotPolicy.LiveAtTrigger,
            ConditionSnapshot snapshot=null, AbilityExecutionUnit unit=AbilityExecutionUnit.Action,
            long turnId=0,long clashId=0,long exchangeId=0,long rollUseId=0,
            long damageResolutionId=0,long skillExecutionId=0,long counterExecutionId=0,
            string weaponInstanceId=null,bool legacyProjection=false)
        {
            Ability=ability; EventKind=kind; ActionId=actionId; Cause=cause;
            Source=source; AimedTarget=aimed; ActualDamageTarget=actual; Facts=facts;
            DuelEffectsEligible=duelEligible; SnapshotPolicy=snapshotPolicy; Snapshot=snapshot;
            Unit=unit; TurnId=turnId; ClashId=clashId; ExchangeId=exchangeId;
            RollUseId=rollUseId; DamageResolutionId=damageResolutionId;
            SkillExecutionId=skillExecutionId; CounterExecutionId=counterExecutionId;
            WeaponInstanceId=weaponInstanceId??string.Empty; IsLegacyProjection=legacyProjection;
        }
        public AbilityEventContext ToEventContext() => new AbilityEventContext(EventKind,
            ActionId,Cause.EventId,Ability.OwnerActorRuntimeId,Cause.SourceRollIndex,
            DuelEffectsEligible,Facts.IsOneSided,this);

        public static AbilityExecutionFrame FromLegacy(AbilityIdentity ability,
            AbilityEventContext e, ConditionSnapshot snapshot=null) =>
            new AbilityExecutionFrame(ability,e.Kind,e.ActionId,
                new EffectCause(e.ActionId,0,e.CauseId,ability.DefinitionId,
                    e.OwnerActorRuntimeId,e.RollUseIndex,EffectOriginKind.Unknown),
                new CombatTargetRef(e.OwnerActorRuntimeId,"",ability.SourceSlotId),
                snapshot==null?default:new CombatTargetRef(snapshot.OriginalTarget.ActorId,
                    snapshot.OriginalTarget.PartId,snapshot.OriginalTarget.SlotId),
                snapshot==null?default:new CombatTargetRef(snapshot.ActualDamageTarget.ActorId,
                    snapshot.ActualDamageTarget.PartId,snapshot.ActualDamageTarget.SlotId),
                new AbilityEventFacts(0,false,false,false,e.IsOneSided),e.DuelEffectsEligible,
                snapshot:snapshot,legacyProjection:true);
    }

    public interface IReadOnlyAbilityExecutionContext : IReadOnlyAbilityContext
    {
        AbilityExecutionFrame Frame { get; }
    }
}
