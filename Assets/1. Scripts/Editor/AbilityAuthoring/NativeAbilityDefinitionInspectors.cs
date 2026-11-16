#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public abstract class NativeAbilityDefinitionInspectorBase : Editor
{
    protected abstract string SourceGuid {get;}
    protected abstract string StableId {get;}
    protected abstract string SourceName {get;}
    protected abstract bool SchemaValid {get;}
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(
            "Source identity is immutable for production assets. "+
            "Use the ability authoring converter and validate before save. "+
            "Snapshot, cause and scope policies must match the trigger boundary.",
            MessageType.Info);
        EditorGUI.BeginChangeCheck();
        var property=serializedObject.GetIterator();
        bool expanded=true;
        while(property.NextVisible(expanded))
        {
            expanded=false;
            bool identity=property.name=="m_Script" ||
                property.name=="StableId" ||
                property.name=="SourceAssetGuid" ||
                property.name=="SourceAssetName" ||
                property.name=="SchemaVersion";
            using(new EditorGUI.DisabledScope(identity))
                EditorGUILayout.PropertyField(property,true);
        }
        if(EditorGUI.EndChangeCheck())serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        var path=AssetDatabase.GUIDToAssetPath(SourceGuid??"");
        var source=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        bool same=source!=null &&
            source.SkillId==StableId && source.name==SourceName &&
            !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(source));
        EditorGUILayout.LabelField("Source/Schema Gate",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Schema Valid",SchemaValid?"Yes":"NO");
        EditorGUILayout.LabelField("GUID/ID/Name",same?"Exact match":"MISMATCH");
        EditorGUILayout.ObjectField("Source Skill",source,typeof(SkillDefinition),false);
        if(!same||!SchemaValid)
            EditorGUILayout.HelpBox("Native authoring is not valid; do not route this graph.",
                MessageType.Error);
        else if(GUILayout.Button("Inspect source / compare R8 conversion"))
        {
            Selection.activeObject=source;
            NativeAbilityAuthoringWindow.Open();
        }
    }
}

[CustomEditor(typeof(NativeAbilityDefinition))]
public sealed class NativeResourceAbilityInspector : NativeAbilityDefinitionInspectorBase
{
    private NativeAbilityDefinition D=>(NativeAbilityDefinition)target;
    protected override string SourceGuid=>D.SourceAssetGuid;
    protected override string StableId=>D.StableId;
    protected override string SourceName=>D.SourceAssetName;
    protected override bool SchemaValid=>D.IsValid;
}

[CustomEditor(typeof(NativeStatusAbilityDefinition))]
public sealed class NativeStatusAbilityInspector : NativeAbilityDefinitionInspectorBase
{
    private NativeStatusAbilityDefinition D=>(NativeStatusAbilityDefinition)target;
    protected override string SourceGuid=>D.SourceAssetGuid;
    protected override string StableId=>D.StableId;
    protected override string SourceName=>D.SourceAssetName;
    protected override bool SchemaValid=>D.IsValid;
}

[CustomEditor(typeof(NativeMixedAbilityDefinition))]
public sealed class NativeMixedAbilityInspector : NativeAbilityDefinitionInspectorBase
{
    private NativeMixedAbilityDefinition D=>(NativeMixedAbilityDefinition)target;
    protected override string SourceGuid=>D.SourceAssetGuid;
    protected override string StableId=>D.StableId;
    protected override string SourceName=>D.SourceAssetName;
    protected override bool SchemaValid=>D.IsValid;
}

[CustomEditor(typeof(NativeUniqueGaugeDefinition))]
public sealed class NativeGaugeAbilityInspector : NativeAbilityDefinitionInspectorBase
{
    private NativeUniqueGaugeDefinition D=>(NativeUniqueGaugeDefinition)target;
    protected override string SourceGuid=>D.SourceAssetGuid;
    protected override string StableId=>D.StableId;
    protected override string SourceName=>D.SourceAssetName;
    protected override bool SchemaValid=>D.IsValid;
}

[CustomEditor(typeof(NativePreparationSetupDefinition))]
public sealed class NativePreparationAbilityInspector : NativeAbilityDefinitionInspectorBase
{
    private NativePreparationSetupDefinition D=>(NativePreparationSetupDefinition)target;
    protected override string SourceGuid=>D.SourceAssetGuid;
    protected override string StableId=>D.StableId;
    protected override string SourceName=>D.SourceAssetName;
    protected override bool SchemaValid=>D.IsValid;
}
#endif