#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// Promotes verified Preparation's two old status authoring values into one
/// immutable new native SO. Source .asset/.meta never touched.
/// </summary>
public static class R7NativePreparationStatusAssetMigration20261102
{
    public const string SourceGuid="f9c5728c648630849a9ba0afe641d02a";
    public const string Path="Assets/2. Data/Resources/ProjectAbyss/NativePreparations/EliteCombatStance_Statuses.asset";
    private static SkillDefinition GetSource()
    {
        var source=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
            AssetDatabase.GUIDToAssetPath(SourceGuid));
        if(source==null||
           source.SkillId!=R7ProductionPreparationStatusMigration.SkillId||
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
    public static void Stage()
    {
        if(Application.isPlaying)throw new InvalidOperationException("EditMode required");
        var src=GetSource();
        if(AssetDatabase.LoadMainAssetAtPath(Path)!=null)
        {
            Verify();
            return;
        }
        if(System.IO.File.Exists(Path))
            throw new InvalidOperationException("Existing unmanaged asset blocked.");
        var graph=ScriptableObject.CreateInstance<R7NativePreparationStatusGraph20261102>();
        graph.SchemaVersion=1;
        graph.StableId=src.SkillId;
        graph.SourceAssetGuid=SourceGuid;
        graph.SourceAssetName=src.name;
        graph.Operations=new[]{
            new R7NativePreparationStatusGraph20261102.StatusOperation{
                Status=StatusEffectId.Bleeding,Stacks=1,Duration=3,
                Trigger=AbilityEventKind.RollUsing,
                Selector=SkillEffectTargetSelector.CurrentTarget,
                RollNumber=1},
            new R7NativePreparationStatusGraph20261102.StatusOperation{
                Status=StatusEffectId.Burn,Stacks=1,Duration=3,
                Trigger=AbilityEventKind.RollUsing,
                Selector=SkillEffectTargetSelector.CurrentTarget,
                RollNumber=1}
        };
        AssetDatabase.CreateAsset(graph,Path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Verify();
        Debug.Log("[R7-1] CREATED native status operation SO; old source preserved");
    }

    public static void Verify()
    {
        var src=GetSource();
        var graph=AssetDatabase.LoadAssetAtPath<R7NativePreparationStatusGraph20261102>(Path);
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