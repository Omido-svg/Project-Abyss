using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public enum ReactionScheduleStatus
    {
        Accepted,
        InvalidRequest,
        UnsupportedCheckpoint,
        Duplicate,
        NestedReactionRequiresPolicy,
        GuardExceeded
    }

    public readonly struct ReactionSchedulingDecision
    {
        public readonly ReactionScheduleStatus Status;
        public readonly ReactionRequest Request;
        public readonly string Diagnostic;
        public bool Accepted => Status == ReactionScheduleStatus.Accepted;
        public ReactionSchedulingDecision(ReactionScheduleStatus status,
            ReactionRequest request, string diagnostic)
        {
            Status=status;
            Request=request;
            Diagnostic=diagnostic ?? string.Empty;
        }
    }

    public readonly struct ReactionDispatchTrace
    {
        public readonly ReactionRequest Request;
        public readonly ReactionTimingCheckpoint AtCheckpoint;
        public readonly int DispatchOrdinal;
        public ReactionDispatchTrace(ReactionRequest request,
            ReactionTimingCheckpoint at, int ordinal)
        {
            Request=request;
            AtCheckpoint=at;
            DispatchOrdinal=ordinal;
        }
    }

    /// <summary>
    /// R6-Ia first stage: battle-scoped reaction INTENT queue.
    /// The caller of TryDequeue is the combat coordinator at an explicit
    /// checkpoint, NEVER an event handler recursively calling ResolvePair.
    /// This class does not create skills, spend resources, or apply damage.
    /// </summary>
    public sealed class BattleReactionScheduler : ICombatActionScheduler
    {
        private readonly List<ReactionRequest> pending=new List<ReactionRequest>();
        private readonly HashSet<long> knownIds=new HashSet<long>();
        private readonly Dictionary<long,int> actionsPerChain=new Dictionary<long,int>();
        private readonly List<ReactionSchedulingDecision> decisions=
            new List<ReactionSchedulingDecision>();
        private readonly List<ReactionDispatchTrace> trace=
            new List<ReactionDispatchTrace>();

        public int MaxPendingRequests { get; set; } = 128;
        public int MaxActionsPerChain { get; set; } = 64;
        public int MaxReactionDepth { get; set; } = 16;
        public int PendingCount => pending.Count;
        public IReadOnlyList<ReactionSchedulingDecision> Decisions => decisions.AsReadOnly();
        public IReadOnlyList<ReactionDispatchTrace> DispatchTrace => trace.AsReadOnly();
        public ReactionSchedulingDecision LastDecision { get; private set; }

        public bool TrySchedule(ReactionRequest request) =>
            Queue(request,allowReactionToReaction:false,depth:1).Accepted;

        public ReactionSchedulingDecision Queue(ReactionRequest request,
            bool allowReactionToReaction=false, int depth=1)
        {
            ReactionScheduleStatus status=ReactionScheduleStatus.Accepted;
            string note="Accepted; execution is deferred to the coordinator checkpoint.";

            if(request.RequestId<=0 || request.ParentActionId<=0 ||
               request.RootActionId<=0 || request.CauseEventId<=0 ||
               request.ChainId<=0 || request.OwnerActorRuntimeId<=0 ||
               string.IsNullOrWhiteSpace(request.SkillDefinitionId))
            {
                status=ReactionScheduleStatus.InvalidRequest;
                note="Invalid request/parent/root/cause/chain/owner/skill identity.";
            }
            else if(request.Checkpoint!=ReactionTimingCheckpoint.AfterExchangeSettled &&
                    request.Checkpoint!=ReactionTimingCheckpoint.AfterClashCompleted &&
                    request.Checkpoint!=ReactionTimingCheckpoint.AfterActionCompleted)
            {
                status=ReactionScheduleStatus.UnsupportedCheckpoint;
                note="Supported checkpoints: AfterExchangeSettled, AfterClashCompleted, AfterActionCompleted.";
            }
            else if(knownIds.Contains(request.RequestId))
            {
                status=ReactionScheduleStatus.Duplicate;
                note="RequestId already queued or dispatched; not replayed.";
            }
            else if(request.ParentActionId!=request.RootActionId &&
                    !allowReactionToReaction)
            {
                status=ReactionScheduleStatus.NestedReactionRequiresPolicy;
                note="Nested reaction requires explicit AllowReactionToReaction policy.";
            }
            else if(depth<=0 || depth>Math.Max(1,MaxReactionDepth) ||
                    pending.Count>=Math.Max(1,MaxPendingRequests) ||
                    (actionsPerChain.TryGetValue(request.ChainId,out var count) &&
                     count>=Math.Max(1,MaxActionsPerChain)))
            {
                status=ReactionScheduleStatus.GuardExceeded;
                note="Diagnostic guard exceeded: chain="+request.ChainId+
                    ",root="+request.RootActionId+
                    ",parent="+request.ParentActionId+
                    ",cause="+request.CauseEventId+
                    ",depth="+depth+
                    ",pending="+pending.Count;
            }

            var decision=new ReactionSchedulingDecision(status,request,note);
            LastDecision=decision;
            decisions.Add(decision);
            if(status==ReactionScheduleStatus.Accepted)
            {
                knownIds.Add(request.RequestId);
                pending.Add(request);
                actionsPerChain[request.ChainId]=
                    actionsPerChain.TryGetValue(request.ChainId,out var previous)
                    ? previous+1 : 1;
            }
            return decision;
        }

        private static int Compare(ReactionRequest left, ReactionRequest right)
        {
            int c=left.Priority.CompareTo(right.Priority);if(c!=0)return c;
            // CauseEventId is the current monotone event identifier. A distinct
            // EventSequence field may replace it after R6 trace integration.
            c=left.CauseEventId.CompareTo(right.CauseEventId);if(c!=0)return c;
            c=left.EnqueueSequence.CompareTo(right.EnqueueSequence);if(c!=0)return c;
            return left.RequestId.CompareTo(right.RequestId);
        }

        /// <summary>
        /// A coordinator explicitly calls this AFTER the current clash/action
        /// and its damage/events have finished committing. Never invoke from
        /// inside ResolvePair / a subscribed BattleEvent callback.
        /// </summary>
        public bool TryDequeue(ReactionTimingCheckpoint checkpoint,
            out ReactionRequest request)
        {
            request=default;
            if(checkpoint!=ReactionTimingCheckpoint.AfterExchangeSettled &&
               checkpoint!=ReactionTimingCheckpoint.AfterClashCompleted &&
               checkpoint!=ReactionTimingCheckpoint.AfterActionCompleted)
                return false;
            int best=-1;
            for(int i=0;i<pending.Count;i++)
            {
                if(pending[i].Checkpoint!=checkpoint)continue;
                if(best<0 || Compare(pending[i],pending[best])<0)
                    best=i;
            }
            if(best<0)return false;
            request=pending[best];
            pending.RemoveAt(best);
            trace.Add(new ReactionDispatchTrace(request,checkpoint,trace.Count));
            // Dequeued means permission to construct a NEW action; it does not
            // claim that the reaction succeeded or has been executed.
            return true;
        }

        public void ClearForBattleEnd()
        {
            pending.Clear();
            knownIds.Clear();
            actionsPerChain.Clear();
            decisions.Clear();
            trace.Clear();
            LastDecision=default;
        }
    }
}
