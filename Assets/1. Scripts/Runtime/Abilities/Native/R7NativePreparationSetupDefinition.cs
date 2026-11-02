using UnityEngine;
namespace ProjectAbyss.Abilities
{
    /// <summary>Third effect of verified Preparation cohort, native authored
    /// constants without runtime dependency on PreparationFlatSetupEffect.</summary>
    [CreateAssetMenu(menuName="Project Abyss/Abilities/Native Preparation Setup",
        fileName="NativePreparationSetup")]
    public sealed class R7NativePreparationSetupDefinition : ScriptableObject
    {
        public int SchemaVersion=1;
        public string StableId,SourceAssetGuid,SourceAssetName;
        public AbilityEventKind Trigger=AbilityEventKind.RollUsing;
        public int BlockGain,TurnClashPowerBonus,WeakenedLegPenalty;
        public bool GiveToSelectedTarget;
        public bool IsValid=>SchemaVersion==1 &&
            !string.IsNullOrWhiteSpace(StableId)&&
            !string.IsNullOrWhiteSpace(SourceAssetGuid)&&
            !string.IsNullOrWhiteSpace(SourceAssetName)&&
            Trigger==AbilityEventKind.RollUsing &&
            BlockGain>=0 && TurnClashPowerBonus>=0 &&
            WeakenedLegPenalty>=0;
    }
}
