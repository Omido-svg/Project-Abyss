using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    // Canonical authored ability data. No SkillDefinition, SkillEffectEntry,
    // TempBalanceSkillProxyEffectDefinition or legacy Apply dependency.
    [CreateAssetMenu(menuName="Project Abyss/Abilities/Native Resource Ability",
        fileName="NativeResourceAbility")]
    public sealed class R7NativeAbilityDefinition : ScriptableObject
    {
        public int SchemaVersion=1;
        public string StableId;
        public string SourceAssetGuid;
        public string SourceAssetName;
        public AbilityEventKind Trigger;
        public R7NativeResourceKind Operation;
        [Min(0)] public int Level0Amount;
        [Min(0)] public int Level1Amount;
        [Min(0)] public int Level2Amount;
        public bool RestrictToRoll;
        [Min(1)] public int RollNumber=1;

        public int AmountForLevel(int level)=>level>=2?Level2Amount:
            level==1?Level1Amount:Level0Amount;

        public bool IsValid =>
            SchemaVersion==1 && !string.IsNullOrWhiteSpace(StableId) &&
            !string.IsNullOrWhiteSpace(SourceAssetGuid) &&
            !string.IsNullOrWhiteSpace(SourceAssetName) &&
            Level0Amount>0&&Level1Amount>0&&Level2Amount>0 &&
            (Operation==R7NativeResourceKind.Block ||
             Operation==R7NativeResourceKind.Prestige ||
             Operation==R7NativeResourceKind.TurnClashPower);
    }
}