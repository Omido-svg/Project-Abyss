using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Battle-local R6-L execution lifetime. No shared-SO runtime counters.
    /// Request/Started/Applied are separate commitment points. This registry
    /// never executes gameplay and never infers "Applied" from a plan/result
    /// label. A content author may provide a factual committed-effect predicate.
    /// </summary>
    public sealed class ReactionLifetimeRegistry
    {
        private sealed class Group
        {
            public readonly AbilityLifetimeRules Rules;
            public readonly AbilityLifetimeMemory Memory;
            public Group(AbilityLifetimeRules rules)
            {
                Rules=rules;Memory=new AbilityLifetimeMemory(rules);
            }
            public bool SameRules(AbilityLifetimeRules r) =>
                Rules.OncePerTurn==r.OncePerTurn &&
                Rules.OncePerClash==r.OncePerClash &&
                Rules.OncePerRollUse==r.OncePerRollUse &&
                Rules.OncePerOriginHit==r.OncePerOriginHit &&
                Rules.FullCooldownTurns==r.FullCooldownTurns;
        }
        private sealed class Bound
        {
            public ReactionRequest Request;
            public AbilityLifetimeEvent Cause;
            public R3ReactionConsumeOn ConsumeOn;
            public Func<ClashResultContext,bool> HasAppliedEffect;
            public Group Group;
            public bool Started;
        }

        private readonly Dictionary<(long,string),Group> groups =
            new Dictionary<(long,string),Group>();
        private readonly Dictionary<long,Bound> requests =
            new Dictionary<long,Bound>();
        
        private readonly HashSet<long> deniedRequests = new HashSet<long>();
private readonly HashSet<long> completed = new HashSet<long>();
        private readonly HashSet<long> quarantinedChains=new HashSet<long>();

        public int PendingBindings => requests.Count;
        public int CompletedCount => completed.Count;
        public int QuarantinedChainCount => quarantinedChains.Count;

        public bool TryBindAccepted(ReactionSchedulingDecision queued,
            AbilityLifetimeRules rules,AbilityLifetimeEvent cause,
            R3ReactionConsumeOn consumeOn,
            Func<ClashResultContext,bool> hasAppliedEffect,
            out string reason)
        {
            reason=null;
            var r=queued.Request;
            if(!queued.Accepted || r.RequestId<=0 || r.ChainId<=0 ||
               r.CauseEventId<=0 || r.OwnerActorRuntimeId<=0 ||
               string.IsNullOrWhiteSpace(r.SkillDefinitionId) ||
               cause.CauseEventId!=r.CauseEventId || cause.TurnId<0)
                reason="A successfully scheduled, cause-matched request is required.";
            else if(consumeOn==R3ReactionConsumeOn.Applied &&
                    hasAppliedEffect==null)
                reason="Applied consumption requires factual applied-result evidence.";
            else if(completed.Contains(r.RequestId) ||
                    requests.ContainsKey(r.RequestId) ||
                    quarantinedChains.Contains(r.ChainId))
                reason="Duplicate or quarantined reaction request.";
            if(reason!=null)
            {
                if(queued.Accepted && !requests.ContainsKey(r.RequestId))
                    deniedRequests.Add(r.RequestId);
                return false;
            }

            var key=(r.OwnerActorRuntimeId,r.SkillDefinitionId);
            if(!groups.TryGetValue(key,out var group))
            {
                group=new Group(rules);
                groups.Add(key,group);
            }
            if(!group.SameRules(rules) || !group.Memory.CanActivate(cause))
            {
                reason="Lifetime rule mismatch, cooldown, or activation already consumed.";
                deniedRequests.Add(r.RequestId);
                return false;
            }
            // The scheduler has ALREADY accepted this request. Requested
            // consumption therefore cannot charge a rejected intent.
            if(consumeOn==R3ReactionConsumeOn.Requested &&
               !group.Memory.CommitIfApplied(cause,
                   new AbilityExecutionResult(1,1,null)))
            {
                reason="Unable to record successful Request acceptance.";
                deniedRequests.Add(r.RequestId);
                return false;
            }
            requests.Add(r.RequestId,new Bound{
                Request=r,Cause=cause,ConsumeOn=consumeOn,
                HasAppliedEffect=hasAppliedEffect,Group=group
            });
            return true;
        }

        private static bool Same(ReactionRequest a,ReactionRequest b) =>
            a.RequestId==b.RequestId &&
            a.ParentActionId==b.ParentActionId &&
            a.RootActionId==b.RootActionId &&
            a.CauseEventId==b.CauseEventId &&
            a.ChainId==b.ChainId &&
            a.OwnerActorRuntimeId==b.OwnerActorRuntimeId &&
            a.Checkpoint==b.Checkpoint &&
            string.Equals(a.SkillDefinitionId,b.SkillDefinitionId,
                StringComparison.Ordinal);

        /// <summary>
        /// Unbound requests retain legacy behavior. Bound ones must pass their
        /// lifetime immediately before the coordinator starts a real action.
        /// </summary>

        public bool MayStart(ReactionRequest request,out string reason)
        {
            reason=null;
            if(deniedRequests.Contains(request.RequestId))
            {
                reason="Rejected lifetime registration; scheduled request cannot bypass R6 policy.";
                return false;
            }
            if(quarantinedChains.Contains(request.ChainId))
            {
                reason="Unknown commit quarantined this reaction chain.";
                return false;
            }
            if(!requests.TryGetValue(request.RequestId,out var entry))
                return !completed.Contains(request.RequestId);
            if(!Same(entry.Request,request))
                reason="Bound reaction provenance was altered.";
            else if(entry.Started)
                reason="Reaction already started.";
            else if(entry.ConsumeOn!=R3ReactionConsumeOn.Requested &&
                    !entry.Group.Memory.CanActivate(entry.Cause))
                reason="Lifetime cooldown or once-per-identity rule denied start.";
            return reason==null;
        }

        /// <summary>
        /// Called AFTER the original ClashManager reports a completed
        /// reaction result. Commit Started only after an exchange really ran.
        /// Commit Applied only from a caller-supplied factual predicate.
        /// </summary>
        public bool Complete(ReactionRequest request,ClashResultContext result,
            out string diagnostic)
        {
            diagnostic=null;
            if(!requests.TryGetValue(request.RequestId,out var entry))
                return !completed.Contains(request.RequestId);
            requests.Remove(request.RequestId);
            completed.Add(request.RequestId);
            if(!Same(entry.Request,request))
            {
                diagnostic="Provenance mismatch at completion.";
                return false;
            }
            entry.Started=true;
            bool reallyStarted=result?.Exchanges!=null &&
                result.Exchanges.Exists(x=>x!=null && !x.WasCancelled);
            if(!reallyStarted)
            {
                diagnostic="No valid reaction exchange occurred.";
                return false;
            }
            if(entry.ConsumeOn==R3ReactionConsumeOn.Requested)
                return true;
            if(entry.ConsumeOn==R3ReactionConsumeOn.Applied &&
                !entry.HasAppliedEffect(result))
            {
                diagnostic="No attested gameplay effect committed.";
                return false;
            }
            if(!entry.Group.Memory.CommitIfApplied(entry.Cause,
                new AbilityExecutionResult(1,1,null)))
            {
                diagnostic="Lifetime commit rejected after reaction.";
                return false;
            }
            return true;
        }


        public bool CompleteSpecialist(ReactionRequest request,bool callbackExecuted,
            out string diagnostic)
        {
            diagnostic=null;
            if(!requests.TryGetValue(request.RequestId,out var entry))
                return !completed.Contains(request.RequestId);
            requests.Remove(request.RequestId);
            completed.Add(request.RequestId);
            if(!Same(entry.Request,request))
            {
                diagnostic="Specialist request lineage altered.";
                return false;
            }
            if(!callbackExecuted)
            {
                diagnostic="Specialist callback not executed.";
                return false;
            }
            if(entry.ConsumeOn==R3ReactionConsumeOn.Requested)return true;
            if(entry.ConsumeOn==R3ReactionConsumeOn.Applied)
            {
                diagnostic="No factual specialist effect result attested; Applied not consumed.";
                return false;
            }
            return entry.Group.Memory.CommitIfApplied(entry.Cause,
                new AbilityExecutionResult(1,1,null));
        }

        public void Cancel(ReactionRequest request)
        {

            deniedRequests.Remove(request.RequestId);
            requests.Remove(request.RequestId);
            completed.Add(request.RequestId);
        }

        public void QuarantineUnknownCommit(ReactionRequest request)
        {
            quarantinedChains.Add(request.ChainId);
            Cancel(request);
        }

        public bool TryGetMemory(long actorId,string skillId,
            out AbilityLifetimeMemory memory)
        {
            memory=null;
            if(!groups.TryGetValue((actorId,skillId??string.Empty),out var group))
                return false;
            memory=group.Memory;
            return true;
        }

        public void ClearForBattleEnd()
        {
            foreach(var group in groups.Values)
                group.Memory.ResetAt(AbilityLifetimeBoundary.Battle);
            groups.Clear();

            deniedRequests.Clear();
            requests.Clear();
            completed.Clear();
            quarantinedChains.Clear();
        }
    }
}
