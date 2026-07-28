#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// Project Abyss 스킬 연출 Authoring 진입점.
///
/// 실제 시간축·클립 편집·프레임 스냅은 Unity Timeline을 그대로 사용하고,
/// 이 창은 전용 CameraRig, 동적 Attacker/Target Binding,
/// Scene View Capture, Battle Event Clip을 간편하게 만든다.
/// </summary>
public sealed class ProjectAbyssSkillCutsceneStudio :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Project Abyss/Skill Cutscene Studio";

    private SkillDefinition skill;
    private CharacterAuthoringBundle characterBundle;
    private Character previewAttacker;
    private Character previewTarget;

    private SkillCutsceneSegment segment =
        SkillCutsceneSegment.Action;

    private string newCameraName =
        "CM_NewShot";

    private SkillCutsceneEventType newEventType =
        SkillCutsceneEventType.Hit;

    private Vector2 scroll;
    private string lastMessage;
    private MessageType lastMessageType =
        MessageType.Info;

    [MenuItem(MenuPath, false, 2020)]
    public static void Open()
    {
        Open(
            Selection.activeObject as
                SkillDefinition,
            null);
    }

    public static void Open(
        SkillDefinition selectedSkill,
        CharacterAuthoringBundle bundle)
    {
        ProjectAbyssSkillCutsceneStudio window =
            GetWindow<
                ProjectAbyssSkillCutsceneStudio>(
                    "Skill Cutscene Studio");

        window.minSize =
            new Vector2(
                660f,
                760f);

        if (selectedSkill != null)
            window.skill = selectedSkill;

        if (bundle != null)
        {
            window.characterBundle =
                bundle;

            window.previewAttacker =
                bundle.CharacterPrefab;
        }

        window.ResolveDefaults();
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        Selection.selectionChanged +=
            HandleSelectionChanged;

        ResolveDefaults();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -=
            HandleSelectionChanged;
    }

    private void HandleSelectionChanged()
    {
        if (Selection.activeObject is
            SkillDefinition selectedSkill)
        {
            skill =
                selectedSkill;

            ResolveDefaults();
            Repaint();
        }
    }

    private void ResolveDefaults()
    {
        if (previewAttacker == null &&
            characterBundle != null)
        {
            previewAttacker =
                characterBundle
                    .CharacterPrefab;
        }

        if (skill == null &&
            Selection.activeObject is
                SkillDefinition selectedSkill)
        {
            skill =
                selectedSkill;
        }
    }

    private void OnGUI()
    {
        DrawHeader();

        scroll =
            EditorGUILayout.BeginScrollView(
                scroll);

        DrawSelection();

        if (skill == null)
        {
            EditorGUILayout.HelpBox(
                "SkillDefinition을 지정하세요. Character Studio의 스킬 카드에서 " +
                "Cutscene Studio 버튼을 누르는 것이 가장 빠릅니다.",
                MessageType.Warning);

            EditorGUILayout.EndScrollView();
            return;
        }

        SkillCutsceneDefinition definition =
            GetDefinition();

        DrawCreationSection(
            definition);

        definition =
            GetDefinition();

        if (definition != null)
        {
            DrawTimelineSection(
                definition);

            DrawCameraAuthoringSection(
                definition);

            DrawEventSection(
                definition);

            DrawAnimationSection(
                definition);

            DrawValidationSection(
                definition);
        }

        if (!string.IsNullOrWhiteSpace(
                lastMessage))
        {
            EditorGUILayout.Space(6f);

            EditorGUILayout.HelpBox(
                lastMessage,
                lastMessageType);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawHeader()
    {
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField(
            "Project Abyss Skill Cutscene Studio v5.0",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Unity Timeline을 시간축으로 사용하고 Project Abyss 전용 Camera/Event Track을 추가합니다.\n" +
            "스킬 전용 CM 카메라를 Scene View에서 자유롭게 배치하고 Ctrl+Shift+F로 구도를 맞춘 뒤, " +
            "현재 Camera Clip에 Capture하면 됩니다.",
            MessageType.Info);
    }

    private void DrawSelection()
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "Authoring 대상",
            EditorStyles.boldLabel);

        CharacterAuthoringBundle nextBundle =
            (CharacterAuthoringBundle)
            EditorGUILayout.ObjectField(
                "Character Bundle",
                characterBundle,
                typeof(
                    CharacterAuthoringBundle),
                false);

        if (nextBundle !=
            characterBundle)
        {
            characterBundle =
                nextBundle;

            if (characterBundle != null)
            {
                previewAttacker =
                    characterBundle
                        .CharacterPrefab;
            }
        }

        skill =
            (SkillDefinition)
            EditorGUILayout.ObjectField(
                "Skill",
                skill,
                typeof(
                    SkillDefinition),
                false);

        previewAttacker =
            (Character)
            EditorGUILayout.ObjectField(
                "Preview Attacker Prefab",
                previewAttacker,
                typeof(Character),
                false);

        previewTarget =
            (Character)
            EditorGUILayout.ObjectField(
                "Preview Target Prefab",
                previewTarget,
                typeof(Character),
                false);

        EditorGUILayout.EndVertical();
    }

    private void DrawCreationSection(
        SkillCutsceneDefinition definition)
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "1. Cutscene Asset",
            EditorStyles.boldLabel);

        EditorGUILayout.ObjectField(
            "Skill Visual",
            skill.VisualDefinition,
            typeof(
                SkillVisualDefinition),
            false);

        EditorGUILayout.ObjectField(
            "Cutscene Definition",
            definition,
            typeof(
                SkillCutsceneDefinition),
            false);

        if (definition == null)
        {
            EditorGUILayout.HelpBox(
                "SkillVisualDefinition, SkillCutsceneDefinition, Action Timeline, " +
                "Camera Rig Prefab과 기본 Track을 한 번에 생성합니다.",
                MessageType.None);
        }

        if (GUILayout.Button(
                definition == null
                    ? "Create Cutscene Assets"
                    : "Repair / Complete Cutscene Assets",
                GUILayout.Height(36f)))
        {
            SkillCutsceneDefinition created =
                SkillCutsceneAssetBuilder
                    .EnsureForSkill(
                        skill,
                        previewAttacker,
                        previewTarget);

            if (created != null)
            {
                lastMessage =
                    "Cutscene Definition, Action Timeline, Camera Rig과 " +
                    "기본 Track 구성을 확인했습니다.";

                lastMessageType =
                    MessageType.Info;

                Selection.activeObject =
                    created;
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawTimelineSection(
        SkillCutsceneDefinition definition)
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "2. Timeline / Preview",
            EditorStyles.boldLabel);

        segment =
            (SkillCutsceneSegment)
            EditorGUILayout.EnumPopup(
                "Segment",
                segment);

        TimelineAsset timeline =
            definition.GetTimeline(
                segment);

        EditorGUILayout.ObjectField(
            "Current Timeline",
            timeline,
            typeof(TimelineAsset),
            false);

        EditorGUILayout.BeginHorizontal();

        if (timeline == null &&
            GUILayout.Button(
                "Create Segment Timeline"))
        {
            timeline =
                SkillCutsceneAssetBuilder
                    .EnsureSegment(
                        definition,
                        segment);

            lastMessage =
                $"{segment} Timeline을 생성했습니다.";

            lastMessageType =
                MessageType.Info;
        }

        using (new EditorGUI.DisabledScope(
                   timeline == null))
        {
            if (GUILayout.Button(
                    "Repair Track Structure"))
            {
                SkillCutsceneAssetBuilder
                    .EnsureTimelineStructure(
                        definition,
                        timeline,
                        addDefaultClips:
                            true);

                AssetDatabase.SaveAssets();

                lastMessage =
                    "Animation / Skill Camera / Battle Events Track을 확인했습니다.";

                lastMessageType =
                    MessageType.Info;
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(
                   previewAttacker == null ||
                   previewTarget == null ||
                   definition
                       .CameraRigPrefab ==
                   null))
        {
            if (GUILayout.Button(
                    "Create / Rebuild Preview Scene",
                    GUILayout.Height(32f)))
            {
                try
                {
                    string scenePath =
                        SkillCutsceneAssetBuilder
                            .CreatePreviewScene(
                                skill,
                                definition,
                                previewAttacker,
                                previewTarget);

                    if (!string.IsNullOrWhiteSpace(
                            scenePath))
                    {
                        lastMessage =
                            "Preview Scene을 만들고 Timeline 창을 열었습니다.\n" +
                            scenePath;

                        lastMessageType =
                            MessageType.Info;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(
                        exception);

                    lastMessage =
                        exception.Message;

                    lastMessageType =
                        MessageType.Error;
                }
            }
        }

        using (new EditorGUI.DisabledScope(
                   string.IsNullOrWhiteSpace(
                       definition
                           .PreviewScenePath)))
        {
            if (GUILayout.Button(
                    "Open Preview + Timeline",
                    GUILayout.Height(32f)))
            {
                OpenPreviewAndTimeline(
                    definition);
            }
        }

        EditorGUILayout.EndHorizontal();

        ProjectAbyssSkillCutscenePreviewBinder
            binder =
                FindPreviewBinder();

        double currentTime =
            binder?.Director?.time ??
            0d;

        int currentFrame =
            Mathf.RoundToInt(
                (float)(
                    currentTime *
                    definition.FrameRate));

        EditorGUILayout.LabelField(
            "Preview Playhead",
            $"Frame {currentFrame}  ·  {currentTime:0.###} sec");

        EditorGUILayout.HelpBox(
            "Timeline에서 Animation Clip을 원하는 프레임에 놓고 Camera Clip의 시작·끝을 드래그하세요. " +
            "카메라 Clip을 겹치면 겹친 구간이 Blend 시간과 Curve로 사용됩니다.",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    private void DrawCameraAuthoringSection(
        SkillCutsceneDefinition definition)
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "3. Camera Authoring",
            EditorStyles.boldLabel);

        EditorGUILayout.ObjectField(
            "Camera Rig Prefab",
            definition.CameraRigPrefab,
            typeof(GameObject),
            false);

        ProjectAbyssSkillCutscenePreviewBinder
            binder =
                FindPreviewBinder();

        TimelineAsset timeline =
            definition.GetTimeline(
                segment);

        double playhead =
            binder?.Director?.time ??
            0d;

        EditorGUILayout.BeginHorizontal();

        newCameraName =
            EditorGUILayout.TextField(
                "New Camera Name",
                newCameraName);

        using (new EditorGUI.DisabledScope(
                   definition.CameraRigPrefab ==
                   null))
        {
            if (GUILayout.Button(
                    "Add CM Camera",
                    GUILayout.Width(110f)))
            {
                CinemachineCamera created =
                    SkillCutsceneAssetBuilder
                        .AddCameraToRig(
                            definition,
                            newCameraName);

                lastMessage =
                    created != null
                        ? $"Camera Rig에 {created.name}을 추가했습니다. " +
                          "열린 Preview Scene은 다시 생성해야 새 카메라가 나타납니다."
                        : "Camera 추가에 실패했습니다.";

                lastMessageType =
                    created != null
                        ? MessageType.Info
                        : MessageType.Error;
            }
        }

        EditorGUILayout.EndHorizontal();

        string selectedCameraKey =
            ResolveSelectedCamera()
                ?.name ??
            "CM_NewShot";

        using (new EditorGUI.DisabledScope(
                   timeline == null))
        {
            if (GUILayout.Button(
                    $"Add Camera Clip At Playhead ({selectedCameraKey})",
                    GUILayout.Height(30f)))
            {
                TimelineClip clip =
                    SkillCutsceneAssetBuilder
                        .AddCameraClip(
                            definition,
                            timeline,
                            playhead,
                            selectedCameraKey);

                lastMessage =
                    clip != null
                        ? $"Frame {Mathf.RoundToInt((float)(playhead * definition.FrameRate))}에 " +
                          $"{selectedCameraKey} Clip을 추가했습니다."
                        : "Camera Clip 추가에 실패했습니다.";

                lastMessageType =
                    clip != null
                        ? MessageType.Info
                        : MessageType.Error;
            }

            if (GUILayout.Button(
                    $"Create Camera Motion Track ({selectedCameraKey})",
                    GUILayout.Height(28f)))
            {
                AnimationTrack track =
                    SkillCutsceneAssetBuilder
                        .EnsureCameraMotionTrack(
                            timeline,
                            selectedCameraKey);

                lastMessage =
                    track != null
                        ? $"{track.name}을 만들었습니다. Timeline Record로 " +
                          "카메라 Transform/FOV 키프레임을 기록하세요."
                        : "Camera Motion Track 생성에 실패했습니다.";

                lastMessageType =
                    track != null
                        ? MessageType.Info
                        : MessageType.Error;

                binder?.BindNow();
            }
        }

        using (new EditorGUI.DisabledScope(
                   binder == null ||
                   ResolveSelectedCamera() ==
                   null))
        {
            if (GUILayout.Button(
                    "Capture Selected CM Camera Into Active Camera Clip",
                    GUILayout.Height(36f)))
            {
                CaptureResult result =
                    CaptureSelectedCamera(
                        definition,
                        binder,
                        timeline);

                lastMessage =
                    result.Message;

                lastMessageType =
                    result.Success
                        ? MessageType.Info
                        : MessageType.Error;
            }
        }

        EditorGUILayout.HelpBox(
            "권장 흐름\n" +
            "1) Timeline Playhead를 원하는 프레임으로 이동\n" +
            "2) Camera Rig의 CM 카메라 선택\n" +
            "3) Scene View에서 원하는 구도 생성\n" +
            "4) Ctrl+Shift+F로 선택 카메라를 Scene View에 정렬\n" +
            "5) 위 Capture 버튼\n" +
            "6) Clip의 Position/Aim Binding을 Follow, Target, Midpoint, World 등으로 지정",
            MessageType.Info);

        DrawCameraKeyList(
            binder);

        EditorGUILayout.EndVertical();
    }

    private void DrawEventSection(
        SkillCutsceneDefinition definition)
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "4. Battle Event Clips",
            EditorStyles.boldLabel);

        TimelineAsset timeline =
            definition.GetTimeline(
                segment);

        ProjectAbyssSkillCutscenePreviewBinder
            binder =
                FindPreviewBinder();

        double playhead =
            binder?.Director?.time ??
            0d;

        newEventType =
            (SkillCutsceneEventType)
            EditorGUILayout.EnumPopup(
                "Event Type",
                newEventType);

        using (new EditorGUI.DisabledScope(
                   timeline == null))
        {
            if (GUILayout.Button(
                    "Add Event Clip At Playhead",
                    GUILayout.Height(30f)))
            {
                TimelineClip clip =
                    SkillCutsceneAssetBuilder
                        .AddEventClip(
                            definition,
                            timeline,
                            playhead,
                            newEventType);

                lastMessage =
                    clip != null
                        ? $"{newEventType} Event Clip을 현재 프레임에 추가했습니다."
                        : "Event Clip 추가에 실패했습니다.";

                lastMessageType =
                    clip != null
                        ? MessageType.Info
                        : MessageType.Error;
            }
        }

        EditorGUILayout.HelpBox(
            "Hit: 계산된 피해·Damage Number·피격 처리를 해당 프레임에 표시\n" +
            "Vfx: SkillVisualDefinition의 VFX Cue 실행\n" +
            "CameraShake: 기존 Hit Shake 실행\n" +
            "TargetHitReaction: 타깃 Hit 재생\n" +
            "SetTimeScale / RestoreTimeScale: 구간 슬로모션\n" +
            "ReturnOverview: 기본 전투 카메라 복귀",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    private void DrawAnimationSection(
        SkillCutsceneDefinition definition)
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "5. Animation",
            EditorStyles.boldLabel);

        SerializedObject serialized =
            new SerializedObject(
                definition);

        serialized.Update();

        DrawProperty(
            serialized,
            "AttackerAnimation");

        DrawProperty(
            serialized,
            "TargetAnimation");

        DrawProperty(
            serialized,
            "AuthoringFrameRate");

        DrawProperty(
            serialized,
            "DefaultDurationFrames");

        DrawProperty(
            serialized,
            "PrepareFacing");

        DrawProperty(
            serialized,
            "PrepareLegacyMovement");

        DrawProperty(
            serialized,
            "RestoreLegacyMovement");

        DrawProperty(
            serialized,
            "RestoreFacing");

        DrawProperty(
            serialized,
            "UseLegacyAutomaticVfx");

        DrawProperty(
            serialized,
            "ApplyMissingHitFallback");

        DrawProperty(
            serialized,
            "RestoreOverview");

        DrawProperty(
            serialized,
            "RestoreTimeScale");

        serialized
            .ApplyModifiedProperties();

        EditorGUILayout.HelpBox(
            "스킬별 Animation Clip은 [Abyss] Attacker Animation Track에 직접 넣습니다. " +
            "그래서 스킬이 바뀌면 Timeline과 Clip도 함께 바뀌며, Base Animator에 상태가 추가되어도 " +
            "이 Timeline 에셋의 직접 Clip 참조는 변경되지 않습니다.",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    private void DrawValidationSection(
        SkillCutsceneDefinition definition)
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "6. Validation",
            EditorStyles.boldLabel);

        List<string> issues =
            Validate(
                definition);

        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Cutscene Definition, Camera Rig, Timeline Track과 Skill Visual 연결이 유효합니다.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "• " +
                string.Join(
                    "\n• ",
                    issues),
                MessageType.Warning);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Ping Cutscene Definition"))
        {
            Selection.activeObject =
                definition;

            EditorGUIUtility.PingObject(
                definition);
        }

        if (GUILayout.Button(
                "Ping Camera Rig"))
        {
            Selection.activeObject =
                definition
                    .CameraRigPrefab;

            EditorGUIUtility.PingObject(
                definition
                    .CameraRigPrefab);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    public static CaptureResult
        CaptureSelectedCameraToCurrentClip()
    {
        ProjectAbyssSkillCutscenePreviewBinder
            binder =
                FindPreviewBinder();

        SkillCutsceneDefinition definition =
            binder?.Definition;

        TimelineAsset timeline =
            definition?
                .GetTimeline(
                    binder.Segment);

        return CaptureSelectedCamera(
            definition,
            binder,
            timeline);
    }

    private static CaptureResult
        CaptureSelectedCamera(
            SkillCutsceneDefinition definition,
            ProjectAbyssSkillCutscenePreviewBinder binder,
            TimelineAsset timeline)
    {
        if (definition == null ||
            binder == null ||
            binder.Context == null ||
            binder.Director == null ||
            timeline == null)
        {
            return CaptureResult.Fail(
                "열린 Preview Scene과 바인딩된 Timeline이 필요합니다.");
        }

        CinemachineCamera camera =
            ResolveSelectedCamera();

        if (camera == null)
        {
            return CaptureResult.Fail(
                "Hierarchy에서 캡처할 CinemachineCamera를 선택하세요.");
        }

        double time =
            binder.Director.time;

        TimelineClip timelineClip =
            FindActiveCameraClip(
                timeline,
                time);

        if (timelineClip == null ||
            timelineClip.asset is not
                SkillCameraTimelineClip clip)
        {
            return CaptureResult.Fail(
                "현재 Playhead를 포함하는 Skill Camera Clip이 없습니다. " +
                "먼저 Camera Clip을 추가하거나 범위를 늘리세요.");
        }

        Undo.RecordObject(
            clip,
            "Capture Skill Camera Pose");

        clip.CameraKey =
            camera.name;

        Transform basis =
            binder.Context
                .ResolvePositionBinding(
                    clip.PositionBinding,
                    clip.PositionAnchorKey);

        Transform poseTransform =
            binder.Context
                .CameraRig
                ?.GetPoseTransform(
                    camera.name) ??
            camera.transform;

        // Camera child에 Motion Track의 로컬 키가 있어도 최종 Scene View 구도를 유지하도록
        // Binding Root의 World Pose를 역산한다.
        Quaternion desiredPoseRotation =
            camera.transform.rotation *
            Quaternion.Inverse(
                camera.transform
                    .localRotation);

        Vector3 desiredPosePosition =
            camera.transform.position -
            desiredPoseRotation *
            camera.transform
                .localPosition;

        if (poseTransform ==
            camera.transform)
        {
            desiredPoseRotation =
                camera.transform.rotation;

            desiredPosePosition =
                camera.transform.position;
        }

        poseTransform.SetPositionAndRotation(
            desiredPosePosition,
            desiredPoseRotation);

        if (clip.PositionBinding ==
                SkillCameraPositionBinding
                    .World ||
            basis == null)
        {
            clip.CapturedPosition =
                desiredPosePosition;

            clip.CapturedEuler =
                desiredPoseRotation
                    .eulerAngles;
        }
        else
        {
            clip.CapturedPosition =
                basis.InverseTransformPoint(
                    desiredPosePosition);

            clip.CapturedEuler =
                (
                    Quaternion.Inverse(
                        basis.rotation) *
                    desiredPoseRotation
                )
                .eulerAngles;
        }

        EditorUtility.SetDirty(
            clip);

        EditorUtility.SetDirty(
            timeline);

        AssetDatabase.SaveAssets();

        binder.BindNow();

        int frame =
            Mathf.RoundToInt(
                (float)(
                    time *
                    definition.FrameRate));

        return CaptureResult.Ok(
            $"{camera.name} 구도를 Frame {frame}의 활성 Camera Clip에 저장했습니다.\n" +
            $"Position={clip.PositionBinding}, Aim={clip.AimBinding}");
    }

    private void OpenPreviewAndTimeline(
        SkillCutsceneDefinition definition)
    {
        if (definition == null ||
            string.IsNullOrWhiteSpace(
                definition
                    .PreviewScenePath))
        {
            return;
        }

        Scene scene =
            EditorSceneManager.OpenScene(
                definition
                    .PreviewScenePath,
                OpenSceneMode.Single);

        if (!scene.IsValid())
        {
            lastMessage =
                "Preview Scene을 열지 못했습니다.";

            lastMessageType =
                MessageType.Error;

            return;
        }

        ProjectAbyssSkillCutscenePreviewBinder
            binder =
                FindPreviewBinder();

        if (binder != null)
        {
            binder.Segment =
                segment;

            binder.BindNow();

            Selection.activeGameObject =
                binder.gameObject;
        }

        EditorApplication
            .ExecuteMenuItem(
                "Window/Sequencing/Timeline");

        lastMessage =
            "Preview Scene과 Timeline을 열었습니다.";

        lastMessageType =
            MessageType.Info;
    }

    private static TimelineClip
        FindActiveCameraClip(
            TimelineAsset timeline,
            double time)
    {
        if (timeline == null)
            return null;

        TimelineClip best =
            null;

        foreach (TrackAsset track
                 in timeline.GetOutputTracks())
        {
            if (track is not
                SkillCameraTimelineTrack)
            {
                continue;
            }

            foreach (TimelineClip clip
                     in track.GetClips())
            {
                if (clip.asset is not
                    SkillCameraTimelineClip)
                {
                    continue;
                }

                bool contains =
                    time >=
                    clip.start -
                    0.000001d &&
                    time <=
                    clip.end +
                    0.000001d;

                if (!contains)
                    continue;

                if (best == null ||
                    clip.start >
                    best.start)
                {
                    best =
                        clip;
                }
            }
        }

        return best;
    }

    private static CinemachineCamera
        ResolveSelectedCamera()
    {
        if (Selection.activeGameObject ==
            null)
        {
            return null;
        }

        return
            Selection.activeGameObject
                .GetComponent<
                    CinemachineCamera>() ??
            Selection.activeGameObject
                .GetComponentInParent<
                    CinemachineCamera>();
    }

    private static
        ProjectAbyssSkillCutscenePreviewBinder
        FindPreviewBinder()
    {
        return
            UnityEngine.Object
                .FindFirstObjectByType<
                    ProjectAbyssSkillCutscenePreviewBinder>(
                        FindObjectsInactive
                            .Include);
    }

    private static void DrawCameraKeyList(
        ProjectAbyssSkillCutscenePreviewBinder
            binder)
    {
        SkillCutsceneCameraRig rig =
            binder?.Context?.CameraRig;

        if (rig == null)
            return;

        IReadOnlyCollection<string> keys =
            rig.GetCameraKeys();

        EditorGUILayout.LabelField(
            "Preview Camera Keys",
            keys.Count == 0
                ? "(none)"
                : string.Join(
                    ", ",
                    keys));
    }

    private SkillCutsceneDefinition
        GetDefinition()
    {
        return skill?
            .VisualDefinition?
            .CutsceneDefinition;
    }

    private static void DrawProperty(
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property =
            serialized.FindProperty(
                propertyName);

        if (property != null)
        {
            EditorGUILayout
                .PropertyField(
                    property,
                    true);
        }
    }

    private static List<string> Validate(
        SkillCutsceneDefinition definition)
    {
        List<string> issues =
            new List<string>();

        if (definition == null)
        {
            issues.Add(
                "SkillCutsceneDefinition이 없습니다.");

            return issues;
        }

        if (definition.ActionTimeline ==
            null)
        {
            issues.Add(
                "Action Timeline이 없습니다.");
        }

        if (definition.CameraRigPrefab ==
            null)
        {
            issues.Add(
                "Camera Rig Prefab이 없습니다.");
        }
        else if (
            definition.CameraRigPrefab
                .GetComponentInChildren<
                    SkillCutsceneCameraRig>(
                        true) == null)
        {
            issues.Add(
                "Camera Rig Prefab에 SkillCutsceneCameraRig이 없습니다.");
        }

        SkillVisualDefinition visual =
            definition == null
                ? null
                : FindSkillVisual(
                    definition);

        if (visual != null &&
            !visual.UseTimelineCutscene)
        {
            issues.Add(
                "SkillVisualDefinition.UseTimelineCutscene이 꺼져 있습니다.");
        }

        foreach (TimelineAsset timeline
                 in EnumerateTimelines(
                     definition))
        {
            if (timeline == null)
                continue;

            bool hasCameraTrack =
                timeline
                    .GetOutputTracks()
                    .Any(
                        track =>
                            track is
                                SkillCameraTimelineTrack);

            bool hasEventTrack =
                timeline
                    .GetOutputTracks()
                    .Any(
                        track =>
                            track is
                                SkillCutsceneEventTrack);

            if (!hasCameraTrack)
            {
                issues.Add(
                    $"{timeline.name}: Skill Camera Track이 없습니다.");
            }

            if (!hasEventTrack)
            {
                issues.Add(
                    $"{timeline.name}: Battle Events Track이 없습니다.");
            }
        }

        return issues;
    }

    private static SkillVisualDefinition
        FindSkillVisual(
            SkillCutsceneDefinition definition)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:SkillVisualDefinition");

        foreach (string guid in guids)
        {
            SkillVisualDefinition visual =
                AssetDatabase
                    .LoadAssetAtPath<
                        SkillVisualDefinition>(
                            AssetDatabase
                                .GUIDToAssetPath(
                                    guid));

            if (visual?
                    .CutsceneDefinition ==
                definition)
            {
                return visual;
            }
        }

        return null;
    }

    private static IEnumerable<
        TimelineAsset> EnumerateTimelines(
            SkillCutsceneDefinition definition)
    {
        if (definition == null)
            yield break;

        yield return
            definition.ActionTimeline;

        yield return
            definition
                .ClashAttackTimeline;

        yield return
            definition
                .PartBreakTimeline;

        yield return
            definition.KillTimeline;

        yield return
            definition.ReturnTimeline;
    }

    public readonly struct CaptureResult
    {
        private CaptureResult(
            bool success,
            string message)
        {
            Success =
                success;

            Message =
                message;
        }

        public bool Success { get; }
        public string Message { get; }

        public static CaptureResult Ok(
            string message)
        {
            return new CaptureResult(
                true,
                message);
        }

        public static CaptureResult Fail(
            string message)
        {
            return new CaptureResult(
                false,
                message);
        }
    }
}
#endif
