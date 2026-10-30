using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R5 command is a request, not confirmed damage/status application.
    /// The existing DamageManager / BattleEffectResolver / Resource service
    /// must be the only gameplay-write authorities once real adapters exist.
    /// </summary>
    public enum CombatCommandKind
    {
        Unknown = 0,
        DealDamage = 1,
        ApplyCharacterStatus = 2,
        ApplyPartStatus = 3,
        RemoveStatus = 4,
        ChangeResource = 5,
        RestoreHp = 6,
        ChangeSkillSlot = 7,
        ScheduleAbility = 8,
        RequestReactionAction = 9
    }

    public enum CombatCommandOutcome
    {
        Applied,
        PartiallyApplied,
        Absorbed,
        Rejected,
        Deferred,
        Failed,
        SkippedDuplicate,
        UnknownCommit
    }

    public readonly struct CombatCommandEnvelope
    {
        public string CommandId { get; }
        public AbilityIdentity Ability { get; }
        public CombatCommandKind Kind { get; }
        public long ActionId { get; }
        public long CauseEventId { get; }
        public CombatTargetRef Target { get; }
        public int RequestedAmount { get; }
        public int Phase { get; }
        public int Priority { get; }
        public int RegistrationOrder { get; }
        public long CommandSequence { get; }

        public CombatCommandEnvelope(string commandId, AbilityIdentity ability,
            CombatCommandKind kind, long actionId, long causeEventId,
            CombatTargetRef target, int requestedAmount, int phase,
            int priority, int registrationOrder, long commandSequence)
        {
            CommandId = commandId ?? string.Empty;
            Ability = ability;
            Kind = kind;
            ActionId = actionId;
            CauseEventId = causeEventId;
            Target = target;
            RequestedAmount = requestedAmount;
            Phase = phase;
            Priority = priority;
            RegistrationOrder = registrationOrder;
            CommandSequence = commandSequence;
        }
    }

    public readonly struct CombatCommandReceipt
    {
        public CombatCommandOutcome Outcome { get; }
        public int AppliedAmount { get; }
        public int AbsorbedAmount { get; }
        public bool EffectCommitted { get; }
        public string Detail { get; }

        public CombatCommandReceipt(CombatCommandOutcome outcome,
            int appliedAmount, int absorbedAmount, bool effectCommitted,
            string detail)
        {
            Outcome = outcome;
            AppliedAmount = Math.Max(0, appliedAmount);
            AbsorbedAmount = Math.Max(0, absorbedAmount);
            EffectCommitted = effectCommitted;
            Detail = detail ?? string.Empty;
        }

        public static CombatCommandReceipt Reject(string detail) =>
            new CombatCommandReceipt(CombatCommandOutcome.Rejected,0,0,false,detail);
    }

    public readonly struct CombatCommandAppliedRecord
    {
        public CombatCommandEnvelope Request { get; }
        public CombatCommandReceipt Receipt { get; }
        public CombatCommandAppliedRecord(CombatCommandEnvelope request, CombatCommandReceipt receipt)
        {
            Request = request; Receipt = receipt;
        }
    }

    public sealed class CombatCommandBatchResult
    {
        public IReadOnlyList<CombatCommandAppliedRecord> Records { get; }
        public int PlannedCount { get; }
        public int CommittedCount { get; }
        public int RejectedCount { get; }
        public bool HasUnknownCommit { get; }
        public bool HasCommitted => CommittedCount > 0;

        public CombatCommandBatchResult(List<CombatCommandAppliedRecord> records)
        {
            var stable = new List<CombatCommandAppliedRecord>(records ?? new List<CombatCommandAppliedRecord>());
            Records = stable.AsReadOnly();
            PlannedCount = stable.Count;
            int committed=0,rejected=0;bool unknown=false;
            foreach(var entry in stable)
            {
                if(entry.Receipt.EffectCommitted)committed++;
                if(entry.Receipt.Outcome==CombatCommandOutcome.Rejected ||
                   entry.Receipt.Outcome==CombatCommandOutcome.Failed ||
                   entry.Receipt.Outcome==CombatCommandOutcome.SkippedDuplicate)rejected++;
                if(entry.Receipt.Outcome==CombatCommandOutcome.UnknownCommit)unknown=true;
            }
            CommittedCount=committed;
            RejectedCount=rejected;
            HasUnknownCommit=unknown;
        }
    }

    public interface ICombatCommandBackend
    {
        bool CanApply(CombatCommandKind kind);
        // The real backend MUST call the existing specialist service exactly once
        // and report a committed result. It must not manufacture success.
        CombatCommandReceipt Apply(CombatCommandEnvelope request);
    }
}
