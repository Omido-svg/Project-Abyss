#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterAuthoringBundle))]
public sealed class CharacterAuthoringBundleEditor : Editor
{
    private bool showRawInspector;

    public override void OnInspectorGUI()
    {
        CharacterAuthoringBundle bundle =
            (CharacterAuthoringBundle)target;

        EditorGUILayout.HelpBox(
            "Character Bundle은 직접 펼쳐 편집하기보다 Character Studio에서 사용하는 것을 권장합니다. " +
            "Studio는 CharacterData, Skill, Effect, Passive, Animator, Camera, VFX를 한 창에 표시합니다.",
            MessageType.Info);

        if (GUILayout.Button(
                "Open Character Studio",
                GUILayout.Height(34f)))
        {
            ProjectAbyssCharacterStudio.Open(bundle);
        }

        EditorGUILayout.Space(6f);

        EditorGUILayout.LabelField(
            "Role",
            GetRoleName(bundle.CharacterPrefab));

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
            "Skill Set",
            bundle.SkillSet,
            typeof(ScriptableObject),
            false);

        showRawInspector =
            EditorGUILayout.Foldout(
                showRawInspector,
                "Raw Bundle Inspector",
                true);

        if (!showRawInspector)
            return;

        EditorGUILayout.Space(4f);
        DrawDefaultInspector();
    }

    private static string GetRoleName(
        Character character)
    {
        return character switch
        {
            NormalEnemy => "Normal Enemy",
            EliteEnemy => "Elite Enemy",
            Enemy => "Custom Enemy",
            Olaf => "Playable — Olaf",
            null => "Unassigned",
            _ => "Playable / Custom"
        };
    }
}

[CustomEditor(typeof(CharacterAuthoringLink))]
public sealed class CharacterAuthoringLinkEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CharacterAuthoringLink link =
            (CharacterAuthoringLink)target;

        EditorGUILayout.Space(6f);

        if (link.Bundle != null &&
            GUILayout.Button("Open Bundle in Character Studio"))
        {
            ProjectAbyssCharacterStudio.Open(link.Bundle);
        }

        if (!GUILayout.Button(
                "Apply Bundle Now",
                GUILayout.Height(28f)))
        {
            return;
        }

        Undo.RecordObject(
            link,
            "Apply Character Authoring Bundle");

        link.ApplyNow();
        EditorUtility.SetDirty(link);
    }
}
#endif
