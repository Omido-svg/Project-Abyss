using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    public enum R7UniqueGaugeKind { OlafMadness = 1, YujinSense = 2 }

    /// <summary>R7 native unique resource authoring. Runtime never reads
    /// SkillEffectEntry, TempBalance proxy or upgrade profile.</summary>
    [CreateAssetMenu(menuName="Project Abyss/Abilities/Native Unique Gauge",
        fileName="NativeUniqueGauge")]
    public sealed class R7NativeUniqueGaugeDefinition : ScriptableObject
    {
        public int SchemaVersion=1;
        public string StableId;
        public string SourceAssetGuid;
        public string SourceAssetName;
        public R7UniqueGaugeKind Kind;
        public AbilityEventKind Trigger;
        public int Level0Amount=1;
        public int Level1Amount=1;
        public int Level2Amount=1;
        public bool RestrictToRoll;
        public int RollNumber=1;
        public int AmountForLevel(int level)=>level>=2?Level2Amount:
            level==1?Level1Amount:Level0Amount;
        public bool IsValid=>SchemaVersion==1 &&
            !string.IsNullOrWhiteSpace(StableId)&&
            !string.IsNullOrWhiteSpace(SourceAssetGuid)&&
            !string.IsNullOrWhiteSpace(SourceAssetName)&&
            (Kind==R7UniqueGaugeKind.OlafMadness||
             Kind==R7UniqueGaugeKind.YujinSense)&&
            Level0Amount>0&&Level1Amount>0&&Level2Amount>0&&RollNumber>=1;
    }
}
