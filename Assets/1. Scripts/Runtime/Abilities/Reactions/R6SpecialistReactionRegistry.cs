using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Opt-in bridge for SPECIAL reactive gameplay that cannot be represented
    /// as a single ActionSlot (e.g. Hifumi's chained Chinchiro counter rolls).
    /// No skill calculation is moved here: the original specialist callback is
    /// executed exactly once at a coordinator checkpoint.
    /// </summary>
    public sealed class R6SpecialistReactionRegistry
    {
        private sealed class Entry
        {
            public readonly ReactionRequest Request;
            public readonly Func<bool> IsStillValid;
            public readonly Action ExecuteOriginalSpecialist;
            public Entry(ReactionRequest request,Func<bool> valid,Action perform)
            {
                Request=request;
                IsStillValid=valid;
                ExecuteOriginalSpecialist=perform;
            }
        }

        private readonly Dictionary<long,Entry> bindings=new Dictionary<long,Entry>();
        private readonly HashSet<long> consumed=new HashSet<long>();
        private readonly HashSet<long> unknownCommitChains=new HashSet<long>();

        public int PendingCount=>bindings.Count;
        public int ConsumedCount=>consumed.Count;
        public bool IsChainQuarantined(long chainId)=>unknownCommitChains.Contains(chainId);
        public bool Contains(long requestId)=>bindings.ContainsKey(requestId);

        public bool TryRegister(ReactionRequest request,Func<bool> stillValid,
            Action executeOriginalSpecialist,out string diagnostic)
        {
            diagnostic=null;
            if(request.RequestId<=0 || request.CauseEventId<=0 ||
                request.RootActionId<=0 || request.ParentActionId<=0 ||
                request.ChainId<=0 || request.OwnerActorRuntimeId<=0 ||
                string.IsNullOrWhiteSpace(request.SkillDefinitionId))
                diagnostic="Specialist request identity/provenance invalid.";
            else if(stillValid==null||executeOriginalSpecialist==null)
                diagnostic="Specialist callback and guard are required.";
            else if(bindings.ContainsKey(request.RequestId)||
                consumed.Contains(request.RequestId))
                diagnostic="Specialist RequestId was already registered or consumed.";

            if(diagnostic!=null)return false;
            bindings.Add(request.RequestId,new Entry(request,stillValid,executeOriginalSpecialist));
            return true;
        }

        public bool CancelUnscheduled(long requestId)
        {
            // Only used if the scheduler refused the corresponding request.
            // Once scheduler Queue accepts it, the binding is one-shot.
            return bindings.Remove(requestId);
        }

        /// <summary>Execution can be unknown-commit on an exception.
        /// It is NEVER retried by this registry.</summary>
        public bool TryExecute(ReactionRequest request,out string diagnostic)
        {
            diagnostic=null;
            if(!bindings.TryGetValue(request.RequestId,out var entry))
            {
                diagnostic="No registered specialist payload.";
                return false;
            }
            bindings.Remove(request.RequestId);
            consumed.Add(request.RequestId);

            var bound=entry.Request;
            if(unknownCommitChains.Contains(bound.ChainId))
            {
                diagnostic="Earlier specialist commit in this chain was indeterminate.";
                return false;
            }
            if(bound.CauseEventId!=request.CauseEventId ||
                bound.RootActionId!=request.RootActionId ||
                bound.ParentActionId!=request.ParentActionId ||
                bound.ChainId!=request.ChainId ||
                bound.OwnerActorRuntimeId!=request.OwnerActorRuntimeId ||
                bound.Checkpoint!=request.Checkpoint ||
                !string.Equals(bound.SkillDefinitionId,request.SkillDefinitionId,
                    StringComparison.Ordinal))
            {
                diagnostic="Specialist request provenance changed after registration.";
                return false;
            }

            try
            {
                if(!entry.IsStillValid())
                {
                    diagnostic="Specialist owner/source/target invalidated.";
                    return false;
                }
                entry.ExecuteOriginalSpecialist();
                return true;
            }
            catch(Exception exception)
            {
                // A specialist can have applied HP before throwing. Do not replay.
                unknownCommitChains.Add(request.ChainId);
                diagnostic="Unknown specialist commit; chain quarantined: "+
                    exception.GetType().Name;
                return false;
            }
        }

        public void ClearForBattleEnd()
        {
            bindings.Clear();
            consumed.Clear();
            unknownCommitChains.Clear();
        }
    }
}