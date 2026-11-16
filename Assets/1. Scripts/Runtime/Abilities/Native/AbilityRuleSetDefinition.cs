using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    public enum AbilityRuleSourceKind { Skill, Passive }
    [Serializable]
    public sealed class AbilityPredicateData
    {
        public string TypeId="always";
        public string Key;
        public int Value=1;
        public ConditionSubject Subject=ConditionSubject.ActualDamageTarget;
        public ConditionScope Scope=ConditionScope.Part;
        public ConditionFact Fact=ConditionFact.KeywordStack;
        public FactComparison Comparison=FactComparison.AtLeast;
        public AbilityOwnerScope StateScope=AbilityOwnerScope.Actor;
    }
    [Serializable]
    public sealed class AbilityConditionData
    {
        public CombatConditionNodeKind Kind=CombatConditionNodeKind.Predicate;
        public AbilityPredicateData Predicate=new();
        public AbilityConditionData[] Children=Array.Empty<AbilityConditionData>();
    }
    [Serializable]
    public sealed class AbilityOperationData
    {
        public string TypeId;
        public string Key;
        public int Amount;
        public int Duration=1;
        public int SecondaryAmount;
        public AbilityOwnerScope StateScope=AbilityOwnerScope.Actor;
        public ScriptableObject Settings;
    }
    [Serializable]
    public sealed class AbilityRuleLifetime
    {
        public bool OncePerTurn, OncePerSkillExecution, OncePerCounterExecution, OncePerRollUse, OncePerBattle;
        public bool ConsumeOnSchedule;
        [Min(0)] public int FullCooldownTurns;
    }
    [Serializable]
    public sealed class AbilityRuleData
    {
        public string RuleId;
        public AbilityEventKind Trigger=AbilityEventKind.RollUsing;
        public AbilityRuleMetadata Policy=new();
        public string TargetSelectorId="actual";
        public AbilityConditionData Condition=new();
        public AbilityOperationData[] Operations=Array.Empty<AbilityOperationData>();
        public AbilityRuleLifetime Lifetime=new();
        public int Phase, Priority;
        public bool RequiresDuelGate=true;
        public bool ScheduledOnly;
    }

    /// <summary>Composable rules for a skill, item or passive. No runtime state is stored in the asset.</summary>
    [CreateAssetMenu(menuName="Project Abyss/Abilities/Rule Set",fileName="AbilityRuleSet")]
    public sealed class AbilityRuleSetDefinition : ScriptableObject
    {
        public bool Enabled=true;
        public AbilityRuleSourceKind SourceKind;
        public int SchemaVersion=1;
        public string StableId, SourceAssetGuid, SourceAssetName;
        public AbilityRuleData[] Rules=Array.Empty<AbilityRuleData>();
    }
}
