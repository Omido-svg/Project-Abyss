using System;
namespace ProjectAbyss.Abilities
{
    public enum ActionOrigin { Planned, Reaction, ExtraHit, Scripted }
    public enum ReactionTimingCheckpoint
    {
        AfterExchangeSettled,
        AfterClashCompleted,
        AfterActionCompleted,
        AfterDamageResultCommitted
    }

    /// <summary>
    /// A reaction request is an intention, not executable damage.
    /// R6 introduces the single BattleActionScheduler and frame suspension.
    /// </summary>
    public readonly struct ReactionRequest
    {
        public readonly long RequestId;
        public readonly long ParentActionId;
        public readonly long RootActionId;
        public readonly long CauseEventId;
        public readonly long ChainId;
        public readonly long OwnerActorRuntimeId;
        public readonly string SkillDefinitionId;
        public readonly ReactionTimingCheckpoint Checkpoint;
        public readonly int Priority;
        public readonly long EnqueueSequence;
        public ReactionRequest(long requestId, long parent, long root, long cause,
            long chain, long owner, string skillDefinitionId,
            ReactionTimingCheckpoint checkpoint, int priority, long sequence)
        {
            RequestId = requestId; ParentActionId = parent; RootActionId = root;
            CauseEventId = cause; ChainId = chain; OwnerActorRuntimeId = owner;
            SkillDefinitionId = skillDefinitionId ?? string.Empty;
            Checkpoint = checkpoint; Priority = priority; EnqueueSequence = sequence;
        }
    }

    public interface IReactionRequestSink
    {
        void Enqueue(ReactionRequest request);
    }

    public interface ICombatExecutionFrame
    {
        long FrameId { get; }
        long ActionId { get; }
        long ParentActionId { get; }
    }

    public interface ICombatActionScheduler
    {
        bool TrySchedule(ReactionRequest request);
    }
}
