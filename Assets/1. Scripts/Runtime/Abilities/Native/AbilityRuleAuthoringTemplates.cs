using System;

namespace ProjectAbyss.Abilities
{
    /// <summary>Safe authoring defaults for a canonical card condition evaluated after its own damage.</summary>
    public static class AbilityRuleAuthoringTemplates
    {
        public static AbilityRuleData AfterDamageEvaluation(string ruleId,AbilityDamageConditionOrigin origin,
            params AbilityOperationData[] operations)
        {
            if(origin!=AbilityDamageConditionOrigin.RollUsing&&origin!=AbilityDamageConditionOrigin.ExchangeWon)
                throw new ArgumentException("Choose the canonical use or exchange-win origin.",nameof(origin));
            return new AbilityRuleData
            {
                RuleId=ruleId,Trigger=AbilityEventKind.DamageSettled,TargetSelectorId="self",RequiresDuelGate=true,
                Policy=new AbilityRuleMetadata{CardAuthored=false,Snapshot=AbilitySnapshotPolicy.AfterCommittedDamage,
                    ConditionPhase=CombatConditionPhase.PostDamageResult,DamageConditionOrigin=origin},
                Condition=new AbilityConditionData{Kind=CombatConditionNodeKind.Any,Children=new[]{
                    new AbilityConditionData{Predicate=new AbilityPredicateData{TypeId="damage.weakened"}},
                    new AbilityConditionData{Predicate=new AbilityPredicateData{TypeId="damage.broken"}}}},
                Operations=operations??Array.Empty<AbilityOperationData>()
            };
        }
    }
}
