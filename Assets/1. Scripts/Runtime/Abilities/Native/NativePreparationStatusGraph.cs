using System;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Full native authoring for the two status operations of the elite
    /// preparation card. No runtime use of SkillEffectEntry.TryApply,
    /// AddBodyPartStatusEffect or legacy status template parameters.
    /// Data is a GUID-locked sibling of the existing native setup graph.
    /// </summary>
    [CreateAssetMenu(menuName="Project Abyss/Abilities/Native Preparation Status Graph",
        fileName="NativePreparationStatuses")]
    public sealed class NativePreparationStatusGraph : ScriptableObject
    {
        [Serializable]
        public struct StatusOperation
        {
            public StatusEffectId Status;
            [Min(1)] public int Stacks;
            [Min(1)] public int Duration;
            public AbilityEventKind Trigger;
            public SkillEffectTargetSelector Selector;
            public bool RestrictToRoll;
            [Min(1)] public int RollNumber;
            public bool IsValid =>
                (Status==StatusEffectId.Bleeding||Status==StatusEffectId.Burn) &&
                Stacks>0&&Duration>0&&Trigger==AbilityEventKind.RollUsing &&
                Selector==SkillEffectTargetSelector.CurrentTarget &&
                !RestrictToRoll&&RollNumber>=1;
        }
        public int SchemaVersion=1;
        public string StableId;
        public string SourceAssetGuid;
        public string SourceAssetName;
        public StatusOperation[] Operations=Array.Empty<StatusOperation>();
        public bool IsValid =>
            SchemaVersion==1&&
            !string.IsNullOrWhiteSpace(StableId)&&
            !string.IsNullOrWhiteSpace(SourceAssetGuid)&&
            !string.IsNullOrWhiteSpace(SourceAssetName)&&
            Operations!=null&&Operations.Length==2&&
            Operations[0].IsValid&&Operations[0].Status==StatusEffectId.Bleeding&&
            Operations[1].IsValid&&Operations[1].Status==StatusEffectId.Burn;
    }
}