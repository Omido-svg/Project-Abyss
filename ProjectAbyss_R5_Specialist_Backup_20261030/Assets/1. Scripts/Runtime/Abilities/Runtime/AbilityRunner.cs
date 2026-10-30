using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R2 non-mutating plan boundary. R5 supplies the real Apply service;
    /// this class MUST NOT report planned intents as applied gameplay.
    /// </summary>
    public sealed class AbilityRunner
    {
        private sealed class ReadContext : IReadOnlyAbilityContext
        {
            public AbilityEventContext Event { get; }
            public AbilityIdentity Ability { get; }
            public ReadContext(AbilityIdentity identity, AbilityEventContext context)
            {
                Ability = identity; Event = context;
            }
        }

        public sealed class PlannedExecution
        {
            public AbilityExecutionResult Result { get; }
            public IReadOnlyList<CombatCommandIntent> Commands { get; }
            internal PlannedExecution(List<CombatCommandIntent> commands, string failure)
            {
                Commands = commands.AsReadOnly();
                Result = new AbilityExecutionResult(commands.Count, 0, failure);
            }
        }

        private sealed class Buffer : ICombatCommandSink
        {
            public readonly List<CombatCommandIntent> Commands = new List<CombatCommandIntent>();
            public void Enqueue(CombatCommandIntent command) => Commands.Add(command);
        }

        // The 6-argument R2 entrypoint remains compatible, treating its single
        // condition as a per-candidate predicate.
        public PlannedExecution Plan(AbilityInstance instance, AbilityEventContext context,
            IAbilityTriggerMatcher matcher, ICombatCondition condition,
            ICombatTargetSelector selector, IReadOnlyList<IEffectOperation> operations)
        {
            return Plan(instance, context, matcher, null, condition, selector, operations);
        }

        /// <summary>
        /// R3: PreTarget is checked BEFORE the candidate selector; Candidate
        /// conditions are evaluated on every SELECTED target. Neither stage
        /// submits commands or consumes real battle RNG.
        /// </summary>
        public PlannedExecution Plan(AbilityInstance instance, AbilityEventContext context,
            IAbilityTriggerMatcher matcher, ICombatCondition preTargetCondition,
            ICombatCondition candidateCondition, ICombatTargetSelector selector,
            IReadOnlyList<IEffectOperation> operations)
        {
            if (instance == null || matcher == null || selector == null)
                return new PlannedExecution(new List<CombatCommandIntent>(), "Invalid contracts");
            if (!matcher.Matches(in context))
                return new PlannedExecution(new List<CombatCommandIntent>(), "Trigger mismatch");
            ActivationKey key = new ActivationKey(context.ActionId, context.Kind, context.RollUseIndex);
            if (!instance.RuntimeState.CanActivate(key))
                return new PlannedExecution(new List<CombatCommandIntent>(), "Already committed");

            var view = new ReadContext(instance.Identity, context);
            if (preTargetCondition != null &&
                !preTargetCondition.Evaluate(view, default))
                return new PlannedExecution(new List<CombatCommandIntent>(), "PreTarget condition failed");
            var candidates = selector.Select(view);
            var valid = new List<CombatTargetRef>();
            if (candidates != null)
                foreach (var target in candidates)
                    if (candidateCondition == null || candidateCondition.Evaluate(view, target))
                        valid.Add(target);

            var buffer = new Buffer();
            if (operations != null)
                foreach (var op in operations)
                    op?.Plan(view, valid.AsReadOnly(), buffer);
            // R2 does not mutate BattleManager or produce DomainEvents.
            // R5 must validate/apply, then commit real results to memory.
            return new PlannedExecution(buffer.Commands, string.Empty);
        }
    }
}
