#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterAuthoringBundle))]
public sealed class CharacterAuthoringBundleEditor : Editor
{
    private bool showRaw;

    public override void OnInspectorGUI()
    {
        CharacterAuthoringBundle bundle = (CharacterAuthoringBundle)target;

        EditorGUILayout.HelpBox(
            "Character Bundle은 Character Prefab Assembly의 최상위 에셋입니다. " +
            "Character Studio에서 Modern CombatLoadout, 최초 장착 스킬, " +
            "Passive/Item, Legacy Adapter와 Presentation을 편집하세요.",
            MessageType.Info);

        if (GUILayout.Button("Open Character Studio", GUILayout.Height(34f)))
            ProjectAbyssCharacterStudio.Open(bundle);

        EditorGUILayout.Space(5f);
        EditorGUILayout.ObjectField(
            "Character Prefab",
            bundle.CharacterPrefab,
            typeof(Character),
            false);
        EditorGUILayout.ObjectField(
            "Character Data",
            bundle.CharacterData,
            typeof(CharacterData),
            false);
        EditorGUILayout.ObjectField(
            "Modern Combat Loadout",
            bundle.CombatLoadout,
            typeof(CharacterCombatLoadout),
            false);
        EditorGUILayout.ObjectField(
            "Legacy Runtime Adapter",
            bundle.SkillSet,
            typeof(ScriptableObject),
            false);
        EditorGUILayout.LabelField(
            "Initial Passives",
            (bundle.EquippedPassives?.Count ?? 0).ToString());

        showRaw = EditorGUILayout.Foldout(
            showRaw,
            "Raw Bundle Inspector",
            true);

        if (showRaw)
            DrawDefaultInspector();
    }
}

[CustomEditor(typeof(CharacterAuthoringLink))]
public sealed class CharacterAuthoringLinkEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        CharacterAuthoringLink link = (CharacterAuthoringLink)target;
        EditorGUILayout.Space(5f);

        if (link.Bundle != null &&
            GUILayout.Button("Open Bundle in Character Studio"))
        {
            ProjectAbyssCharacterStudio.Open(link.Bundle);
        }

        if (GUILayout.Button("Apply Bundle Now", GUILayout.Height(28f)))
        {
            Undo.RecordObject(link, "Apply Character Authoring Bundle");
            link.ApplyNow();
            EditorUtility.SetDirty(link);
        }

        if (link.Bundle != null && GUILayout.Button("Assemble / Repair Prefab"))
        {
            CharacterPrefabAssemblyUtility.Assemble(
                link.Bundle,
                ensureStandardComponents: true,
                createStandardHierarchy: true);
        }
    }
}
#endif
