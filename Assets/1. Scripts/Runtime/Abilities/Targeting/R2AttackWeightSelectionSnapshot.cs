using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public enum R2WeightModifierPhase
    {
        AtPlanning,
        AtActionStart,
        OnRollUsing
    }

    public readonly struct R2WeightModifier
    {
        public readonly string StableModifierId;
        public readonly int Delta;
        public readonly R2WeightModifierPhase EffectiveFrom;
        public R2WeightModifier(string id, int delta, R2WeightModifierPhase phase)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A stable modifier identity is required.");
            StableModifierId = id;
            Delta = delta;
            EffectiveFrom = phase;
        }
    }

    /// <summary>
    /// A versioned immutable key: a stale action/plan/target-policy result
    /// cannot be substituted for a newer target selection.
    /// </summary>
    public readonly struct R2WeightSelectionKey : IEquatable<R2WeightSelectionKey>
    {
        public readonly long ActionId;
        public readonly int PlanningRevision;
        public readonly int TargetingVersion;
        public readonly string PolicyId;
        public R2WeightSelectionKey(long actionId, int revision,
            int targetingVersion, string policyId)
        {
            if (actionId <= 0 || revision < 0 || targetingVersion < 0 ||
                string.IsNullOrWhiteSpace(policyId))
                throw new ArgumentException("Weight snapshot key requires action, revision, version and policy.");
            ActionId=actionId;
            PlanningRevision=revision;
            TargetingVersion=targetingVersion;
            PolicyId=policyId;
        }
        public bool Equals(R2WeightSelectionKey other) =>
            ActionId == other.ActionId &&
            PlanningRevision == other.PlanningRevision &&
            TargetingVersion == other.TargetingVersion &&
            string.Equals(PolicyId, other.PolicyId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is R2WeightSelectionKey other && Equals(other);
        public override int GetHashCode() =>
            HashCode.Combine(ActionId,PlanningRevision,TargetingVersion,PolicyId);
    }

    public interface IR2CombatSlotWeightQuery
    {
        int GetSlotWeightCost(in CombatSlotRef target);
    }
    public interface IR2SlotOccupancyQuery
    {
        int GetExistingMainTargetCount(in CombatSlotRef target);
    }

    /// <summary>
    /// Freezes current candidate slot identities, hit keys, eligibility,
    /// occupancy, budget modifiers and policy. Never references a mutable SO
    /// or live Character; creates immutable copies of each snapshot entry.
    /// </summary>
    public sealed class R2AttackWeightSelectionSnapshot
    {
        private readonly WeightedTargetSlot primary;
        private readonly WeightedTargetSlot[] secondary;
        private readonly R2WeightModifier[] modifiers;
        public R2WeightSelectionKey Key { get; }
        public int BaseWeight { get; }
        public R2WeightModifierPhase CapturePhase { get; }
        public WeightedTargetSlot Primary => primary;
        public IReadOnlyList<WeightedTargetSlot> Secondary =>
            Array.AsReadOnly(secondary);
        public IReadOnlyList<R2WeightModifier> Modifiers =>
            Array.AsReadOnly(modifiers);

        public R2AttackWeightSelectionSnapshot(R2WeightSelectionKey key,
            int baseWeight, WeightedTargetSlot primary,
            IEnumerable<WeightedTargetSlot> secondary,
            IEnumerable<R2WeightModifier> modifiers,
            R2WeightModifierPhase capturedAt)
        {
            Key=key;
            BaseWeight=Math.Max(1,baseWeight);
            CapturePhase=capturedAt;
            this.primary=Copy(primary);
            var candidates=new List<WeightedTargetSlot>();
            if (secondary!=null)
                foreach(var candidate in secondary)
                    if(candidate!=null)candidates.Add(Copy(candidate));
            this.secondary=candidates.ToArray();
            this.modifiers=new List<R2WeightModifier>(
                modifiers ?? Array.Empty<R2WeightModifier>()).ToArray();
        }

        private static WeightedTargetSlot Copy(WeightedTargetSlot value) =>
            value == null ? null :
            new WeightedTargetSlot(value.Slot,value.HitTarget,value.WeightCost,
                value.OccupiedMainTargetCount,value.AreaPriority,
                value.SlotOrder,value.Eligible);

        public int EffectiveWeight
        {
            get
            {
                long value=BaseWeight;
                foreach(var modifier in modifiers)
                    if(modifier.EffectiveFrom <= CapturePhase)
                        value+=modifier.Delta;
                return (int)Math.Max(1,Math.Min(int.MaxValue,value));
            }
        }

        public SlotWeightedSelectionResult Plan() =>
            SlotWeightedTargetPlanner.Plan(Key.ActionId,Key.PlanningRevision,
                EffectiveWeight,primary,secondary);
    }
}
