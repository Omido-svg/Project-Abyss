using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public enum AbilityCausePolicy { PreserveDirectParent, DerivedEvent }
    public enum AbilityDamageConditionOrigin { Unspecified=0, RollUsing=1, ExchangeWon=2 }
    [Serializable]
    public sealed class AbilityRuleMetadata
    {
        public AbilitySnapshotPolicy Snapshot=AbilitySnapshotPolicy.LiveAtTrigger;
        public AbilityOwnerScope OwnerScope=AbilityOwnerScope.Actor;
        public CombatConditionPhase ConditionPhase=CombatConditionPhase.PreTarget;
        public AbilityCausePolicy CausePolicy=AbilityCausePolicy.PreserveDirectParent;
        public bool CardAuthored=true;
        public AbilityDamageConditionOrigin DamageConditionOrigin;
        public bool RequiresConfiguredValue;
        public string PendingDesignReason;

        public IReadOnlyList<string> Validate(AbilityEventKind trigger)
        {
            var issues=new List<string>();
            if(!Enum.IsDefined(typeof(AbilitySnapshotPolicy),Snapshot) ||
               !Enum.IsDefined(typeof(AbilityOwnerScope),OwnerScope) ||
               !Enum.IsDefined(typeof(CombatConditionPhase),ConditionPhase) ||
               !Enum.IsDefined(typeof(AbilityCausePolicy),CausePolicy) ||
               !Enum.IsDefined(typeof(AbilityDamageConditionOrigin),DamageConditionOrigin))issues.Add("INVALID_RULE_POLICY");
            if(CardAuthored && !CanonicalEventPolicy.IsCanonicalAbilityCardTrigger(trigger))
                issues.Add("INTERNAL_EVENT_IS_NOT_CARD_TRIGGER");
            if((Snapshot==AbilitySnapshotPolicy.AfterCommittedDamage || ConditionPhase==CombatConditionPhase.PostDamageResult) &&
                trigger!=AbilityEventKind.DamageSettled)issues.Add("COMMITTED_DAMAGE_REQUIRED");
            if(Snapshot==AbilitySnapshotPolicy.TurnEndFinalSnapshot && trigger!=AbilityEventKind.TurnEnding)
                issues.Add("FINAL_TURN_SNAPSHOT_REQUIRED");
            if(DamageConditionOrigin!=AbilityDamageConditionOrigin.Unspecified &&
               (CardAuthored || trigger!=AbilityEventKind.DamageSettled || Snapshot!=AbilitySnapshotPolicy.AfterCommittedDamage ||
                ConditionPhase!=CombatConditionPhase.PostDamageResult))issues.Add("INVALID_DEFERRED_CARD_DAMAGE_POLICY");
            if(RequiresConfiguredValue)issues.Add("BLOCKED_BY_UNDECIDED_RULE: "+(PendingDesignReason??""));
            return issues.AsReadOnly();
        }
    }

    public enum CombatEventFact { RollSucceeded, RollFailed, CriticalRoll, ClashOnly, OneSidedOnly }
    public sealed class CombatEventFactCondition : ICombatCondition
    {
        private readonly CombatEventFact fact;
        public CombatEventFactCondition(CombatEventFact fact){this.fact=fact;}
        public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef candidate)
        {
            if(!(context is IReadOnlyAbilityExecutionContext read) || read.Frame==null)return false;
            var f=read.Frame.Facts;
            return fact switch
            {
                CombatEventFact.RollSucceeded=>f.RollOutcomeKnown && f.RollSucceeded,
                CombatEventFact.RollFailed=>f.RollOutcomeKnown && f.RollNumber>0 && !f.RollSucceeded,
                CombatEventFact.CriticalRoll=>f.CriticalRoll,
                CombatEventFact.ClashOnly=>f.IsClash,
                CombatEventFact.OneSidedOnly=>f.IsOneSided,
                _=>false
            };
        }
    }
}
