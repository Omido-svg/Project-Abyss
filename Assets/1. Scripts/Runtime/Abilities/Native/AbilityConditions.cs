using System;

namespace ProjectAbyss.Abilities
{
    /// <summary>Readable constructors for the existing condition tree. No new expression evaluator.</summary>
    public static class AbilityConditions
    {
        public static AbilityConditionData Always() => new();
        public static AbilityConditionData RollSucceeded() => Custom("roll.succeeded");
        public static AbilityConditionData RollFailed() => Custom("roll.failed");
        public static AbilityConditionData CriticalRoll() => Custom("roll.critical");
        public static AbilityConditionData PartWeakened() => Custom("damage.weakened");
        public static AbilityConditionData PartBroken() => Custom("damage.broken");
        public static AbilityConditionData OpenedWindow() => Custom("damage.opened_window");
        public static AbilityConditionData PartWeakenedOrBroken() => Any(PartWeakened(), PartBroken());
        public static AbilityConditionData AppliedHpAtLeast(int amount) => Custom("damage.hp_at_least", value: NonNegative(amount));
        public static AbilityConditionData AppliedStaggerAtLeast(int amount) => Custom("damage.stagger_at_least", value: NonNegative(amount));

        public static AbilityConditionData OwnDirectCause(string skillId)
        {
            SkillRules.RequireId(skillId, nameof(skillId));
            return Custom("cause.direct", key: skillId);
        }

        public static AbilityConditionData All(params AbilityConditionData[] children) => Group(CombatConditionNodeKind.All, children);
        public static AbilityConditionData Any(params AbilityConditionData[] children) => Group(CombatConditionNodeKind.Any, children);
        public static AbilityConditionData Not(AbilityConditionData condition) => Group(CombatConditionNodeKind.Not, new[] { condition });

        public static AbilityConditionData Fact(ConditionSubject subject, ConditionScope scope, ConditionFact fact,
            string key, FactComparison comparison, int value = 1) => new()
        {
            Predicate = new AbilityPredicateData
            {
                TypeId = "fact", Subject = subject, Scope = scope, Fact = fact,
                Key = key, Comparison = comparison, Value = value
            }
        };

        /// <summary>Custom registered predicates use the same path as built-ins; raw data remains available for extra fields.</summary>
        public static AbilityConditionData Custom(string typeId, string key = null, int value = 1)
        {
            SkillRules.RequireId(typeId, nameof(typeId));
            return new AbilityConditionData { Predicate = new AbilityPredicateData { TypeId = typeId, Key = key, Value = value } };
        }

        private static AbilityConditionData Group(CombatConditionNodeKind kind, AbilityConditionData[] children)
        {
            if (children == null || children.Length == 0) throw new ArgumentException("At least one condition is required.", nameof(children));
            foreach (var child in children)
                if (child == null) throw new ArgumentException("A condition cannot be null.", nameof(children));
            return new AbilityConditionData { Kind = kind, Children = (AbilityConditionData[])children.Clone() };
        }

        private static int NonNegative(int amount) => amount >= 0 ? amount : throw new ArgumentOutOfRangeException(nameof(amount));
    }
}
