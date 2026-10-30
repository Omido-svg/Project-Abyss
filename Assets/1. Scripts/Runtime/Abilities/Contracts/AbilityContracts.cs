using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public enum AbilityEventKind
    {
        AttackResolutionStarting,
        RollUsing,
        ExchangeWon,
        ExchangeLost,
        ClashCompleted,
        PreparationActivated,
        DamageSettled,
        PartWeakened,
        PartBroken,
        KillSettled,
        TurnStarting,
        TurnEnding,
        GlobalBattleStarted,
        GlobalBattleEnded
    }

    [Serializable]
    public sealed class AbilityDefinitionRef
    {
        public string StableDefinitionId;
        public string SourceAssetGuid;
        public int SchemaVersion = 1;
        public AbilityDefinitionRef(string id, string guid)
        {
            StableDefinitionId = id ?? string.Empty;
            SourceAssetGuid = guid ?? string.Empty;
        }
    }

    public readonly struct AbilityIdentity : IEquatable<AbilityIdentity>
    {
        public readonly string DefinitionId;
        public readonly long OwnerActorRuntimeId;
        public readonly string SourceSlotId;
        public readonly int EntryIndex;
        public AbilityIdentity(string definitionId, long ownerActorRuntimeId, string sourceSlotId, int entryIndex)
        {
            DefinitionId = definitionId ?? string.Empty;
            OwnerActorRuntimeId = ownerActorRuntimeId;
            SourceSlotId = sourceSlotId ?? string.Empty;
            EntryIndex = entryIndex;
        }
        public bool Equals(AbilityIdentity other) =>
            DefinitionId == other.DefinitionId &&
            OwnerActorRuntimeId == other.OwnerActorRuntimeId &&
            SourceSlotId == other.SourceSlotId && EntryIndex == other.EntryIndex;
        public override bool Equals(object obj) => obj is AbilityIdentity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(DefinitionId, OwnerActorRuntimeId, SourceSlotId, EntryIndex);
    }

    public readonly struct CombatTargetRef
    {
        public readonly long ActorRuntimeId;
        public readonly string PartId;
        public readonly string SlotId;
        public CombatTargetRef(long actorId, string partId, string slotId)
        {
            ActorRuntimeId = actorId;
            PartId = partId ?? string.Empty;
            SlotId = slotId ?? string.Empty;
        }
    }

    public readonly struct AbilityEventContext
    {
        public readonly AbilityEventKind Kind;
        public readonly long ActionId;
        public readonly long CauseId;
        public readonly long OwnerActorRuntimeId;
        public readonly int RollUseIndex;
        public readonly bool DuelEffectsEligible;
        public readonly bool IsOneSided;
        public AbilityEventContext(AbilityEventKind kind, long actionId, long causeId,
            long ownerId, int rollUseIndex, bool duelEligible, bool oneSided)
        {
            Kind = kind; ActionId = actionId; CauseId = causeId;
            OwnerActorRuntimeId = ownerId; RollUseIndex = rollUseIndex;
            DuelEffectsEligible = duelEligible; IsOneSided = oneSided;
        }
    }

    public interface IReadOnlyAbilityContext
    {
        AbilityEventContext Event { get; }
        AbilityIdentity Ability { get; }
    }

    public interface IAbilityTriggerMatcher
    {
        bool Matches(in AbilityEventContext context);
    }

    public interface ICombatCondition
    {
        bool Evaluate(IReadOnlyAbilityContext context, CombatTargetRef candidate);
    }

    public interface ICombatTargetSelector
    {
        IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext context);
    }

    public interface IEffectOperation
    {
        void Plan(IReadOnlyAbilityContext context,
            IReadOnlyList<CombatTargetRef> targets, ICombatCommandSink sink);
    }

    public readonly struct CombatCommandIntent
    {
        public readonly string OperationKind;
        public readonly CombatTargetRef Target;
        public readonly int Magnitude;
        public CombatCommandIntent(string kind, CombatTargetRef target, int magnitude)
        {
            OperationKind = kind ?? string.Empty;
            Target = target;
            Magnitude = magnitude;
        }
    }

    public interface ICombatCommandSink
    {
        void Enqueue(CombatCommandIntent intent);
    }
}
