using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum SkillCameraPositionBinding
{
    RigRootFixed = 0,
    CombatFrameFollow = 1,
    AttackerRoot = 2,
    AttackerVisualRoot = 3,
    TargetRoot = 4,
    TargetVisualRoot = 5,
    AttackerAnchor = 6,
    TargetAnchor = 7,
    AttackerTargetMidpoint = 8,
    World = 9
}

public enum SkillCameraAimBinding
{
    KeepCapturedRotation = 0,
    AttackerRoot = 1,
    AttackerVisualRoot = 2,
    TargetRoot = 3,
    TargetVisualRoot = 4,
    AttackerAnchor = 5,
    TargetAnchor = 6,
    AttackerTargetMidpoint = 7,
    CombatFrame = 8
}

/// <summary>
/// Timeline의 한 Camera Clip.
///
/// CameraKey는 SkillCutsceneCameraRig 안의 CinemachineCamera 이름입니다.
/// Timeline Clip의 위치와 길이가 프레임 구간이며,
/// Clip overlap/ease가 있으면 해당 길이와 Curve를 실제 Camera Blend에 사용합니다.
/// </summary>
[Serializable]
public sealed class SkillCameraTimelineClip :
    PlayableAsset,
    ITimelineClipAsset
{
    [Header("Camera")]
    public string CameraKey =
        "CM_Overview";

    [Header("Position Follow")]
    public SkillCameraPositionBinding PositionBinding =
        SkillCameraPositionBinding
            .RigRootFixed;

    public string PositionAnchorKey =
        "Center";

    [Tooltip(
        "Position Binding 좌표계에서 캡처한 Camera 위치입니다. " +
        "Scene View 구도를 만든 뒤 Cutscene Studio의 Capture를 사용하세요.")]
    public Vector3 CapturedPosition;

    [Min(0f)]
    public float PositionDamping;

    [Header("Aim / Rotation")]
    public SkillCameraAimBinding AimBinding =
        SkillCameraAimBinding
            .AttackerTargetMidpoint;

    public string AimAnchorKey =
        "Chest";

    [Tooltip(
        "Aim Binding 위치에 더해지는 로컬 Offset입니다.")]
    public Vector3 AimOffset =
        new Vector3(
            0f,
            1.2f,
            0f);

    [Tooltip(
        "Keep Captured Rotation일 때는 전체 로컬 회전, " +
        "LookAt 모드에서는 Roll/추가 회전으로 사용됩니다.")]
    public Vector3 CapturedEuler;

    [Min(0f)]
    public float RotationDamping;

    [Header("Transition")]
    [Tooltip(
        "Timeline Clip overlap 또는 ease-in이 있으면 그 길이와 Curve를 우선 사용합니다.")]
    public bool UseTimelineMixAsBlend =
        true;

    public CinemachineBlendDefinition.Styles
        FallbackBlendStyle =
            CinemachineBlendDefinition
                .Styles
                .EaseInOut;

    [Min(0)]
    public int FallbackBlendFrames =
        6;

    public AnimationCurve FallbackCustomCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f);

    [Header("Activation")]
    public bool SnapPoseOnEnter =
        true;

    public ClipCaps clipCaps =>
        ClipCaps.Blending |
        ClipCaps.ClipIn |
        ClipCaps.SpeedMultiplier;

    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        ScriptPlayable<
            SkillCameraTimelineBehaviour>
            playable =
                ScriptPlayable<
                    SkillCameraTimelineBehaviour>
                    .Create(graph);

        playable.GetBehaviour().Asset =
            this;

        return playable;
    }
}

