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
    private AbilityDamageConditionOrigin damageOrigin=AbilityDamageConditionOrigin.RollUsing;
    private bool showDetails=true;
    public override void OnInspectorGUI()
    {
        var definition=(AbilityRuleSetDefinition)target;
        EditorGUILayout.LabelField("스킬 규칙 요약",EditorStyles.boldLabel);
        foreach(var rule in definition.Rules??Array.Empty<AbilityRuleData>())
            EditorGUILayout.HelpBox(AbilityRuleSummary.Describe(rule),MessageType.None);
        showDetails=EditorGUILayout.Foldout(showDetails,"상세 데이터 편집",true);
        if(showDetails)DrawDefaultInspector();
        var registry=new AbilityRuleRegistry();AbilityRuleModules.Configure(registry);
        foreach(var issue in ValidateAsset(definition,registry))EditorGUILayout.HelpBox(issue,definition.Enabled?MessageType.Error:MessageType.Warning);
        EditorGUILayout.HelpBox("Drafts: Enabled=false. Use a registered operation for a new mechanic; Plan reads immutable facts, Commit uses domain services. Zero values are not inferred from pending design.",MessageType.Info);
        EditorGUILayout.HelpBox("CardAuthored는 직접 트리거 저작 여부입니다. Skill 소유 내부 규칙에도 결투 게이트가 적용됩니다. 순수 패시브는 SourceKind=Passive로 분리하세요.",MessageType.Info);
        if(definition.SourceKind==AbilityRuleSourceKind.Skill)
        {
            damageOrigin=(AbilityDamageConditionOrigin)EditorGUILayout.EnumPopup("피해 후 판정의 원래 발동",damageOrigin);
            if(GUILayout.Button("피해 후 조건 판정 규칙 추가 (비활성 초안)"))
            {
                if(damageOrigin==AbilityDamageConditionOrigin.Unspecified)damageOrigin=AbilityDamageConditionOrigin.RollUsing;
                Undo.RecordObject(definition,"Add damage-evaluated card rule");
                definition.Rules=(definition.Rules??Array.Empty<AbilityRuleData>()).Concat(new[]{AbilityRuleAuthoringTemplates.AfterDamageEvaluation(
                    "after_damage_"+(definition.Rules?.Length??0),damageOrigin)}).ToArray();
                definition.Enabled=false;EditorUtility.SetDirty(definition);
            }
        }
        if(GUILayout.Button("Show registered authoring vocabulary"))
            EditorUtility.DisplayDialog("Ability vocabulary","Operations:\n"+string.Join("\n",registry.OperationIds.OrderBy(x=>x))+"\n\nConditions:\n"+
                string.Join("\n",registry.PredicateIds.OrderBy(x=>x))+"\n\nTargets:\n"+string.Join("\n",registry.SelectorIds.OrderBy(x=>x)),"Close");
    }

    /// <summary>Saves a new rule graph and its local next-turn settings together. Never overwrites an existing asset.</summary>
    public static void SaveNewDraft(AbilityRuleSetDefinition definition,string path)
    {
        if(definition==null)throw new ArgumentNullException(nameof(definition));
        if(!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(definition)))
            throw new ArgumentException("이미 저장된 정의입니다. 기존 자산은 Inspector에서 편집하세요.",nameof(definition));
        if(string.IsNullOrWhiteSpace(path)||!path.StartsWith("Assets/",StringComparison.Ordinal)||
            !path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase)||
            !AssetDatabase.IsValidFolder(System.IO.Path.GetDirectoryName(path).Replace('\\','/'))||
            System.IO.File.Exists(path)||AssetDatabase.LoadMainAssetAtPath(path)!=null)
            throw new ArgumentException("비어 있는 Assets 폴더의 .asset 경로를 지정하세요.",nameof(path));

        var reservations=(definition.Rules??Array.Empty<AbilityRuleData>()).Where(r=>r!=null)
            .SelectMany(r=>r.Operations??Array.Empty<AbilityOperationData>()).Where(op=>op!=null)
            .Select(op=>op.Settings).OfType<AbilityScheduleDefinition>()
            .Where(s=>s.Definition==definition&&string.IsNullOrEmpty(AssetDatabase.GetAssetPath(s))).Distinct().ToArray();
        foreach(var reservation in reservations)
        {
            var matches=(definition.Rules??Array.Empty<AbilityRuleData>()).Where(r=>r!=null&&r.RuleId==reservation.RuleId).ToArray();
            if(matches.Length!=1||!matches[0].ScheduledOnly)
                throw new ArgumentException("예약 지급 규칙을 Rules에 한 번 포함해야 합니다: "+reservation.RuleId,nameof(definition));
        }
        bool created=false;
        try
        {
            AssetDatabase.CreateAsset(definition,path);
            created=AssetDatabase.GetAssetPath(definition)==path;
            if(!created)throw new InvalidOperationException("규칙 자산을 생성하지 못했습니다: "+path);
            foreach(var reservation in reservations)
            {
                AssetDatabase.AddObjectToAsset(reservation,definition);
                EditorUtility.SetDirty(reservation);
            }
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
        }
        catch
        {
            if(created)AssetDatabase.DeleteAsset(path);
            throw;
        }
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
