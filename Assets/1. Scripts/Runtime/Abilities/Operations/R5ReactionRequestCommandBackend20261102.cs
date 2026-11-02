using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R5-I: command RequestReactionAction is a scheduler INTENT. It never
    /// constructs, consumes, executes or damages a reaction. R6 coordinator
    /// alone drains the queue at a permitted post-commit checkpoint.
    /// </summary>
    public sealed class R5ReactionRequestCommandBackend : ICombatCommandBackend
    {
        private readonly BattleReactionScheduler scheduler;
        private readonly Dictionary<string, ReactionRequest> payloads =
            new Dictionary<string, ReactionRequest>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReactionSchedulingDecision> decisions =
            new Dictionary<string, ReactionSchedulingDecision>(StringComparer.Ordinal);

        public R5ReactionRequestCommandBackend(BattleReactionScheduler scheduler)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }
        public int BoundRequestCount => payloads.Count;
        public bool CanApply(CombatCommandKind kind) =>
            kind == CombatCommandKind.RequestReactionAction;

        public void RegisterRequest(CombatCommandEnvelope command, ReactionRequest request)
        {
            if (command.Kind != CombatCommandKind.RequestReactionAction ||
                command.RequestedAmount != 0 ||
                string.IsNullOrWhiteSpace(command.CommandId) ||
                command.Target.ActorRuntimeId != request.OwnerActorRuntimeId ||
                request.ParentActionId != command.ActionId ||
                request.CauseEventId != command.CauseEventId ||
                request.RequestId <= 0 || request.RootActionId <= 0 ||
                request.ChainId <= 0 || request.OwnerActorRuntimeId <= 0 ||
                string.IsNullOrWhiteSpace(request.SkillDefinitionId))
                throw new ArgumentException("Reaction intent does not match command owner, parent or cause.");
            if (payloads.ContainsKey(command.CommandId))
                throw new InvalidOperationException("Duplicate registered reaction command ID.");
            payloads.Add(command.CommandId, request);
        }

        public CombatCommandReceipt Apply(CombatCommandEnvelope command)
        {
            if (!CanApply(command.Kind) ||
                !payloads.TryGetValue(command.CommandId, out var request))
                return CombatCommandReceipt.Reject("No validated reaction request is registered.");
            var decision = scheduler.Queue(request);
            decisions[command.CommandId] = decision;
            if (!decision.Accepted)
                return CombatCommandReceipt.Reject("Reaction queue refused request: " +
                    decision.Status + " / " + decision.Diagnostic);
            // Accepted to R6 queue, NOT an executed skill and NOT a gameplay
            // commit. No AbilityInstance activation is marked successful here.
            return new CombatCommandReceipt(CombatCommandOutcome.Scheduled,
                0, 0, false, "ReactionRequest queued; coordinator has not executed it.");
        }

        public bool TryGetDecision(string commandId, out ReactionSchedulingDecision result)
        {
            result = default;
            return commandId != null && decisions.TryGetValue(commandId, out result);
        }

        public void ClearForBattleEnd()
        {
            payloads.Clear();
            decisions.Clear();
        }
    }

    /// <summary>
    /// Typed R5 routing: exactly one backend may own each command kind.
    /// A reaction intent can share a batch with damage but is never
    /// accidentally routed to DamageManager or legacy SkillEffectDispatcher.
    /// </summary>
    public sealed class R5CompositeCommandBackend : ICombatCommandBackend
    {
        private readonly ICombatCommandBackend[] providers;
        public R5CompositeCommandBackend(params ICombatCommandBackend[] providers)
        {
            if (providers == null || providers.Length == 0)
                throw new ArgumentException("At least one backend required.");
            this.providers = (ICombatCommandBackend[])providers.Clone();
            foreach (var provider in this.providers)
                if (provider == null)
                    throw new ArgumentException("Null backend is not permitted.");
        }

        private ICombatCommandBackend FindUnique(CombatCommandKind kind)
        {
            ICombatCommandBackend selected = null;
            foreach (var provider in providers)
                if (provider.CanApply(kind))
                {
                    if (selected != null)
                        return null; // Ambiguous handlers must fail CLOSED.
                    selected = provider;
                }
            return selected;
        }
        public bool CanApply(CombatCommandKind kind) => FindUnique(kind) != null;
        public CombatCommandReceipt Apply(CombatCommandEnvelope command)
        {
            var provider = FindUnique(command.Kind);
            return provider == null
                ? CombatCommandReceipt.Reject("Command backend missing or ambiguous.")
                : provider.Apply(command);
        }
    }
}
