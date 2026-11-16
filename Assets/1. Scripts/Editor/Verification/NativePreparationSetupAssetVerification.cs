#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>One exact native Preparation setup graph, no original SO edits.</summary>
public static class NativePreparationSetupAssetVerification
{
    private const string Guid="f9c5728c648630849a9ba0afe641d02a";
    private const string Path="Assets/2. Data/Resources/ProjectAbyss/NativePreparations/EliteCombatStance_Setup.asset";
    private static SkillDefinition Source()
    {
        var src=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
            AssetDatabase.GUIDToAssetPath(Guid));
        if(src==null || src.SkillId!=PreparationStatusAbilityBinding.SkillId ||
           src.name!="전투 태세(도사림)"||src.EffectEntries?.Count!=3 ||
           !(src.EffectEntries[2].Definition is PreparationFlatSetupEffect e)||
           src.EffectEntries[2].EffectiveTiming!=SkillEffectTiming.OnExecute ||
           src.EffectEntries[2].Conditions?.Count>0 ||
           src.EffectEntries[2].RestrictToRoll ||
           e.BlockGain!=3||e.TurnClashPowerBonus!=1||
           e.WeakenedLegPenalty!=1||e.GiveToSelectedTarget)
            throw new InvalidOperationException("Preparation source contract changed");
        return src;
    }

    public static void Verify()
    {
        if(Application.isPlaying)throw new InvalidOperationException("EditMode");
        var src=Source();
        var graph=AssetDatabase.LoadAssetAtPath<NativePreparationSetupDefinition>(Path);
        bool valid=graph!=null && graph.IsValid&&
            graph.StableId==src.SkillId &&
            graph.SourceAssetGuid==Guid&&
            graph.SourceAssetName==src.name&&
            graph.BlockGain==3&&graph.TurnClashPowerBonus==1 &&
            graph.WeakenedLegPenalty==1&&!graph.GiveToSelectedTarget;
        if(!valid)throw new InvalidOperationException("Preparation graph identity/data mismatch");
        Debug.Log("[R7-PrepSetupData] RESULT PASS=8 FAIL=0");
    }
}
#endif
