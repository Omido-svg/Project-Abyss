#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// Promotes verified Preparation's two old status authoring values into one
/// immutable new native SO. Source .asset/.meta never touched.
/// </summary>
public static class NativePreparationStatusAssetVerification
{
    public const string SourceGuid="f9c5728c648630849a9ba0afe641d02a";
    public const string Path="Assets/2. Data/Resources/ProjectAbyss/NativePreparations/EliteCombatStance_Statuses.asset";
    private static SkillDefinition GetSource()
    {
        var source=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
            AssetDatabase.GUIDToAssetPath(SourceGuid));
        if(source==null||
           source.SkillId!=PreparationStatusAbilityBinding.SkillId||
           source.name!="전투 태세(도사림)" ||
           source.ActionType!=ActionType.Preparation||
           source.EffectEntries?.Count!=3) // Legacy Effects list retained for safe rollback
            throw new InvalidOperationException("Preparation identity/effect count changed");
        for(int i=0;i<2;i++)
        {
            var entry=source.EffectEntries[i];
            if(!(entry?.Definition is AddBodyPartStatusEffect effect)||
               effect.StatusEffectId!=(i==0?StatusEffectId.Bleeding:StatusEffectId.Burn)||
               effect.AuthoringSchema!=StatusEffectAuthoringSchema.Legacy||
               effect.ApplicationTiming!=StatusApplicationTiming.Immediate||
               entry.EffectiveTiming!=SkillEffectTiming.OnExecute||
               entry.OverrideTiming||entry.RestrictToRoll||
               (entry.Conditions?.Count??0)!=0||
               effect.Conditions.Count!=0||
               !string.IsNullOrEmpty(entry.UpgradeKey)||
               !(entry.Overrides?.OverrideStack==true)||
               entry.Overrides.Stack!=1||
               !entry.Overrides.OverrideDuration||entry.Overrides.Duration!=3||
               effect.TargetSelector!=SkillEffectTargetSelector.CurrentTarget||
               effect.RegenerationHealAmount!=0||
               entry.Overrides.OverrideInfiniteDuration||
               entry.Overrides.OverrideForceCharacterStatus||
               entry.Overrides.OverrideGiveToSelectedTarget)
                throw new InvalidOperationException(
                    "Status authoring has changed, refuses native contract "+i);
        }
        return source;
    }


    public static void Verify()
    {
        var src=GetSource();
        var graph=AssetDatabase.LoadAssetAtPath<NativePreparationStatusGraph>(Path);
        bool good=graph!=null&&graph.IsValid &&
            graph.StableId==src.SkillId&&graph.SourceAssetGuid==SourceGuid&&
            graph.SourceAssetName==src.name &&
            graph.Operations[0].Stacks==1&&graph.Operations[0].Duration==3&&
            graph.Operations[1].Stacks==1&&graph.Operations[1].Duration==3&&
            graph.Operations[0].Trigger==AbilityEventKind.RollUsing &&
            graph.Operations[1].Trigger==AbilityEventKind.RollUsing;
        if(!good)throw new InvalidOperationException(
            "R7-1 preparation status graph does not match original authoring.");
        Debug.Log("[R7-1] RESULT PASS=9 FAIL=0 native statuses 2/2");
    }
}
#endif