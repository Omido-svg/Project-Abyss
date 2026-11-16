using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public enum PlanMutationKind
    {
        OtherActionAimChanged,
        SourceSkillChanged,
        SourceTargetChanged,
        SourceReassigned,
        AttackWeightPolicyChanged,
        ForcedTargetChanged,
        ExplicitReplan,
        BattleEnded
    }

    /// <summary>
    /// R4-Wc: battle-local snapshot cache. A separate action changing only its
    /// aim must not silently reroll the locked secondary allocation.
    /// Does not write back to ActionSlot or alter existing ClashBuilder/UI.
    /// </summary>
    public sealed class PlanningAllocationCache
    {
        private readonly Dictionary<long, LiveSlotSnapshot> snapshots =
            new Dictionary<long, LiveSlotSnapshot>();

        public int Count => snapshots.Count;

        public bool TryGet(long sourceActionId, out LiveSlotSnapshot snapshot) =>
            snapshots.TryGetValue(sourceActionId, out snapshot);

        public LiveSlotSnapshot GetOrCapture(long sourceActionId,
            Func<LiveSlotSnapshot> capture)
        {
            if (sourceActionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceActionId));
            if (snapshots.TryGetValue(sourceActionId, out var existing))
                return existing;
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));

            var created = capture();
            if (created == null || created.SourceActionId != sourceActionId)
                throw new InvalidOperationException(
                    "The captured allocation belongs to a different source action.");

            // Invalid primary selection is an error, not a stable cached plan.
            if (created.HasUnambiguousMain && created.Selection?.Success == true)
                snapshots.Add(sourceActionId, created);

            return created;
        }

        public bool Invalidate(long sourceActionId, PlanMutationKind reason)
        {
            if (reason == PlanMutationKind.BattleEnded)
            {
                Clear();
                return true;
            }
            if (reason == PlanMutationKind.OtherActionAimChanged)
                return false; // explicit fixed-plan policy

            return snapshots.Remove(sourceActionId);
        }

        public void Clear() => snapshots.Clear();
    }
}
