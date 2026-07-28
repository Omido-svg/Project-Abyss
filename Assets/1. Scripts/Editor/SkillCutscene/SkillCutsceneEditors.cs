#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(
    typeof(
        SkillCutsceneDefinition))]
public sealed class SkillCutsceneDefinitionEditor :
    Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SkillCutsceneDefinition definition =
            (SkillCutsceneDefinition)
            target;

        EditorGUILayout.Space(8f);

        if (GUILayout.Button(
                "Open Skill Cutscene Studio",
                GUILayout.Height(32f)))
        {
            ProjectAbyssSkillCutsceneStudio
                .Open(
                    FindOwnerSkill(
                        definition),
                    null);
        }

        using (new EditorGUI.DisabledScope(
                   string.IsNullOrWhiteSpace(
                       definition
                           .PreviewScenePath)))
        {
            if (GUILayout.Button(
                    "Open Preview Scene"))
            {
                UnityEditor.SceneManagement
                    .EditorSceneManager
                    .OpenScene(
                        definition
                            .PreviewScenePath);

                EditorApplication
                    .ExecuteMenuItem(
                        "Window/Sequencing/Timeline");
            }
        }
    }

    private static SkillDefinition
        FindOwnerSkill(
            SkillCutsceneDefinition definition)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:SkillDefinition");

        foreach (string guid in guids)
        {
            SkillDefinition skill =
                AssetDatabase
                    .LoadAssetAtPath<
                        SkillDefinition>(
                            AssetDatabase
                                .GUIDToAssetPath(
                                    guid));

            if (skill?
                    .VisualDefinition?
                    .CutsceneDefinition ==
                definition)
            {
                return skill;
            }
        }

        return null;
    }
}

[CustomEditor(
    typeof(
        SkillCameraTimelineClip))]
public sealed class SkillCameraTimelineClipEditor :
    Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);

        if (GUILayout.Button(
                "Capture Selected CM Camera",
                GUILayout.Height(30f)))
        {
            ProjectAbyssSkillCutsceneStudio
                .CaptureResult result =
                    ProjectAbyssSkillCutsceneStudio
                        .CaptureSelectedCameraToCurrentClip();

            if (result.Success)
            {
                Debug.Log(
                    result.Message,
                    target);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Skill Camera Capture",
                    result.Message,
                    "확인");
            }
        }

        EditorGUILayout.HelpBox(
            "Timeline Playhead를 이 Clip 범위 안에 놓고, Preview Scene의 CM 카메라를 선택한 뒤 " +
            "Scene View 구도를 Ctrl+Shift+F로 적용하고 Capture하세요.",
            MessageType.Info);
    }
}

[CustomEditor(
    typeof(
        SkillCutsceneEventClip))]
public sealed class SkillCutsceneEventClipEditor :
    Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.HelpBox(
            "Event Clip이 활성 구간에 처음 진입하는 프레임에 한 번 실행됩니다. " +
            "Hit은 전투 결과를 다시 계산하지 않고 이미 계산된 DamageContext를 해당 프레임에 표시합니다.",
            MessageType.None);
    }
}
#endif
