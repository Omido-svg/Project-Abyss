#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterAuthoringBundle))]
public sealed class CharacterAuthoringBundleEditor : Editor
{
    private readonly Dictionary<int, Editor>
        nestedEditors = new();

    private readonly Dictionary<int, bool>
        expanded = new();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "이 에셋이 캐릭터 제작의 단일 진입점입니다. " +
            "내부 Skill/Visual/Camera/Effect/VFX는 Sub-Asset으로 유지되며 " +
            "기존 런타임 타입과 참조 계약은 변경되지 않습니다.",
            MessageType.Info);

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "includedAssets",
            "supportingAssets");

        serializedObject.ApplyModifiedProperties();

        CharacterAuthoringBundle bundle =
            (CharacterAuthoringBundle)target;

        EditorGUILayout.Space(8f);
        DrawValidation(bundle);

        EditorGUILayout.Space(8f);
        DrawAssetCreationToolbar(bundle);

        EditorGUILayout.Space(8f);
        DrawAssetGroup(
            "Core / Generated Sub-Assets",
            bundle.IncludedAssets);

        DrawAssetGroup(
            "Optional Effect / VFX Sub-Assets",
            bundle.SupportingAssets);
    }

    private void DrawValidation(
        CharacterAuthoringBundle bundle)
    {
        Character prefab = bundle.CharacterPrefab;

        if (prefab == null)
        {
            EditorGUILayout.HelpBox(
                "Character Prefab이 아직 연결되지 않았습니다. " +
                "Workbench에서 Template Prefab을 지정하거나 직접 연결하세요.",
                MessageType.Warning);
            return;
        }

        if (!bundle.IsCompatibleWith(
                prefab,
                out string reason))
        {
            EditorGUILayout.HelpBox(
                reason,
                MessageType.Error);
            return;
        }

        EditorGUILayout.HelpBox(
            "Character Prefab과 Bundle 조합이 유효합니다.",
            MessageType.Info);
    }

    private void DrawAssetCreationToolbar(
        CharacterAuthoringBundle bundle)
    {
        EditorGUILayout.LabelField(
            "Add Optional Sub-Asset",
            EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Skill Effect"))
            ShowDerivedTypeMenu<SkillEffectDefinition>(bundle);

        if (GUILayout.Button("Battle VFX"))
            AddSupportingAsset(bundle, typeof(BattleVfxDefinition));

        if (GUILayout.Button("Persistent VFX"))
            AddSupportingAsset(bundle, typeof(PersistentBattleVfxDefinition));

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Status Visual"))
            AddSupportingAsset(bundle, typeof(StatusEffectVisualDefinition));

        if (GUILayout.Button("Skill Visual"))
            AddSupportingAsset(bundle, typeof(SkillVisualDefinition));

        if (GUILayout.Button("Skill Camera"))
            AddSupportingAsset(bundle, typeof(SkillCameraDefinition));

        EditorGUILayout.EndHorizontal();
    }

    private void ShowDerivedTypeMenu<T>(
        CharacterAuthoringBundle bundle)
        where T : ScriptableObject
    {
        GenericMenu menu = new();
        bool hasType = false;

        foreach (Type type in
                 TypeCache.GetTypesDerivedFrom<T>())
        {
            if (type == null ||
                type.IsAbstract ||
                type.IsGenericType ||
                !typeof(ScriptableObject).IsAssignableFrom(type))
            {
                continue;
            }

            hasType = true;
            string menuName =
                string.IsNullOrWhiteSpace(type.FullName)
                    ? type.Name
                    : type.FullName.Replace('.', '/');

            Type captured = type;

            menu.AddItem(
                new GUIContent(menuName),
                false,
                () => AddSupportingAsset(
                    bundle,
                    captured));
        }

        if (!hasType)
        {
            menu.AddDisabledItem(
                new GUIContent("No concrete types"));
        }

        menu.ShowAsContext();
    }

    private void AddSupportingAsset(
        CharacterAuthoringBundle bundle,
        Type type)
    {
        if (bundle == null ||
            type == null ||
            type.IsAbstract ||
            !typeof(ScriptableObject).IsAssignableFrom(type))
        {
            return;
        }

        ScriptableObject asset =
            ScriptableObject.CreateInstance(type);

        asset.name =
            ObjectNames.NicifyVariableName(type.Name);

        AssetDatabase.AddObjectToAsset(
            asset,
            bundle);

        bundle.RegisterSupportingAsset(asset);

        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(bundle);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            AssetDatabase.GetAssetPath(bundle));

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private void DrawAssetGroup(
        string title,
        IReadOnlyList<UnityEngine.Object> assets)
    {
        EditorGUILayout.LabelField(
            title,
            EditorStyles.boldLabel);

        if (assets == null || assets.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "등록된 Sub-Asset이 없습니다.",
                MessageType.None);
            return;
        }

        HashSet<int> drawn = new();

        for (int i = 0; i < assets.Count; i++)
        {
            UnityEngine.Object asset = assets[i];

            if (asset == null || asset == target)
                continue;

            int id = asset.GetInstanceID();

            if (!drawn.Add(id))
                continue;

            DrawNestedAsset(asset, id);
        }
    }

    private void DrawNestedAsset(
        UnityEngine.Object asset,
        int id)
    {
        bool isExpanded =
            expanded.TryGetValue(id, out bool value) &&
            value;

        isExpanded =
            EditorGUILayout.InspectorTitlebar(
                isExpanded,
                asset);

        expanded[id] = isExpanded;

        if (!isExpanded)
            return;

        if (!nestedEditors.TryGetValue(
                id,
                out Editor nestedEditor) ||
            nestedEditor == null)
        {
            nestedEditor = CreateEditor(asset);
            nestedEditors[id] = nestedEditor;
        }

        EditorGUI.indentLevel++;
        nestedEditor.OnInspectorGUI();
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4f);
    }

    private void OnDisable()
    {
        foreach (Editor editor in nestedEditors.Values)
        {
            if (editor != null)
                DestroyImmediate(editor);
        }

        nestedEditors.Clear();
        expanded.Clear();
    }
}

[CustomEditor(typeof(CharacterAuthoringLink))]
public sealed class CharacterAuthoringLinkEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(6f);

        if (!GUILayout.Button(
                "Apply Bundle Now",
                GUILayout.Height(28f)))
        {
            return;
        }

        CharacterAuthoringLink link =
            (CharacterAuthoringLink)target;

        Undo.RecordObject(
            link,
            "Apply Character Authoring Bundle");

        link.ApplyNow();
        EditorUtility.SetDirty(link);
    }
}
#endif
