using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public enum ClashFrameStepStatus
    {
        ExchangeCommitted,
        AwaitingCheckpoint,
        ReadyToStep,
        Completed,
        Invalidated,
        UnknownCommit
    }

    public readonly struct ClashFrameStepResult
    {
        public readonly ClashFrameStepStatus Status;
        public readonly long FrameId;
        public readonly long ActionId;
        public readonly int CommittedExchangeIndex;
        public readonly int NextExchangeIndex;
        public readonly string Diagnostic;
        public ClashFrameStepResult(ClashFrameStepStatus status,long frameId,
            long actionId,int committed,int next,string diagnostic)
        {
            Status=status;FrameId=frameId;ActionId=actionId;
            CommittedExchangeIndex=committed;NextExchangeIndex=next;
            Diagnostic=diagnostic??string.Empty;
        }
    }

    /// <summary>
    /// R6-Ib foundational cursor for a future ClashManager step adapter.
    /// IMPORTANT: this does NOT yet split existing ClashManager.ResolvePair.
    /// An actual ExchangeCommit callback is required to advance. Neither the
    /// frame nor the scheduler computes rolls or applies damage.
    /// </summary>
    public sealed class ClashResolutionFrame : ICombatExecutionFrame
    {
        private readonly Func<bool> canContinue;
        private readonly HashSet<int> committedExchanges=new HashSet<int>();
        private bool awaitingCheckpoint;
        private bool completed;
        private bool invalidated;
        private bool unknownCommit;
        private int nextIndex;
        public long FrameId { get; }
        public long ActionId { get; }
        public long ParentActionId { get; }
        public int TotalExchanges { get; }
        public int NextExchangeIndex => nextIndex;
        public bool AwaitingCheckpoint => awaitingCheckpoint;
        public bool IsComplete => completed;
        public bool IsInvalidated => invalidated;
        public bool HasUnknownCommit => unknownCommit;
        public IReadOnlyList<int> CommittedExchanges
        {
            get
            {
                var ordered=new List<int>(committedExchanges);
                ordered.Sort();
                return ordered.AsReadOnly();
            }
        }

        public ClashResolutionFrame(long frameId,long actionId,long parentActionId,
            int totalExchanges,Func<bool> canContinue)
        {
            if(frameId<=0 || actionId<=0 || totalExchanges<=0)
                throw new ArgumentOutOfRangeException(nameof(frameId),
                    "Frame/action ID and total exchange count must be positive.");
            FrameId=frameId;ActionId=actionId;ParentActionId=parentActionId;
            TotalExchanges=totalExchanges;
            this.canContinue=canContinue ?? throw new ArgumentNullException(nameof(canContinue));
        }

        private ClashFrameStepResult Result(ClashFrameStepStatus status,
            int committed,string diagnostic=null) =>
            new ClashFrameStepResult(status,FrameId,ActionId,committed,nextIndex,diagnostic);

        public ClashFrameStepResult Step(Func<int,bool> commitExchange)
        {
            if(unknownCommit)
                return Result(ClashFrameStepStatus.UnknownCommit,-1,
                    "Previous exchange commit threw: reconcile before restarting.");
            if(invalidated)
                return Result(ClashFrameStepStatus.Invalidated,-1,
                    "Original action/target invalidated before exchange.");
            if(awaitingCheckpoint)
                return Result(ClashFrameStepStatus.AwaitingCheckpoint,-1,
                    "Drain AfterExchangeSettled checkpoint before resuming.");
            if(completed)
                return Result(ClashFrameStepStatus.Completed,-1);
            if(commitExchange==null)
                throw new ArgumentNullException(nameof(commitExchange));
            if(!canContinue())
            {
                invalidated=true;
                return Result(ClashFrameStepStatus.Invalidated,-1,
                    "Owner/target/parts no longer valid.");
            }

            int index=nextIndex;
            if(committedExchanges.Contains(index))
                throw new InvalidOperationException("Exchange was already committed.");
            bool applied;
            try{applied=commitExchange(index);}
            catch(Exception e)
            {
                // The delegate may have applied HP before throwing. Do NOT retry.
                unknownCommit=true;
                return Result(ClashFrameStepStatus.UnknownCommit,-1,
                    "Unknown specialist commit: "+e.GetType().Name);
            }
            if(!applied)
            {
                invalidated=true;
                return Result(ClashFrameStepStatus.Invalidated,-1,
                    "Commit delegate rejected this exchange; no replay permitted.");
            }
            committedExchanges.Add(index);
            nextIndex++;
            awaitingCheckpoint=true;
            return Result(ClashFrameStepStatus.ExchangeCommitted,index);
        }

        public ClashFrameStepResult ResumeAfterCheckpoint()
        {
            if(unknownCommit)
                return Result(ClashFrameStepStatus.UnknownCommit,-1);
            if(invalidated)
                return Result(ClashFrameStepStatus.Invalidated,-1);
            if(!awaitingCheckpoint)
                throw new InvalidOperationException("No completed exchange awaits checkpoint.");
            awaitingCheckpoint=false;
            if(nextIndex>=TotalExchanges)
            {
                // Final exchange may legally kill the original target.
                completed=true;
                return Result(ClashFrameStepStatus.Completed,nextIndex-1);
            }
            if(!canContinue())
            {
                invalidated=true;
                return Result(ClashFrameStepStatus.Invalidated,-1,
                    "Reaction invalidated original action before resume.");
            }
            return Result(ClashFrameStepStatus.ReadyToStep,nextIndex-1,
                "Checkpoint drained; next exchange is ready.");
        }
    }
}
