using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public enum WeightModifierPhase
    {
        AtPlanning,
        AtActionStart,
        OnRollUsing
    }

    public readonly struct WeightModifier
    {
        public readonly string StableModifierId;
        public readonly int Delta;
        public readonly WeightModifierPhase EffectiveFrom;
        public WeightModifier(string id, int delta, WeightModifierPhase phase)
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
    public readonly struct WeightSelectionKey : IEquatable<WeightSelectionKey>
    {
        public readonly long ActionId;
        public readonly int PlanningRevision;
        public readonly int TargetingVersion;
        public readonly string PolicyId;
        public WeightSelectionKey(long actionId, int revision,
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
        public bool Equals(WeightSelectionKey other) =>
            ActionId == other.ActionId &&
            PlanningRevision == other.PlanningRevision &&
            TargetingVersion == other.TargetingVersion &&
            string.Equals(PolicyId, other.PolicyId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WeightSelectionKey other && Equals(other);
        public override int GetHashCode() =>
            HashCode.Combine(ActionId,PlanningRevision,TargetingVersion,PolicyId);
    }

    public interface ICombatSlotWeightQuery
    {
        int GetSlotWeightCost(in CombatSlotRef target);
    }
    public interface ISlotOccupancyQuery
    {
        int GetExistingMainTargetCount(in CombatSlotRef target);
    }

    /// <summary>
    /// Freezes current candidate slot identities, hit keys, eligibility,
    /// occupancy, budget modifiers and policy. Never references a mutable SO
    /// or live Character; creates immutable copies of each snapshot entry.
    /// </summary>
    public sealed class AttackWeightSelectionSnapshot
    {
        private readonly WeightedTargetSlot primary;
        private readonly WeightedTargetSlot[] secondary;
        private readonly WeightModifier[] modifiers;
        public WeightSelectionKey Key { get; }
        public int BaseWeight { get; }
        public WeightModifierPhase CapturePhase { get; }
        public WeightedTargetSlot Primary => primary;
        public IReadOnlyList<WeightedTargetSlot> Secondary =>
            Array.AsReadOnly(secondary);
        public IReadOnlyList<WeightModifier> Modifiers =>
            Array.AsReadOnly(modifiers);

        public AttackWeightSelectionSnapshot(WeightSelectionKey key,
            int baseWeight, WeightedTargetSlot primary,
            IEnumerable<WeightedTargetSlot> secondary,
            IEnumerable<WeightModifier> modifiers,
            WeightModifierPhase capturedAt)
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
            this.modifiers=new List<WeightModifier>(
                modifiers ?? Array.Empty<WeightModifier>()).ToArray();
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
