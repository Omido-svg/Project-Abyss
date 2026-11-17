using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Authoring helpers only: every method produces the existing mutable rule data.
    /// Nothing here runs in the event/command execution path. Advanced fields remain available.
    /// </summary>
    public static class SkillRules
    {
        public static AbilityRuleSetDefinition CreateDraft(SkillDefinition source, string sourceGuid)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            RequireId(sourceGuid, nameof(sourceGuid));
            RequireId(source.SkillId, nameof(source.SkillId));
            var definition = ScriptableObject.CreateInstance<AbilityRuleSetDefinition>();
            definition.name = source.name + "Rules";
            definition.Enabled = false;
            definition.SourceKind = AbilityRuleSourceKind.Skill;
            definition.StableId = source.SkillId;
            definition.SourceAssetGuid = sourceGuid;
            definition.SourceAssetName = source.name;
            return definition;
        }

        public static AbilityRuleData OnUse(string id, params AbilityOperationData[] effects) =>
            Card(id, AbilityEventKind.RollUsing, effects);

        public static AbilityRuleData OnExchangeWin(string id, params AbilityOperationData[] effects) =>
            Card(id, AbilityEventKind.ExchangeWon, effects);

        public static AbilityRuleData OnExchangeLoss(string id, params AbilityOperationData[] effects) =>
            Card(id, AbilityEventKind.ExchangeLost, effects);

        public static AbilityRuleData OnClashCompleted(string id, params AbilityOperationData[] effects) =>
            Card(id, AbilityEventKind.ClashCompleted, effects);

        public static AbilityRuleData OnAttackResolutionStarting(string id, params AbilityOperationData[] effects) =>
            Card(id, AbilityEventKind.AttackResolutionStarting, effects);

        public static AbilityRuleData OnPreparationActivated(string id, params AbilityOperationData[] effects) =>
            Card(id, AbilityEventKind.PreparationActivated, effects);

        public static AbilityRuleData Card(string id, AbilityEventKind trigger, params AbilityOperationData[] effects)
        {
            if (!CanonicalEventPolicy.IsCanonicalAbilityCardTrigger(trigger))
                throw new ArgumentException("Use an internal-event or after-hit helper for this event.", nameof(trigger));
            return NewRule(id, trigger, effects);
        }

        /// <summary>Still owned by the skill and still subject to its duel gate; this is not a passive.</summary>
        public static AbilityRuleData OnInternalEvent(string id, AbilityEventKind trigger, params AbilityOperationData[] effects)
        {
            var rule = NewRule(id, trigger, effects);
            rule.Policy.CardAuthored = false;
            return rule;
        }

        public static AbilityRuleData OnOwnDirectMarkBurst(string id, string sourceSkillId, params AbilityOperationData[] effects)
        {
            RequireId(sourceSkillId, nameof(sourceSkillId));
            return OnInternalEvent(id, AbilityEventKind.MarkBurst, effects)
                .To(AbilityTargets.Self)
                .When(AbilityConditions.OwnDirectCause(sourceSkillId));
        }

        public static AbilityRuleData AfterWinningHit(string id, AbilityConditionData condition, params AbilityOperationData[] effects) =>
            AfterHit(id, AbilityDamageConditionOrigin.ExchangeWon, condition, effects);

        public static AbilityRuleData AfterUsingHit(string id, AbilityConditionData condition, params AbilityOperationData[] effects) =>
            AfterHit(id, AbilityDamageConditionOrigin.RollUsing, condition, effects);

        private static AbilityRuleData AfterHit(string id, AbilityDamageConditionOrigin origin,
            AbilityConditionData condition, AbilityOperationData[] effects)
        {
            RequireId(id, nameof(id));
            RequireEffects(effects);
            var rule = AbilityRuleAuthoringTemplates.AfterDamageEvaluation(id, origin, effects);
            return rule.When(condition);
        }

        /// <summary>
        /// Connects a scheduling operation, its delivery rule and its settings asset.
        /// Include Delivery in definition.Rules and persist Reservation as a subasset when saving.
        /// Calling this method alone neither registers a rule nor schedules a combat effect.
        /// </summary>
        public static AbilityNextTurnReward NextTurnReward(AbilityRuleSetDefinition definition,
            string deliveryRuleId, params AbilityOperationData[] effects)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.SourceKind != AbilityRuleSourceKind.Skill)
                throw new ArgumentException("SkillRules requires a skill-owned definition.", nameof(definition));
            var delivery = OnInternalEvent(deliveryRuleId, AbilityEventKind.TurnStarting, effects)
                .To(AbilityTargets.Self).AtSnapshot(AbilitySnapshotPolicy.LockAtScheduleCreation);
            delivery.ScheduledOnly = true;
            var reservation = ScriptableObject.CreateInstance<AbilityScheduleDefinition>();
            reservation.name = deliveryRuleId + "Reservation";
            reservation.Definition = definition;
            reservation.RuleId = deliveryRuleId;
            return new AbilityNextTurnReward(delivery, reservation);
        }

        public static AbilityRuleData To(this AbilityRuleData rule, string selectorId)
        {
            RequireRule(rule);
            RequireId(selectorId, nameof(selectorId));
            rule.TargetSelectorId = selectorId;
            return rule;
        }

        /// <summary>Replaces the condition. Use AbilityConditions.All/Any to compose multiple conditions.</summary>
        public static AbilityRuleData When(this AbilityRuleData rule, AbilityConditionData condition)
        {
            RequireRule(rule);
            rule.Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            return rule;
        }

        public static AbilityRuleData AtActionStart(this AbilityRuleData rule) =>
            rule.AtSnapshot(AbilitySnapshotPolicy.LockAtActionStart);

        public static AbilityRuleData AtSnapshot(this AbilityRuleData rule, AbilitySnapshotPolicy snapshot)
        {
            RequireRule(rule);
            rule.Policy.Snapshot = snapshot;
            return rule;
        }

        public static AbilityRuleData OncePerSkillExecution(this AbilityRuleData rule, bool consumeOnSchedule = false)
        {
            RequireRule(rule);
            rule.Lifetime.OncePerSkillExecution = true;
            rule.Lifetime.ConsumeOnSchedule = consumeOnSchedule;
            return rule;
        }

        public static AbilityRuleData OncePerTurn(this AbilityRuleData rule)
        {
            RequireRule(rule);
            rule.Lifetime.OncePerTurn = true;
            return rule;
        }

        public static AbilityRuleData WithCooldown(this AbilityRuleData rule, int fullTurns)
        {
            RequireRule(rule);
            if (fullTurns < 0) throw new ArgumentOutOfRangeException(nameof(fullTurns));
            rule.Lifetime.FullCooldownTurns = fullTurns;
            return rule;
        }

        private static AbilityRuleData NewRule(string id, AbilityEventKind trigger, AbilityOperationData[] effects)
        {
            RequireId(id, nameof(id));
            if (!Enum.IsDefined(typeof(AbilityEventKind), trigger)) throw new ArgumentOutOfRangeException(nameof(trigger));
            RequireEffects(effects);
            return new AbilityRuleData
            {
                RuleId = id, Trigger = trigger, RequiresDuelGate = true,
                Operations = (AbilityOperationData[])effects.Clone()
            };
        }

        private static void RequireEffects(AbilityOperationData[] effects)
        {
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            foreach (var effect in effects)
                if (effect == null) throw new ArgumentException("An effect cannot be null.", nameof(effects));
            // Empty effects are useful for disabled Inspector drafts; the existing validator blocks activation.
        }

        private static void RequireRule(AbilityRuleData rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
        }

        internal static void RequireId(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty identifier is required.", parameter);
        }
    }

    /// <summary>Authoring-time connection only. The runtime still receives the original data and settings types.</summary>
    public sealed class AbilityNextTurnReward
    {
        public AbilityRuleData Delivery { get; }
        public AbilityScheduleDefinition Reservation { get; }
        public AbilityOperationData Schedule { get; }

        internal AbilityNextTurnReward(AbilityRuleData delivery, AbilityScheduleDefinition reservation)
        {
            Delivery = delivery;
            Reservation = reservation;
            Schedule = AbilityEffects.Custom("schedule.next_turn", settings: reservation);
        }
    }
}
