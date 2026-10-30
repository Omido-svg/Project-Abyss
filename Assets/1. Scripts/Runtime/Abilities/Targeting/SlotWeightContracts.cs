using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    // R2-W contracts only. Legacy AttackWeightSettings still owns live behavior.
    public enum AttackWeightTargetingModel
    {
        LegacyCharacterRandom = 0,
        SlotWeighted = 1
    }

    public readonly struct CombatSlotRef : IEquatable<CombatSlotRef>
    {
        public readonly long ActorRuntimeId;
        public readonly string SlotId;
        public readonly string PartId;
        public readonly int Generation;
        public CombatSlotRef(long actorId, string slotId, string partId, int generation)
        {
            ActorRuntimeId = actorId;
            SlotId = slotId ?? string.Empty;
            PartId = partId ?? string.Empty;
            Generation = generation;
        }
        public bool Equals(CombatSlotRef other) =>
            ActorRuntimeId == other.ActorRuntimeId && SlotId == other.SlotId &&
            PartId == other.PartId && Generation == other.Generation;
        public override bool Equals(object obj) => obj is CombatSlotRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ActorRuntimeId, SlotId, PartId, Generation);
    }

    /// <summary>Different slots may resolve to the same physical hit target.</summary>
    public readonly struct HitTargetKey : IEquatable<HitTargetKey>
    {
        public readonly long ActorRuntimeId;
        public readonly string PartId; // empty for a normal enemy without parts
        public HitTargetKey(long actorId, string partId = null)
        {
            ActorRuntimeId = actorId; PartId = partId ?? string.Empty;
        }
        public bool Equals(HitTargetKey other) =>
            ActorRuntimeId == other.ActorRuntimeId && PartId == other.PartId;
        public override bool Equals(object obj) => obj is HitTargetKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ActorRuntimeId, PartId);
    }

    public readonly struct SelectedSlotAllocation
    {
        public readonly CombatSlotRef Slot;
        public readonly bool IsPrimary;
        public readonly int NominalSlotWeightCost;
        public readonly int ActuallyConsumedWeight;
        public readonly int PriorityRank;
        public readonly int ExistingTargetCount;
        public SelectedSlotAllocation(CombatSlotRef slot, bool primary,
            int nominalCost, int consumed, int rank, int occupiedCount)
        {
            Slot = slot; IsPrimary = primary;
            NominalSlotWeightCost = Math.Max(1, nominalCost);
            ActuallyConsumedWeight = Math.Max(0, consumed);
            PriorityRank = rank; ExistingTargetCount = occupiedCount;
        }
    }

    public sealed class AttackWeightAllocation
    {
        public long ActionId { get; }
        public int PlanningRevision { get; }
        public int EffectiveWeight { get; }
        public IReadOnlyList<SelectedSlotAllocation> SelectedSlots { get; }
        public IReadOnlyList<HitTargetKey> DistinctHitTargets { get; }
        public AttackWeightAllocation(long actionId, int revision, int weight,
            IReadOnlyList<SelectedSlotAllocation> slots,
            IReadOnlyList<HitTargetKey> hits)
        {
            ActionId = actionId; PlanningRevision = revision;
            EffectiveWeight = Math.Max(1, weight);
            SelectedSlots = Array.AsReadOnly(slots == null ? Array.Empty<SelectedSlotAllocation>() : new List<SelectedSlotAllocation>(slots).ToArray());
            DistinctHitTargets = Array.AsReadOnly(hits == null ? Array.Empty<HitTargetKey>() : new List<HitTargetKey>(hits).ToArray());
        }
    }
}
