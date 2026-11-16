#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>Permanent authoring surface. New drafts never become active combat routes until validated and enabled.</summary>
[CustomEditor(typeof(AbilityRuleSetDefinition))]
public sealed class AbilityRuleSetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var registry=new AbilityRuleRegistry();AbilityRuleModules.Configure(registry);
        var definition=(AbilityRuleSetDefinition)target;
        foreach(var issue in ValidateAsset(definition,registry))EditorGUILayout.HelpBox(issue,definition.Enabled?MessageType.Error:MessageType.Warning);
        EditorGUILayout.HelpBox("Drafts: Enabled=false. Use a registered operation for a new mechanic; Plan reads immutable facts, Commit uses domain services. Zero values are not inferred from pending design.",MessageType.Info);
        if(GUILayout.Button("Show registered authoring vocabulary"))
            EditorUtility.DisplayDialog("Ability vocabulary","Operations:\n"+string.Join("\n",registry.OperationIds.OrderBy(x=>x))+"\n\nConditions:\n"+
                string.Join("\n",registry.PredicateIds.OrderBy(x=>x))+"\n\nTargets:\n"+string.Join("\n",registry.SelectorIds.OrderBy(x=>x)),"Close");
    }
    public static string[] ValidateAsset(AbilityRuleSetDefinition definition,AbilityRuleRegistry registry)
    {
        var issues=registry.Validate(definition).ToList();
        var path=AssetDatabase.GUIDToAssetPath(definition.SourceAssetGuid);
        var source=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        if(source==null||source.name!=definition.SourceAssetName)issues.Add("SOURCE_GUID_OR_NAME_MISMATCH");
        if(definition.SourceKind==AbilityRuleSourceKind.Skill && (source is not SkillDefinition skill||skill.SkillId!=definition.StableId))
            issues.Add("SOURCE_SKILL_ID_MISMATCH");
        foreach(var guid in AssetDatabase.FindAssets("t:AbilityRuleSetDefinition"))
        {
            var other=AssetDatabase.LoadAssetAtPath<AbilityRuleSetDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if(other!=definition&&other.Enabled&&definition.Enabled&&(other.StableId==definition.StableId||other.SourceAssetGuid==definition.SourceAssetGuid))
                issues.Add("DUPLICATE_ACTIVE_SOURCE: "+AssetDatabase.GetAssetPath(other));
        }
        return issues.Distinct().ToArray();
    }
    [MenuItem("Assets/Project Abyss/Create Ability Rule Draft",true)]
    private static bool CanCreate()=>Selection.activeObject is SkillDefinition;
    [MenuItem("Assets/Project Abyss/Create Ability Rule Draft")]
    private static void CreateDraft()
    {
        var skill=(SkillDefinition)Selection.activeObject;
        string path=EditorUtility.SaveFilePanelInProject("Create Ability Rule Draft",skill.name+"_Rules","asset",
            "Save under a Resources/ProjectAbyss/AbilityRules folder to load automatically.","Assets/Resources");
        if(string.IsNullOrEmpty(path))return;
        var definition=ScriptableObject.CreateInstance<AbilityRuleSetDefinition>();
        definition.Enabled=false;definition.SourceKind=AbilityRuleSourceKind.Skill;definition.StableId=skill.SkillId;
        definition.SourceAssetGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skill));definition.SourceAssetName=skill.name;
        definition.Rules=new[]{new AbilityRuleData{RuleId="rule",Trigger=AbilityEventKind.RollUsing,TargetSelectorId="actual",
            Policy=new AbilityRuleMetadata{RequiresConfiguredValue=true,PendingDesignReason="Configure the intended operations and amounts."}}};
        AssetDatabase.CreateAsset(definition,path);AssetDatabase.SaveAssets();Selection.activeObject=definition;
    }
}
#endif
