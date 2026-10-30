using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R5 staging coordinator. This does not provide an alternative damage
    /// formula. No production backend is registered in this phase.
    /// Commands are stable-sorted, validated and delegated to one specialist
    /// backend. Unknown-commit exceptions are blocked from replay.
    /// </summary>
    public sealed class CombatCommandApplyService
    {
        private readonly HashSet<string> attempted = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> inFlight = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<AbilityIdentity, HashSet<ActivationKey>> unsafeActivations =
            new Dictionary<AbilityIdentity, HashSet<ActivationKey>>();
        public int MaxCommandsPerBatch { get; set; } = 128;
        public int UniqueAttemptCount => attempted.Count;

        private static int Order(CombatCommandEnvelope a, CombatCommandEnvelope b)
        {
            int c=a.Phase.CompareTo(b.Phase);if(c!=0)return c;
            c=a.Priority.CompareTo(b.Priority);if(c!=0)return c;
            c=a.RegistrationOrder.CompareTo(b.RegistrationOrder);if(c!=0)return c;
            c=string.CompareOrdinal(a.Ability.DefinitionId,b.Ability.DefinitionId);if(c!=0)return c;
            c=a.Ability.OwnerActorRuntimeId.CompareTo(b.Ability.OwnerActorRuntimeId);if(c!=0)return c;
            c=a.Ability.EntryIndex.CompareTo(b.Ability.EntryIndex);if(c!=0)return c;
            c=a.CommandSequence.CompareTo(b.CommandSequence);if(c!=0)return c;
            return string.CompareOrdinal(a.CommandId,b.CommandId);
        }

        public CombatCommandBatchResult Apply(AbilityInstance instance,
            AbilityEventContext cause, IReadOnlyList<CombatCommandEnvelope> commands,
            ICombatCommandBackend backend)
        {
            if(instance==null)throw new ArgumentNullException(nameof(instance));
            if(commands==null)throw new ArgumentNullException(nameof(commands));
            if(backend==null)throw new ArgumentNullException(nameof(backend));
            if(commands.Count>Math.Max(1,MaxCommandsPerBatch))
                throw new InvalidOperationException("R5 command-safety limit exceeded; no command was applied.");

            var ordered=new List<CombatCommandEnvelope>(commands);
            ordered.Sort(Order);
            var records=new List<CombatCommandAppliedRecord>();
            var key=new ActivationKey(cause.ActionId,cause.Kind,
                cause.RollUseIndex,cause.CauseId);
            foreach(var command in ordered)
            {
                CombatCommandReceipt receipt;
                if(string.IsNullOrWhiteSpace(command.CommandId) ||
                   command.ActionId!=cause.ActionId ||
                   command.CauseEventId!=cause.CauseId ||
                   !command.Ability.Equals(instance.Identity) ||
                   command.Kind==CombatCommandKind.Unknown ||
                   command.Target.ActorRuntimeId<=0)
                {
                    receipt=CombatCommandReceipt.Reject("Invalid command identity/cause/target.");
                    records.Add(new CombatCommandAppliedRecord(command,receipt));
                    continue;
                }

                if(attempted.Contains(command.CommandId) || inFlight.Contains(command.CommandId))
                {
                    receipt=new CombatCommandReceipt(CombatCommandOutcome.SkippedDuplicate,
                        0,0,false,"Command ID already attempted in this battle.");
                    records.Add(new CombatCommandAppliedRecord(command,receipt));
                    continue;
                }

                if(unsafeActivations.TryGetValue(instance.Identity,out var blocked) && blocked.Contains(key))
                {
                    receipt=CombatCommandReceipt.Reject("Indeterminate commit; reconciliation required.");
                    records.Add(new CombatCommandAppliedRecord(command,receipt));
                    continue;
                }
                if(!instance.RuntimeState.CanActivate(key))
                {
                    receipt=CombatCommandReceipt.Reject("Ability activation already committed.");
                    records.Add(new CombatCommandAppliedRecord(command,receipt));
                    continue;
                }
                if(!backend.CanApply(command.Kind))
                {
                    receipt=CombatCommandReceipt.Reject("No specialist backend for command.");
                    records.Add(new CombatCommandAppliedRecord(command,receipt));
                    continue;
                }

                if(!inFlight.Add(command.CommandId))
                    throw new InvalidOperationException("Reentrant command detected.");

                // Record before entering the foreign specialist service.
                // A thrown exception MAY follow a mutation: retry is unsafe.
                attempted.Add(command.CommandId);
                try
                {
                    receipt=backend.Apply(command);
                    // The backend must attest actual commit. Status labels alone
                    // are insufficient to mark gameplay as successfully applied.
                    if(receipt.EffectCommitted &&
                        (receipt.Outcome==CombatCommandOutcome.Rejected ||
                         receipt.Outcome==CombatCommandOutcome.Failed ||
                         receipt.Outcome==CombatCommandOutcome.Deferred))
                        receipt=new CombatCommandReceipt(CombatCommandOutcome.UnknownCommit,
                            receipt.AppliedAmount,receipt.AbsorbedAmount,false,
                            "Backend gave contradictory status and committed flag.");
                }
                catch(Exception e)
                {
                    receipt=new CombatCommandReceipt(CombatCommandOutcome.UnknownCommit,
                        0,0,false,"Backend threw; do NOT retry without reconciliation: "+e.GetType().Name);
                    records.Add(new CombatCommandAppliedRecord(command,receipt));
                    break; // stop this batch: following commands must not run
                }
                finally
                {
                    inFlight.Remove(command.CommandId);
                }
                records.Add(new CombatCommandAppliedRecord(command,receipt));
                // A backend may report UnknownCommit without throwing.
                // Do not execute the remainder of this batch in either case.
                if(receipt.Outcome==CombatCommandOutcome.UnknownCommit)
                    break;
            }

            var result=new CombatCommandBatchResult(records);
            if(result.HasUnknownCommit)
            {
                if(!unsafeActivations.TryGetValue(instance.Identity,out var blocked))
                {
                    blocked=new HashSet<ActivationKey>();
                    unsafeActivations[instance.Identity]=blocked;
                }
                blocked.Add(key);
            }
            if(result.CommittedCount>0 && !result.HasUnknownCommit)
                instance.RuntimeState.Commit(key,new AbilityExecutionResult(
                    result.PlannedCount,result.CommittedCount,null));
            return result;
        }

        public void ResetForBattle()
        {
            if(inFlight.Count>0)
                throw new InvalidOperationException("Cannot reset during command application.");
            attempted.Clear();
            unsafeActivations.Clear();
        }
    }
}
