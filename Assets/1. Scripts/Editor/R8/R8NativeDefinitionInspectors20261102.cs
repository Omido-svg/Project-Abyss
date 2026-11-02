#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public abstract class R8NativeDefinitionInspectorBase20261102 : Editor
{
    protected abstract string SourceGuid {get;}
    protected abstract string StableId {get;}
    protected abstract string SourceName {get;}
    protected abstract bool SchemaValid {get;}
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(
            "R8: Source identity is immutable for production assets. "+
            "Use the R8 converter for creation and validate before save. "+
            "Manual property edits require a new R7 parity gate.",
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
            R8NativeAuthoringWindow20261102.Open();
        }
    }
}

[CustomEditor(typeof(R7NativeAbilityDefinition))]
public sealed class R8ResourceNativeInspector20261102 : R8NativeDefinitionInspectorBase20261102
{
    private R7NativeAbilityDefinition D=>(R7NativeAbilityDefinition)target;
    protected override string SourceGuid=>D.SourceAssetGuid;
    protected override string StableId=>D.StableId;
    protected override string SourceName=>D.SourceAssetName;
    protected override bool SchemaValid=>D.IsValid;
}

[CustomEditor(typeof(R7NativeStatusAbilityDefinition))]
public sealed class R8StatusNativeInspector20261102 : R8NativeDefinitionInspectorBase20261102
{
    private R7NativeStatusAbilityDefinition D=>(R7NativeStatusAbilityDefinition)target;
    protected override string SourceGuid=>D.SourceAssetGuid;
    protected override string StableId=>D.StableId;
    protected override string SourceName=>D.SourceAssetName;
    protected override bool SchemaValid=>D.IsValid;
}

[CustomEditor(typeof(R7NativeMixedAbilityDefinition))]
public sealed class R8MixedNativeInspector20261102 : R8NativeDefinitionInspectorBase20261102
{
    private R7NativeMixedAbilityDefinition D=>(R7NativeMixedAbilityDefinition)target;
    protected override string SourceGuid=>D.SourceAssetGuid;
    protected override string StableId=>D.StableId;
    protected override string SourceName=>D.SourceAssetName;
    protected override bool SchemaValid=>D.IsValid;
}

[CustomEditor(typeof(R7NativeUniqueGaugeDefinition))]
public sealed class R8UniqueGaugeNativeInspector20261102 : R8NativeDefinitionInspectorBase20261102
{
    private R7NativeUniqueGaugeDefinition D=>(R7NativeUniqueGaugeDefinition)target;
    protected override string SourceGuid=>D.SourceAssetGuid;
    protected override string StableId=>D.StableId;
    protected override string SourceName=>D.SourceAssetName;
    protected override bool SchemaValid=>D.IsValid;
}

[CustomEditor(typeof(R7NativePreparationSetupDefinition))]
public sealed class R8PreparationNativeInspector20261102 : R8NativeDefinitionInspectorBase20261102
{
    private R7NativePreparationSetupDefinition D=>(R7NativePreparationSetupDefinition)target;
    protected override string SourceGuid=>D.SourceAssetGuid;
    protected override string StableId=>D.StableId;
    protected override string SourceName=>D.SourceAssetName;
    protected override bool SchemaValid=>D.IsValid;
}
#endif