using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    // R7 data-only snapshot for the seven legacy-authored bleeding cards.
    // Runtime activation deliberately remains disabled until parity is proved.
    public enum MixedOperationKind { RolledPartDamage = 1, ApplyBleeding = 2 }

    [Serializable]
    public sealed class MixedOperationEntry
    {
        public MixedOperationKind Kind;
        public AbilityEventKind Trigger;
        public int EntryIndex;
        public int StackAmount;
        
        // Fully native rolled-damage configuration (schema 2).
        public float DamageMultiplier = 1f;
        public int DamageFlatBonus;
        public DamageType DamageType = DamageType.SkillPart;
        public bool UseSkillCanBreakPart = true;
        public bool CanBreakPart;
        public bool ApplyMomentum;
        public bool ApplyGuard = true;
public int Duration;
        // R7-4 per-operation native roll/condition filters.
        // Existing schema-2 assets remain unrestricted by default.
        public bool RestrictToRoll;
        [Min(1)] public int RollNumber=1;
        public MixedAbilityConditionKind Condition;
        public StatusEffectId RequiredStatus=StatusEffectId.Strength;
        [Min(1)] public int RequiredNumericValue=1;
    }

    [CreateAssetMenu(menuName = "Project Abyss/Abilities/Native Mixed Ability",
        fileName = "NativeMixedAbility")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "ProjectAbyss.Abilities", null, "R7NativeMixedAbilityDefinition")]
    public sealed class NativeMixedAbilityDefinition : ScriptableObject
    {
        public AbilityRuleMetadata Rules = new AbilityRuleMetadata();
        public int SchemaVersion = 1;
        public string StableId;
        public string SourceAssetGuid;
        
        public bool SourceCanBreakPart;
public string SourceAssetName;
        public MixedOperationEntry[] Operations = Array.Empty<MixedOperationEntry>();

        public bool SupportsRulePolicies => Rules==null ||
            Rules.Snapshot==AbilitySnapshotPolicy.LiveAtTrigger && Rules.OwnerScope==AbilityOwnerScope.Actor;

        public bool IsValid
        {
            get
            {
                if (!SupportsRulePolicies || SchemaVersion != 2 || string.IsNullOrWhiteSpace(StableId) ||
                    string.IsNullOrWhiteSpace(SourceAssetGuid) ||
                    string.IsNullOrWhiteSpace(SourceAssetName) ||
                    Operations == null || Operations.Length == 0) return false;
                for (int i = 0; i < Operations.Length; i++)
                {
                    var e = Operations[i];
                    if (e == null || e.EntryIndex != i ||
                        (Rules!=null && Rules.Validate(e.Trigger).Count>0) ||
                        !MixedAbilityConditionGate.IsValid(e) ||
                        (e.Kind != MixedOperationKind.ApplyBleeding &&
                         e.Kind != MixedOperationKind.RolledPartDamage) ||
                        (e.Kind == MixedOperationKind.ApplyBleeding &&
                         (e.StackAmount <= 0 || e.Duration <= 0))) return false;
                }
                return true;
            }
        }
    }
}