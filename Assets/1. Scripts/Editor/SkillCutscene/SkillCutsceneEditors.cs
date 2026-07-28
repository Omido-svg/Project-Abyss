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
            "Hit은 전투 결과를 다시 계산하지 않고 이미 계산된 DamageContext를 표시하고, 현재 타깃의 Hit 상태를 재생합니다. " +
            "공격 Timeline의 Target Animation Track에는 특정 캐릭터 Hit Clip을 고정하지 마세요. CameraImpactPulse는 CameraImpactTiming과 " +
            "SkillCameraDefinition의 조건에 맞는 FOV/Impulse 프리셋을 실행합니다.",
            MessageType.None);
    }
}

[CustomEditor(typeof(SkillVfxTimelineClip))]
public sealed class SkillVfxTimelineClipEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Capture Selected → Start"))
            RunCapture(false);

        if (GUILayout.Button("Capture Selected → End"))
            RunCapture(true);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "VFX Definition은 Effect 자체를 지정합니다. 위치, 회전, Scale, Binding, 이동 Curve, " +
            "Playback Speed와 Lifetime은 이 Timeline Clip에서 설정하세요. " +
            "Capture는 Preview Scene의 선택된 GameObject Transform을 현재 Clip에 저장합니다.",
            MessageType.Info);
    }

    private void RunCapture(bool captureEnd)
    {
        ProjectAbyssSkillCutsceneStudio.CaptureResult result =
            ProjectAbyssSkillCutsceneStudio
                .CaptureSelectedVfxTransformToCurrentClip(captureEnd);

        if (result.Success)
        {
            Debug.Log(result.Message, target);
            return;
        }

        EditorUtility.DisplayDialog(
            "Skill VFX Capture",
            result.Message,
            "확인");
    }
}

[CustomEditor(typeof(SkillShaderTimelineClip))]
public sealed class SkillShaderTimelineClipEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.HelpBox(
            "Shader Effect Definition은 제어할 Property 이름과 목표값만 보유합니다. " +
            "Attacker/Target, Renderer 검색 방식, Anchor, Material Slot, 재생 속도와 Strength Curve는 " +
            "이 Clip에서 결정합니다. Clip 종료 또는 Timeline 중단 시 원래 MaterialPropertyBlock이 복구됩니다.",
            MessageType.Info);
    }
}

#endif
