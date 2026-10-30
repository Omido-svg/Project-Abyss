using System;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R6-Ic ordered proof of domain/presentation causality. Records are
    /// informational; they NEVER drive gameplay, damage or scheduling.
    /// </summary>
    public enum R6StepTraceKind
    {
        ExchangeCommitted = 0,
        ExchangeVisualCompleted = 1,
        ReactionCommitted = 2,
        ReactionVisualCompleted = 3,
        OriginalActionCompleted = 4
    }

    public readonly struct R6StepTraceRecord
    {
        public readonly int Sequence;
        public readonly R6StepTraceKind Kind;
        public readonly long ActionId;
        public readonly long RootActionId;
        public readonly long CauseEventId;
        public readonly int ExchangeIndex;

        public R6StepTraceRecord(int sequence, R6StepTraceKind kind,
            long actionId, long rootActionId, long causeEventId,
            int exchangeIndex)
        {
            Sequence = sequence;
            Kind = kind;
            ActionId = actionId;
            RootActionId = rootActionId;
            CauseEventId = causeEventId;
            ExchangeIndex = exchangeIndex;
        }
    }
}