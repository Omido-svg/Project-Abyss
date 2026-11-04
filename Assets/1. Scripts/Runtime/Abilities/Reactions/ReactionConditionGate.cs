using System;

namespace ProjectAbyss.Abilities
{
    public enum R3ReactionCostPolicy { Free, PayNormalSkillCost, CustomCost }
    public enum R3ReactionConsumeOn { Requested, Started, Applied }

    public readonly struct R3ReactionRequestSnapshot
    {
        public readonly ReactionRequest Request;
        public readonly AbilityEventContext CauseEvent;
        public readonly AppliedDamageEvidence HistoricalDamage;
        public readonly bool HasHistoricalDamage;
        public readonly R3ReactionCostPolicy CostPolicy;
        public readonly R3ReactionConsumeOn ConsumeOn;
        public readonly int RequiredCost;

        internal R3ReactionRequestSnapshot(ReactionRequest request,
            AbilityEventContext causeEvent, AppliedDamageEvidence historical,
            bool hasDamage, R3ReactionCostPolicy costPolicy,
            R3ReactionConsumeOn consumeOn, int requiredCost)
        {
            Request=request;CauseEvent=causeEvent;
            HistoricalDamage=historical;HasHistoricalDamage=hasDamage;
            CostPolicy=costPolicy;ConsumeOn=consumeOn;
            RequiredCost=requiredCost;
        }
    }

    /// <summary>
    /// R3-I: strict two-stage read-only condition gate. Capturing the result
    /// of the original event does not spend resources or execute a reaction.
    /// At execution, current survival, part, skill and cost are revalidated.
    /// R5/R6 perform any actual resource transaction or action execution.
    /// </summary>
    public sealed class R3ReactionConditionGate
    {
        public bool TryCaptureRequest(ReactionRequest request,
            AbilityEventContext causeEvent,bool eventConditionPassed,
            bool lifetimeMayRequest,R3ReactionCostPolicy costPolicy,
            R3ReactionConsumeOn consumeOn,int requiredCost,
            bool hasCommittedDamage,AppliedDamageEvidence historicalDamage,
            out R3ReactionRequestSnapshot snapshot,out string error)
        {
            snapshot=default;error=null;
            if(request.RequestId<=0 || request.OwnerActorRuntimeId<=0 ||
               request.CauseEventId<=0 || request.ParentActionId<=0 ||
               request.RootActionId<=0 || request.ChainId<=0 ||
               request.CauseEventId!=causeEvent.CauseId)
                error="Request origin mismatch or invalid provenance";
            else if(!eventConditionPassed||!lifetimeMayRequest)
                error="Trigger or lifetime request condition rejected";
            else if(requiredCost<0 ||
                    costPolicy==R3ReactionCostPolicy.Free&&requiredCost!=0)
                error="Invalid cost contract";
            else if(causeEvent.Kind==AbilityEventKind.DamageSettled &&
                    (!hasCommittedDamage||
                     historicalDamage.CauseEventId!=causeEvent.CauseId||
                     historicalDamage.OriginHitId<=0||
                     historicalDamage.OriginRollUseId<=0))
                error="A committed matching result is required";
            if(error!=null)return false;
            snapshot=new R3ReactionRequestSnapshot(request,causeEvent,historicalDamage,
                hasCommittedDamage,costPolicy,consumeOn,requiredCost);
            return true;
        }

        public bool ValidateAtExecution(in R3ReactionRequestSnapshot snapshot,
            Character owner,Character target,BodyPart ownerPart,BodyPart targetPart,
            bool requestedSkillAvailable,int currentlyAvailableResource,
            out string reason)
        {
            reason=null;
            if(snapshot.Request.RequestId<=0)
                reason="Missing captured request";
            else if(owner==null || owner.IsDead || target==null || target.IsDead)
                reason="Owner or target dead/missing";
            else if(R4LiveSlotQuery.ActorRuntimeId(owner)!=snapshot.Request.OwnerActorRuntimeId)
                reason="Wrong reaction owner";
            else if(!requestedSkillAvailable)
                reason="Skill no longer available";
            else if(ownerPart!=null &&
                    (ownerPart.Owner!=owner||ownerPart.IsBroken))
                reason="Owner part broken/unusable";
            else if(targetPart!=null &&
                    (targetPart.Owner!=target||!target.IsValidTargetPart(targetPart)))
                reason="Target part no longer available";
            else if(snapshot.CostPolicy!=R3ReactionCostPolicy.Free &&
                    currentlyAvailableResource<snapshot.RequiredCost)
                reason="Insufficient resource at action start";
            return reason==null;
        }
    }
}
