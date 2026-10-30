using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R4-Wa/b: explicit targetable slot snapshot. The live Character/ActionSlot
    /// adapters, taunt/stealth filtering and preview caching are implemented in
    /// later R4 integration. No UnityEngine.Random or source mutations here.
    /// </summary>
    public sealed class WeightedTargetSlot
    {
        public CombatSlotRef Slot { get; }
        public HitTargetKey HitTarget { get; }
        public int WeightCost { get; }
        public int OccupiedMainTargetCount { get; }
        public int AreaPriority { get; }
        public int SlotOrder { get; }
        public bool Eligible { get; }

        public WeightedTargetSlot(CombatSlotRef slot, HitTargetKey hitTarget,
            int cost, int occupiedMainCount, int areaPriority, int slotOrder, bool eligible = true)
        {
            Slot = slot;
            HitTarget = hitTarget;
            WeightCost = Math.Max(1, cost);
            OccupiedMainTargetCount = Math.Max(0, occupiedMainCount);
            AreaPriority = areaPriority;
            SlotOrder = slotOrder;
            Eligible = eligible;
        }
    }

    public enum SlotWeightedSelectionStatus
    {
        Selected,
        InvalidPrimary,
        ConflictingSlotSnapshot
    }

    public sealed class SlotWeightedSelectionResult
    {
        public SlotWeightedSelectionStatus Status { get; }
        public AttackWeightAllocation Allocation { get; }
        public int UnspentBudget { get; }
        public string Diagnostic { get; }
        public bool Success => Status == SlotWeightedSelectionStatus.Selected;

        internal SlotWeightedSelectionResult(SlotWeightedSelectionStatus status,
            AttackWeightAllocation allocation, int unspentBudget, string diagnostic)
        {
            Status = status;
            Allocation = allocation;
            UnspentBudget = Math.Max(0, unspentBudget);
            Diagnostic = diagnostic ?? string.Empty;
        }
    }

    public static class SlotWeightedTargetPlanner
    {
        /// <summary>
        /// Stable order: lowest main-target occupancy, explicit area priority,
        /// slot order, actor runtime ID, ordinal slot and part IDs, generation.
        /// The main slot is ALWAYS selected first regardless of secondary rank.
        /// </summary>
        private sealed class RankComparer : IComparer<WeightedTargetSlot>
        {
            public int Compare(WeightedTargetSlot a, WeightedTargetSlot b)
            {
                int c = a.OccupiedMainTargetCount.CompareTo(b.OccupiedMainTargetCount);
                if (c != 0) return c;
                c = a.AreaPriority.CompareTo(b.AreaPriority);
                if (c != 0) return c;
                c = a.SlotOrder.CompareTo(b.SlotOrder);
                if (c != 0) return c;
                c = a.Slot.ActorRuntimeId.CompareTo(b.Slot.ActorRuntimeId);
                if (c != 0) return c;
                c = string.CompareOrdinal(a.Slot.SlotId, b.Slot.SlotId);
                if (c != 0) return c;
                c = string.CompareOrdinal(a.Slot.PartId, b.Slot.PartId);
                if (c != 0) return c;
                c = a.Slot.Generation.CompareTo(b.Slot.Generation);
                if (c != 0) return c;
                c = a.HitTarget.ActorRuntimeId.CompareTo(b.HitTarget.ActorRuntimeId);
                if (c != 0) return c;
                return string.CompareOrdinal(a.HitTarget.PartId, b.HitTarget.PartId);
            }
        }

        private static bool IsValid(WeightedTargetSlot slot) =>
            slot != null && slot.Eligible &&
            slot.Slot.ActorRuntimeId > 0 &&
            !string.IsNullOrEmpty(slot.Slot.SlotId) &&
            slot.HitTarget.ActorRuntimeId > 0;

        public static SlotWeightedSelectionResult Plan(long actionId, int planningRevision,
            int weightBudget, WeightedTargetSlot primary,
            IReadOnlyList<WeightedTargetSlot> secondaryCandidates)
        {
            int budget = Math.Max(1, weightBudget);
            if (!IsValid(primary))
                return new SlotWeightedSelectionResult(
                    SlotWeightedSelectionStatus.InvalidPrimary, null, budget,
                    "Primary targetable slot is invalid or ineligible.");

            var eligible = new List<WeightedTargetSlot>();
            if (secondaryCandidates != null)
                foreach (var c in secondaryCandidates)
                    if (IsValid(c) && !c.Slot.Equals(primary.Slot))
                        eligible.Add(c);

            // A given SlotRef must resolve to one hit target and one cost. Do
            // not hide contradictory snapshots behind input-order-dependent de-dup.
            var bySlot = new Dictionary<CombatSlotRef, WeightedTargetSlot>();
            foreach (var c in eligible)
            {
                if (!bySlot.TryGetValue(c.Slot, out var old))
                {
                    bySlot.Add(c.Slot, c);
                    continue;
                }

                if (!old.HitTarget.Equals(c.HitTarget) ||
                    old.WeightCost != c.WeightCost ||
                    old.OccupiedMainTargetCount != c.OccupiedMainTargetCount ||
                    old.AreaPriority != c.AreaPriority ||
                    old.SlotOrder != c.SlotOrder)
                    return new SlotWeightedSelectionResult(
                        SlotWeightedSelectionStatus.ConflictingSlotSnapshot, null, budget,
                        "One CombatSlotRef has conflicting target/cost/priority snapshots.");
            }

            eligible = new List<WeightedTargetSlot>(bySlot.Values);
            eligible.Sort(new RankComparer());

            var selected = new List<SelectedSlotAllocation>();
            var hits = new List<HitTargetKey>();
            var seenHits = new HashSet<HitTargetKey>();

            void Add(WeightedTargetSlot slot, bool isPrimary, int rank)
            {
                int consumed = Math.Min(budget, slot.WeightCost);
                selected.Add(new SelectedSlotAllocation(slot.Slot,
                    isPrimary, slot.WeightCost, consumed, rank,
                    slot.OccupiedMainTargetCount));
                budget -= consumed;
                if (seenHits.Add(slot.HitTarget))
                    hits.Add(slot.HitTarget);
            }

            Add(primary, true, 0);
            for (int i = 0; i < eligible.Count && budget > 0; i++)
                Add(eligible[i], false, i + 1);

            var allocation = new AttackWeightAllocation(actionId, planningRevision,
                weightBudget, selected, hits);
            return new SlotWeightedSelectionResult(
                SlotWeightedSelectionStatus.Selected, allocation, budget, string.Empty);
        }
    }
}
