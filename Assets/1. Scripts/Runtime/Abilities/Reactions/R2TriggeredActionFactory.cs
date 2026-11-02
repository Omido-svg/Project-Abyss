using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R2-I: immutable parent/root/chain provenance. A request is an intent;
    /// the new combat action owns an independent ActionId and Skill instance.
    /// </summary>
    public sealed class R2TriggeredActionFrame
    {
        public long FrameId { get; }
        public long ActionId { get; }
        public long ParentActionId { get; }
        public long RootActionId { get; }
        public long CauseEventId { get; }
        public long ChainId { get; }
        public long RequestId { get; }
        public ActionOrigin Origin => ActionOrigin.Reaction;
        public BattleAction Action { get; }
        public AbilityInstance Ability { get; }

        internal R2TriggeredActionFrame(ReactionRequest request, BattleAction action,
            AbilityInstance ability)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            Ability = ability ?? throw new ArgumentNullException(nameof(ability));
            FrameId = ActionId = action.ActionId;
            ParentActionId = request.ParentActionId;
            RootActionId = request.RootActionId;
            CauseEventId = request.CauseEventId;
            ChainId = request.ChainId;
            RequestId = request.RequestId;
        }
    }

    /// <summary>
    /// R2-I draft factory: NO scheduling, damage, registration or resource
    /// consumption. R6 alone can decide when/how to execute these drafts.
    /// Rejected draft IDs are never committed to the uniqueness registry.
    /// </summary>
    public sealed class R2TriggeredActionFactory : IR2TriggeredActionFactory
    {
        private readonly HashSet<long> allocatedActions = new HashSet<long>();
        private readonly IR2ReactionTimingPolicy timingPolicy;
        public R2TriggeredActionFactory(IR2ReactionTimingPolicy timingPolicy = null)
        {
            this.timingPolicy = timingPolicy ?? new R2CanonicalReactionTimingPolicy();
        }
        public int CreatedCount => allocatedActions.Count;

        public bool TryCreate(ReactionRequest request, long newActionId,
            Character owner, Character target, SkillDefinition definition,
            BodyPart ownerPart, BodyPart targetPart,
            out R2TriggeredActionFrame frame, out string error)
        {
            frame = null;
            error = null;
            if (newActionId <= 0 || newActionId == request.RootActionId ||
                newActionId == request.ParentActionId ||
                allocatedActions.Contains(newActionId))
                error = "New reaction must have an unused ActionId distinct from parent/root.";
            else if (request.RequestId <= 0 || request.RootActionId <= 0 ||
                     request.ParentActionId <= 0 || request.CauseEventId <= 0 ||
                     request.ChainId <= 0)
                error = "Reaction provenance is missing.";
            else if (owner == null || target == null || definition == null ||
                     owner.IsDead || target.IsDead)
                error = "Missing or dead reaction owner/target/definition.";
            else if (owner == target)
                error = "Reaction owner cannot target itself in this combat draft.";
            else if (R4LiveSlotQuery.ActorRuntimeId(owner) != request.OwnerActorRuntimeId)
                error = "OwnerActorRuntimeId does not match the actual actor.";
            else if (!string.Equals(definition.SkillId, request.SkillDefinitionId,
                         StringComparison.Ordinal))
                error = "Requested skill identity does not match.";
            else if ((ownerPart != null && (ownerPart.Owner != owner || ownerPart.IsBroken)) ||
                     (targetPart != null && (targetPart.Owner != target ||
                         !target.IsValidTargetPart(targetPart))))
                error = "Owner or target part is invalid.";
            if (error == null && !timingPolicy.CanConstructAt(in request, out var timingProblem))
                error = timingProblem;
            
if (error != null) return false;

            // Every request allocates its own skill runtime, action state and
            // ability memory. The shared SkillDefinition is NEVER mutated.
            // Honor the owner's character-specific skill factory; a reaction must
            // not silently downgrade Yujin/Hifumi/Olaf to generic DataSkill.
            Skill skill = owner.CreateRuntimeSkillForLoadout(definition);
            if (skill == null)
            {
                error = "Runtime skill construction failed.";
                return false;
            }
            skill.Initialize(owner, owner.BattleEvent);
            string sourceSlotId = "R2-REACTION-" + newActionId;
            var slot = new ActionSlot
            {
                ActionId = newActionId,
                Owner = owner,
                Part = ownerPart,
                Skill = skill,
                TargetCharacter = target,
                TargetPart = targetPart,
                SlotId = sourceSlotId,
                Phase = ActionPhase.COMBAT,
                ResourceCostCommitted = false
            };
            var action = new BattleAction { Slot = slot };
            var ability = new AbilityInstance(
                new AbilityDefinitionRef(definition.SkillId, string.Empty),
                new AbilityIdentity(definition.SkillId,
                    request.OwnerActorRuntimeId, sourceSlotId, 0));
            frame = new R2TriggeredActionFrame(request, action, ability);
            allocatedActions.Add(newActionId);
            return true;
        }

        public void ClearForBattleEnd() => allocatedActions.Clear();
    }
}
