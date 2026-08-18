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

    private BattleVfxDefinition newVfxDefinition;
    private SkillShaderEffectDefinition newShaderDefinition;

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

        SkillVisualDefinition definition =
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

            DrawVisualFxSection(
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
            "Project Abyss Skill Cutscene Studio v6.3 — Timeline Visual FX",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Unity Timeline을 시간축으로 사용하고 Project Abyss 전용 Camera/Event Track을 추가합니다.\n" +
            "스킬 전용 CM 카메라를 Scene View에서 자유롭게 배치하고 Ctrl+Shift+F로 구도를 맞춘 뒤, " +
            "현재 Camera Clip에 Capture하면 됩니다. VFX/Shader FX는 Visual FX 그룹의 전용 Track에서 직접 편집합니다.",
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
        SkillVisualDefinition definition)
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "1. Skill Presentation",
            EditorStyles.boldLabel);

        EditorGUILayout.ObjectField(
            "Skill Presentation",
            definition,
            typeof(
                SkillVisualDefinition),
            false);

        if (definition == null)
        {
            EditorGUILayout.HelpBox(
                "통합 SkillVisualDefinition, 필수 2-Segment 공격자 Timeline, " +
                "Camera Rig Prefab과 기본 Track을 한 번에 생성합니다.",
                MessageType.None);
        }

        if (GUILayout.Button(
                definition == null
                    ? "Create Required Timeline Assets"
                    : "Repair Action / ClashAttack Timelines",
                GUILayout.Height(36f)))
        {
            SkillVisualDefinition created =
                SkillCutsceneAssetBuilder
                    .EnsureForSkill(
                        skill,
                        previewAttacker,
                        previewTarget);

            if (created != null)
            {
                lastMessage =
                    "Skill Presentation, Action/ClashAttack Timeline, Camera Rig과 " +
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
        SkillVisualDefinition definition)
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "2. Timeline / Preview",
            EditorStyles.boldLabel);

        if (!SkillCutsceneSegmentUtility
                .IsActiveAttackerSegment(segment))
        {
            segment = SkillCutsceneSegment.Action;
        }

        int segmentIndex =
            segment == SkillCutsceneSegment.ClashAttack
                ? 1
                : 0;

        segmentIndex =
            EditorGUILayout.Popup(
                "Segment",
                segmentIndex,
                new[]
                {
                    "Action",
                    "Clash Attack"
                });

        segment =
            segmentIndex == 1
                ? SkillCutsceneSegment.ClashAttack
                : SkillCutsceneSegment.Action;

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
        SkillVisualDefinition definition)
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

    private void DrawVisualFxSection(
        SkillVisualDefinition definition)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            "4. Visual FX — VFX + Shader",
            EditorStyles.boldLabel);

        SerializedObject definitionObject = new SerializedObject(definition);
        definitionObject.Update();
        SerializedProperty explicitFx =
            definitionObject.FindProperty("UseExplicitVisualFxTracks");

        if (explicitFx != null)
        {
            EditorGUILayout.PropertyField(
                explicitFx,
                new GUIContent("Use Explicit Visual FX Tracks"));
            definitionObject.ApplyModifiedProperties();
        }

        TimelineAsset timeline = definition.GetTimeline(segment);
        ProjectAbyssSkillCutscenePreviewBinder binder = FindPreviewBinder();
        double playhead = binder?.Director?.time ?? 0d;

        newVfxDefinition =
            (BattleVfxDefinition)EditorGUILayout.ObjectField(
                "VFX Definition",
                newVfxDefinition,
                typeof(BattleVfxDefinition),
                false);

        using (new EditorGUI.DisabledScope(timeline == null))
        {
            if (GUILayout.Button(
                    "Add VFX Clip At Playhead",
                    GUILayout.Height(30f)))
            {
                TimelineClip clip = SkillCutsceneAssetBuilder.AddVfxClip(
                    definition,
                    timeline,
                    playhead,
                    newVfxDefinition);

                lastMessage = clip != null
                    ? "Visual FX/VFX Track에 Clip을 추가했습니다. " +
                      "Clip Inspector에서 Binding, 위치, 이동, Scale, Playback Speed를 설정하세요."
                    : "VFX Clip 추가에 실패했습니다.";
                lastMessageType = clip != null
                    ? MessageType.Info
                    : MessageType.Error;
                binder?.BindNow();
            }
        }

        EditorGUILayout.Space(4f);
        newShaderDefinition =
            (SkillShaderEffectDefinition)EditorGUILayout.ObjectField(
                "Shader Effect Definition",
                newShaderDefinition,
                typeof(SkillShaderEffectDefinition),
                false);

        using (new EditorGUI.DisabledScope(timeline == null))
        {
            if (GUILayout.Button(
                    "Add Shader FX Clip At Playhead",
                    GUILayout.Height(30f)))
            {
                TimelineClip clip = SkillCutsceneAssetBuilder.AddShaderFxClip(
                    definition,
                    timeline,
                    playhead,
                    newShaderDefinition);

                lastMessage = clip != null
                    ? "Visual FX/Shader Track에 Clip을 추가했습니다. " +
                      "Clip Inspector에서 Attacker/Target, Renderer, Material Slot과 Strength Curve를 설정하세요."
                    : "Shader FX Clip 추가에 실패했습니다.";
                lastMessageType = clip != null
                    ? MessageType.Info
                    : MessageType.Error;
                binder?.BindNow();
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(
                   timeline == null || binder?.Context == null ||
                   Selection.activeGameObject == null))
        {
            if (GUILayout.Button("Capture Selected Transform → VFX Start"))
            {
                CaptureResult result = CaptureSelectedVfxTransform(
                    binder,
                    timeline,
                    captureEnd: false);
                lastMessage = result.Message;
                lastMessageType = result.Success
                    ? MessageType.Info
                    : MessageType.Error;
            }

            if (GUILayout.Button("Capture Selected Transform → VFX End"))
            {
                CaptureResult result = CaptureSelectedVfxTransform(
                    binder,
                    timeline,
                    captureEnd: true);
                lastMessage = result.Message;
                lastMessageType = result.Success
                    ? MessageType.Info
                    : MessageType.Error;
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "VFX Definition은 Effect Prefab과 Pool/Lifetime 기본값을 나타냅니다. " +
            "스킬별 발생 위치, 회전, Scale, 이동 경로, 재생 속도와 Clip 수명은 VFX Timeline Clip에서 설정합니다.\n" +
            "Shader Effect Definition은 제어할 Shader Property 계약만 나타냅니다. " +
            "어느 캐릭터/Renderer/Material Slot에 언제 적용할지는 Shader FX Clip에서 설정합니다.\n" +
            "Scene View에서 임시 VFX 또는 빈 GameObject를 원하는 위치에 놓고 Start/End Capture를 사용하면 좌표를 Clip에 저장할 수 있습니다.",
            MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    public static CaptureResult CaptureSelectedVfxTransformToCurrentClip(
        bool captureEnd)
    {
        ProjectAbyssSkillCutscenePreviewBinder binder = FindPreviewBinder();
        SkillVisualDefinition definition = binder?.Definition;
        TimelineAsset timeline = definition?.GetTimeline(binder.Segment);

        return CaptureSelectedVfxTransform(
            binder,
            timeline,
            captureEnd);
    }

    private static CaptureResult CaptureSelectedVfxTransform(
        ProjectAbyssSkillCutscenePreviewBinder binder,
        TimelineAsset timeline,
        bool captureEnd)
    {
        if (binder?.Director == null || binder.Context == null || timeline == null)
            return CaptureResult.Fail("열린 Preview Scene과 바인딩된 Timeline이 필요합니다.");

        GameObject selected = Selection.activeGameObject;

        if (selected == null)
            return CaptureResult.Fail("Scene View에서 위치 기준으로 사용할 GameObject를 선택하세요.");

        TimelineClip timelineClip = FindActiveVfxClip(
            timeline,
            binder.Director.time);

        if (timelineClip?.asset is not SkillVfxTimelineClip clip)
        {
            return CaptureResult.Fail(
                "현재 Playhead를 포함하는 VFX Clip이 없습니다. VFX Clip 범위 안으로 Playhead를 이동하세요.");
        }

        Undo.RecordObject(clip, "Capture Skill VFX Transform");

        Transform basis = binder.Context.ResolveVisualFxBinding(
            clip.Binding,
            clip.AnchorKey);

        Vector3 position;
        Vector3 euler;

        if (clip.Binding == SkillVisualFxBinding.World || basis == null)
        {
            position = selected.transform.position;
            euler = selected.transform.rotation.eulerAngles;
        }
        else
        {
            position = basis.InverseTransformPoint(selected.transform.position);
            euler = (Quaternion.Inverse(basis.rotation) *
                     selected.transform.rotation).eulerAngles;
        }

        if (captureEnd)
        {
            clip.AnimateTransform = true;
            clip.EndPosition = position;
            clip.EndEuler = euler;
            clip.EndScale = selected.transform.lossyScale;
        }
        else
        {
            clip.StartPosition = position;
            clip.StartEuler = euler;
            clip.StartScale = selected.transform.lossyScale;
        }

        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        binder.BindNow();

        return CaptureResult.Ok(
            $"{selected.name} Transform을 활성 VFX Clip의 " +
            $"{(captureEnd ? "End" : "Start")} 값으로 저장했습니다.");
    }

    private static TimelineClip FindActiveVfxClip(
        TimelineAsset timeline,
        double time)
    {
        if (timeline == null)
            return null;

        return SkillTimelineTrackUtility
            .EnumerateAllTracks(timeline)
            .OfType<SkillVfxTimelineTrack>()
            .SelectMany(track => track.GetClips())
            .Where(clip => clip.start <= time && time <= clip.end)
            .OrderBy(clip => clip.start)
            .LastOrDefault();
    }

    private void DrawEventSection(
        SkillVisualDefinition definition)
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "5. Battle Event Clips",
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
            "Hit: 계산된 피해·Damage Number와 현재 타깃 Presentation Profile의 반응을 해당 프레임에 재생\n" +
            "Vfx: SkillVisualDefinition의 VFX Cue 실행\n" +
            "CameraShake: Hit Shake 프리셋 실행\n" +
            "CameraImpactPulse: SkillVisualDefinition의 FOV/Impulse 프리셋 실행\n" +
            "TargetHitReaction: 피해 없는 추가 움찔만 필요할 때 사용(Hit에는 기본 반응 포함)\n" +
            "SetTimeScale / RestoreTimeScale: 구간 슬로모션\n" +
            "ReturnOverview: 기본 전투 카메라 복귀",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    private void DrawAnimationSection(
        SkillVisualDefinition definition)
    {
        EditorGUILayout.BeginVertical(
            "box");

        EditorGUILayout.LabelField(
            "6. Animation",
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
            "TargetReaction");

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
            "PrepareMovement");

        DrawProperty(
            serialized,
            "RestoreMovement");

        DrawProperty(
            serialized,
            "RestoreFacing");

        DrawProperty(
            serialized,
            "RestoreOverview");

        DrawProperty(
            serialized,
            "RestoreTimeScale");

        serialized
            .ApplyModifiedProperties();

        EditorGUILayout.HelpBox(
            "스킬 Timeline은 공격자의 Animation Clip만 소유합니다. " +
            "Target Reaction은 의미 키만 지정하며 실제 피격 Clip은 현재 타깃의 " +
            "CharacterPresentationProfile이 선택합니다. 합 접근/대치/승패/재정렬 모션도 " +
            "스킬 Timeline이 아니라 각 캐릭터 Presentation Profile에서 편집합니다.",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    private void DrawValidationSection(
        SkillVisualDefinition definition)
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
                "통합 Skill Presentation, Camera Rig과 Timeline Track 연결이 유효합니다.",
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
                "Ping Skill Presentation"))
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

        SkillVisualDefinition definition =
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
            SkillVisualDefinition definition,
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
        SkillVisualDefinition definition)
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

    private SkillVisualDefinition
        GetDefinition()
    {
        return SkillPresentationAccess.Get(
            skill);
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
        SkillVisualDefinition definition)
    {
        List<string> issues =
            new List<string>();

        if (definition == null)
        {
            issues.Add(
                "SkillVisualDefinition이 없습니다.");

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

        if (!definition.HasCompleteTimelineSet)
        {
            issues.Add(
                "필수 Timeline 세트 누락: " +
                string.Join(
                    ", ",
                    definition.GetMissingRequirements()));
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
            SkillVisualDefinition definition)
    {
        return definition;
    }

    private static IEnumerable<
        TimelineAsset> EnumerateTimelines(
            SkillVisualDefinition definition)
    {
        if (definition == null)
            yield break;

        yield return
            definition.ActionTimeline;

        yield return
            definition
                .ClashAttackTimeline;
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