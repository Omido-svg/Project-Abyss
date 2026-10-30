using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    // Native authored status data; old TempBalance/SkillEffectEntry is NOT
    // referenced or consulted for planning/applying status effects.
    [CreateAssetMenu(menuName="Project Abyss/Abilities/Native Status Ability",
        fileName="NativeStatusAbility")]
    public sealed class R7NativeStatusAbilityDefinition : ScriptableObject
    {
        public int SchemaVersion=1;
        public string StableId;
        public string SourceAssetGuid;
        public string SourceAssetName;
        public AbilityEventKind Trigger;
        public StatusEffectId Status=StatusEffectId.Bleeding;
        [Min(0)] public int Level0Stacks;
        [Min(0)] public int Level1Stacks;
        [Min(0)] public int Level2Stacks;
        public bool RestrictToRoll;
        [Min(1)] public int RollNumber=1;
        public int StacksForLevel(int level)=>level>=2?Level2Stacks:
            level==1?Level1Stacks:Level0Stacks;
        public bool IsValid=>SchemaVersion==1 &&
            !string.IsNullOrWhiteSpace(StableId) &&
            !string.IsNullOrWhiteSpace(SourceAssetGuid) &&
            !string.IsNullOrWhiteSpace(SourceAssetName) &&
            Status==StatusEffectId.Bleeding &&
            Level0Stacks>0&&Level1Stacks>0&&Level2Stacks>0;
    }
}