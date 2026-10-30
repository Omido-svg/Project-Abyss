using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Read-only bridge to live ActionSlot/CharacterSlotConfig/TargetPoint data.
    /// This is a QUERY only: does not add ActionSlots, pair clashes, spend energy,
    /// consume RNG, or change AttackWeightTargetResolver's legacy behavior.
    /// </summary>
    public sealed class R4LiveSlotSnapshot
    {
        public long SourceActionId { get; }
        public int PlanningRevision { get; }
        public IReadOnlyList<WeightedTargetSlot> Candidates { get; }
        public WeightedTargetSlot Primary { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public SlotWeightedSelectionResult Selection { get; }
        public bool HasUnambiguousMain => Primary != null;

        internal R4LiveSlotSnapshot(long actionId, int revision,
            List<WeightedTargetSlot> candidates, WeightedTargetSlot primary,
            List<string> messages, SlotWeightedSelectionResult selection)
        {
            SourceActionId = actionId;
            PlanningRevision = revision;
            Candidates = candidates.AsReadOnly();
            Primary = primary;
            Diagnostics = messages.AsReadOnly();
            Selection = selection;
        }
    }

    public static class R4LiveSlotQuery
    {
        // Runtime-scoped opaque ID, never serialized as character identity.
        // The Registry in R2-K must eventually own battle-local generations.
        public static long ActorRuntimeId(Character actor) =>
            actor == null ? 0 : unchecked((uint)actor.GetInstanceID());

        private static string PartId(BodyPart part) =>
            part == null ? string.Empty : (part.PartId ?? string.Empty);

        private static BodyPart ResolveLinkedPart(Character actor, CharacterSlotConfig config)
        {
            if (actor?.BodyParts == null || config == null || !config.HasLinkedPart)
                return null;
            if (!string.IsNullOrWhiteSpace(config.LinkedPartId))
                return actor.BodyParts.FirstOrDefault(p =>
                    p != null && string.Equals(p.PartId, config.LinkedPartId,
                        StringComparison.Ordinal));
            return actor.BodyParts.FirstOrDefault(p => p != null &&
                p.Type == config.LinkedPartType);
        }

        private static bool Targetable(Character actor, BodyPart part)
        {
            if (actor == null || actor.IsDead)
                return false;
            if (actor.UsesBodyParts)
                return part != null && part.Owner == actor && !part.IsBroken &&
                       actor.IsValidTargetPart(part);
            return part == null; // partless enemies: one actor-level hit
        }

        private static void Add(List<WeightedTargetSlot> list, Character actor,
            string slotId, BodyPart linkedPart, int order)
        {
            if (!Targetable(actor, linkedPart) ||
                string.IsNullOrWhiteSpace(slotId))
                return;
            var slotRef = new CombatSlotRef(ActorRuntimeId(actor), slotId,
                PartId(linkedPart), 0);
            if (list.Any(x => x.Slot.Equals(slotRef)))
                return;
            list.Add(new WeightedTargetSlot(slotRef,
                new HitTargetKey(ActorRuntimeId(actor), PartId(linkedPart)),
                cost: 1, occupiedMainCount: 0, areaPriority: 0,
                slotOrder: order));
        }

        private static List<WeightedTargetSlot> BuildCandidates(Character actor,
            IReadOnlyList<ActionSlot> planned, List<string> diagnostics)
        {
            var items = new List<WeightedTargetSlot>();
            if (actor == null || actor.IsDead)
                return items;

            // A configured slot exists even if no ActionSlot was planned.
            // Do not create a fake executable action to stand in for it.
            var configs = actor.Data?.ActionSlots;
            int configured = 0;
            if (configs != null)
            {
                for (int i = 0; i < configs.Count; i++)
                {
                    var cfg = configs[i];
                    if (cfg == null || !cfg.Enabled)
                        continue;
                    configured++;
                    Add(items, actor, cfg.SlotId,
                        ResolveLinkedPart(actor, cfg), i);
                }
            }

            // Additional runtime action slots may be created by rulebreakers.
            // Their identities are useful without mutating the execution queue.
            if (planned != null)
                foreach (var slot in planned)
                    if (slot?.Owner == actor && slot.Phase == ActionPhase.COMBAT &&
                        !slot.SkipResolution && slot.SlotConfig?.Enabled != false)
                        Add(items, actor,
                            string.IsNullOrWhiteSpace(slot.SlotId)
                                ? "RUNTIME:" + slot.ActionIndex + ":" + PartId(slot.Part)
                                : slot.SlotId,
                            slot.Part, slot.ActionIndex);

            if (configured == 0 && items.Count == 0)
            {
                // TargetPoint represents a targetable place, NOT a combat action.
                // For partless actors this produces a single actor-level target.
                var points = actor.GetTargetPoints(false);
                int index = 0;
                if (points != null)
                    foreach (var p in points)
                    {
                        if (p.IsValid && p.Character == actor)
                            Add(items, actor, "TARGETPOINT:" +
                                (p.Part == null ? "ACTOR" : PartId(p.Part)),
                                p.Part, index);
                        index++;
                    }
            }
            if (items.Count == 0)
                diagnostics.Add("No eligible targetable slots for actor " + ActorRuntimeId(actor));
            return items;
        }

        /// <summary>
        /// Snapshots ALL target-side actors. A caller must pass the explicitly
        /// valid hostile side; this method will not invent faction relationships.
        /// Candidate occupancy counts planned MAIN target slots only.
        /// A plan without TargetSlot is counted by part when unambiguous.
        /// </summary>
        public static R4LiveSlotSnapshot Capture(ActionSlot source,
            IReadOnlyList<Character> targetSide,
            IReadOnlyList<ActionSlot> currentPlans, int planningRevision,
            int effectiveWeight)
        {
            var messages = new List<string>();
            var candidates = new List<WeightedTargetSlot>();
            if (source == null || source.Owner == null ||
                source.TargetCharacter == null)
            {
                messages.Add("Missing source/owner/declared target.");
                return new R4LiveSlotSnapshot(source?.ActionId ?? 0,
                    planningRevision, candidates, null, messages,
                    SlotWeightedTargetPlanner.Plan(source?.ActionId ?? 0,
                        planningRevision, effectiveWeight, null, candidates));
            }

            var actors = new HashSet<Character>();
            if (targetSide != null)
                foreach (var actor in targetSide)
                    if (actor != null && actor != source.Owner && !actor.IsDead)
                        actors.Add(actor);
            if (!actors.Contains(source.TargetCharacter))
                messages.Add("Declared target is not in the supplied hostile side.");

            foreach (var actor in actors.OrderBy(ActorRuntimeId))
                candidates.AddRange(BuildCandidates(actor, currentPlans, messages));

            var occupancy = new Dictionary<CombatSlotRef, int>();
            if (currentPlans != null)
                foreach (var action in currentPlans)
                {
                    if (action == null || action.Phase != ActionPhase.COMBAT ||
                        action.SkipResolution)
                        continue;
                    var aimedActor = action.TargetSlot?.Owner ?? action.TargetCharacter;
                    if (aimedActor == null)
                        continue;
                    var aimedSlots = candidates.Where(c =>
                        c.Slot.ActorRuntimeId == ActorRuntimeId(aimedActor) &&
                        (action.TargetSlot != null
                            ? c.Slot.SlotId == action.TargetSlot.SlotId
                            : c.HitTarget.PartId == PartId(action.TargetPart)))
                        .ToArray();
                    if (aimedSlots.Length == 1)
                    {
                        var key = aimedSlots[0].Slot;
                        occupancy[key] = occupancy.TryGetValue(key, out int n) ? n + 1 : 1;
                    }
                    else if (aimedSlots.Length > 1)
                        messages.Add("Ambiguous planned main occupancy: action " + action.ActionId);
                }

            candidates = candidates.Select(s =>
                new WeightedTargetSlot(s.Slot, s.HitTarget, s.WeightCost,
                    occupancy.TryGetValue(s.Slot, out int count) ? count : 0,
                    s.AreaPriority, s.SlotOrder, s.Eligible)).ToList();

            var matches = candidates.Where(c =>
                c.Slot.ActorRuntimeId == ActorRuntimeId(source.TargetCharacter) &&
                (source.TargetSlot != null
                    ? source.TargetSlot.Owner == source.TargetCharacter &&
                      c.Slot.SlotId == source.TargetSlot.SlotId
                    : c.HitTarget.PartId == PartId(source.TargetPart)))
                .ToList();

            WeightedTargetSlot primary = null;
            if (actors.Contains(source.TargetCharacter) && matches.Count == 1)
            {
                var m = matches[0];
                // ActionSlot.TargetSlot (clash target) is distinct from the
                // explicitly aimed damage BodyPart (actual hit target).
                if (source.TargetPart == null || Targetable(source.TargetCharacter, source.TargetPart))
                    primary = new WeightedTargetSlot(m.Slot,
                        new HitTargetKey(ActorRuntimeId(source.TargetCharacter),
                            PartId(source.TargetPart)), m.WeightCost,
                        m.OccupiedMainTargetCount, m.AreaPriority, m.SlotOrder);
            }
            if (primary == null)
                messages.Add("Main target slot missing/ambiguous, or damage part invalid; explicit slot binding required.");

            var selection = SlotWeightedTargetPlanner.Plan(source.ActionId,
                planningRevision, effectiveWeight, primary, candidates);
            return new R4LiveSlotSnapshot(source.ActionId, planningRevision,
                candidates, primary, messages, selection);
        }
    }
}
