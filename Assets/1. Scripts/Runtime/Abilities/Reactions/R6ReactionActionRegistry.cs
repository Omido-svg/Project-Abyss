using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Explicit one-shot binding between a queued reaction request and a NEW
    /// combat ActionSlot. Nothing is automatically inferred from legacy events.
    /// Registration and execution are opt-in; Hifumi's existing callback
    /// behavior is left intact until an evidence-backed migration.
    /// </summary>
    public sealed class R6ReactionActionRegistry
    {
        private readonly Dictionary<long,ActionSlot> bindings =
            new Dictionary<long,ActionSlot>();
        private readonly HashSet<long> claimedActionIds = new HashSet<long>();
        private readonly Dictionary<long,ReactionRequest> registeredRequests =
            new Dictionary<long,ReactionRequest>();

        public int PendingBindings => bindings.Count;

        public bool TryRegister(ReactionRequest request,ActionSlot action,
            out string diagnostic)
        {
            diagnostic=Validate(request,action);
            if(diagnostic!=null)
                return false;
            if(bindings.ContainsKey(request.RequestId) ||
               claimedActionIds.Contains(action.ActionId))
            {
                diagnostic="Duplicate request or reaction ActionId; a new action is required.";
                return false;
            }
            claimedActionIds.Add(action.ActionId);
            bindings.Add(request.RequestId,action);
            registeredRequests.Add(request.RequestId,request);
            return true;
        }

        public bool TryTake(ReactionRequest request,out ActionSlot action,
            out string diagnostic)
        {
            action=null;
            diagnostic=null;
            if(!bindings.TryGetValue(request.RequestId,out var planned))
            {
                diagnostic="No explicit reaction action registered for RequestId="+request.RequestId;
                return false;
            }
            // One-shot even if the target became invalid; no duplicate replay.
            bindings.Remove(request.RequestId);
            if(!registeredRequests.TryGetValue(request.RequestId,out var original) ||
               original.CauseEventId!=request.CauseEventId ||
               original.ParentActionId!=request.ParentActionId ||
               original.RootActionId!=request.RootActionId ||
               original.ChainId!=request.ChainId ||
               original.OwnerActorRuntimeId!=request.OwnerActorRuntimeId ||
               original.Checkpoint!=request.Checkpoint ||
               !string.Equals(original.SkillDefinitionId,request.SkillDefinitionId,
                   StringComparison.Ordinal))
            {
                registeredRequests.Remove(request.RequestId);
                diagnostic="Reaction request provenance changed after registration.";
                return false;
            }
            registeredRequests.Remove(request.RequestId);
            diagnostic=Validate(request,planned);
            if(diagnostic!=null)
                return false;
            action=planned;
            return true;
        }

        private static string Validate(ReactionRequest request,ActionSlot slot)
        {
            if(request.RequestId<=0 || request.RootActionId<=0 ||
               request.ParentActionId<=0 || request.CauseEventId<=0)
                return "Missing request provenance.";
            if(slot?.Owner==null || slot.Skill==null ||
               slot.TargetCharacter==null || slot.ActionId<=0 ||
               slot.Phase!=ActionPhase.COMBAT)
                return "Reaction action/owner/target/skill is incomplete.";
            // Reaction actions do not run through Planning. A producer cannot
            // claim their cost was prepaid without passing Planning's commit.
            if(slot.ResourceCostCommitted)
                return "Reaction must not forge a planning cost commit.";
            if(slot.ActionId==request.ParentActionId ||
               slot.ActionId==request.RootActionId)
                return "Reaction requires a distinct action ID.";
            if(R4LiveSlotQuery.ActorRuntimeId(slot.Owner)!=
                request.OwnerActorRuntimeId)
                return "Reaction owner is not the requesting actor.";
            var actualSkillId=slot.Skill.Definition?.SkillId ??
                slot.Skill.SkillName;
            if(!string.Equals(actualSkillId,request.SkillDefinitionId,
                StringComparison.Ordinal))
                return "Reaction skill does not match requested identity.";
            if(slot.Owner.IsDead || slot.TargetCharacter.IsDead ||
               slot.Part!=null && slot.Part.IsBroken ||
               slot.TargetPart!=null &&
               (slot.TargetPart.Owner!=slot.TargetCharacter ||
                !slot.TargetCharacter.IsValidTargetPart(slot.TargetPart)))
                return "Reaction owner/target/part is no longer valid.";
            if(slot.Owner==slot.TargetCharacter)
                return "Reaction owner cannot be its own combat target.";
            return null;
        }

        public void ClearForBattleEnd()
        {
            bindings.Clear();
            claimedActionIds.Clear();
            registeredRequests.Clear();
        }
    }
}
