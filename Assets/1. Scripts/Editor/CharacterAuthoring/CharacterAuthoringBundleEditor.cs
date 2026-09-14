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
            "Passive/Item과 Presentation을 편집하세요.",
            MessageType.Info);

        if (GUILayout.Button("Open Character Studio", GUILayout.Height(34f)))
            ProjectAbyssCharacterStudio.Open(bundle);

        if (GUILayout.Button(
                "Open Character Verification",
                GUILayout.Height(30f)))
        {
            CharacterVerificationWindow.Open(bundle);
        }

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
            "Character Presentation",
            bundle.PresentationProfile,
            typeof(CharacterPresentationProfile),
            false);
        EditorGUILayout.LabelField(
            "Initial Passives",
            (bundle.EquippedPassives?.Count ?? 0).ToString());

        if (bundle.Kind == CharacterAuthoringKind.EliteEnemy)
        {
            serializedObject.Update();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "P0 Elite / Boss Body Parts",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Elite/Boss는 5부위를 데이터로 정의합니다. 각 부위의 이름, 슬롯 역할, " +
                "약화 디버프와 파괴 디버프를 여기서 편집합니다. 파괴 시 슬롯 상실은 공통 규칙이며, " +
                "아래 Broken 필드는 그 위에 추가되는 적별 페널티입니다.",
                MessageType.None);

            SerializedProperty usePosture =
                serializedObject.FindProperty("useElitePostureRotation");
            SerializedProperty posture =
                serializedObject.FindProperty("elitePostureSettings");
            SerializedProperty parts =
                serializedObject.FindProperty("eliteBodyPartDefinitions");

            if (usePosture != null)
                EditorGUILayout.PropertyField(usePosture);
            if (posture != null)
                EditorGUILayout.PropertyField(posture, true);
            if (parts != null)
                EditorGUILayout.PropertyField(parts, true);

            if (serializedObject.ApplyModifiedProperties())
                EditorUtility.SetDirty(bundle);
        }

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
