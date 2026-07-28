using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Timeline Asset 안의 의미 기반 바인딩을 실제 BattleAction의
/// Attacker / Target / VisualRoot / Anchor로 변환한다.
///
/// Camera Clip은 Character Prefab의 사전 배치 CameraPoint에 제한되지 않는다.
/// Scene View에서 캡처한 자유로운 Offset을 CombatFrame, Character, World 등
/// 원하는 좌표계에 저장하고 해당 프레임 구간 동안 따라가게 한다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class SkillCutsceneRuntimeContext :
    MonoBehaviour
{
    private readonly Dictionary<
        int,
        SkillCutsceneBlendInfo> blendByClip =
            new Dictionary<
                int,
                SkillCutsceneBlendInfo>();

    private SkillCutsceneCameraRig cameraRig;
    private Character attacker;
    private Character target;
    private BodyPart targetPart;

    private Transform combatFrame;
    private Transform midpoint;

    private float authoringFrameRate =
        30f;

    private float originalTimeScale =
        1f;

    private bool capturedTimeScale;
    private bool restoreOverview;
    private bool restoreTimeScale;

    private SkillCameraTimelineClip
        activeCameraClip;

    private Action<
        SkillCutsceneEventClip>
        eventCallback;

    public Character Attacker =>
        attacker;

    public Character Target =>
        target;

    public BodyPart TargetPart =>
        targetPart;

    public SkillCutsceneCameraRig CameraRig =>
        cameraRig;

    public Animator AttackerAnimator =>
        attacker != null
            ? attacker.GetComponentInChildren<
                Animator>(true)
            : null;

    public Animator TargetAnimator =>
        target != null
            ? target.GetComponentInChildren<
                Animator>(true)
            : null;

    public float AuthoringFrameRate =>
        Mathf.Max(
            1f,
            authoringFrameRate);

    public void Configure(
        SkillCutsceneCameraRig rig,
        Character newAttacker,
        Character newTarget,
        BodyPart newTargetPart,
        double frameRate,
        bool shouldRestoreOverview,
        bool shouldRestoreTimeScale,
        Action<
            SkillCutsceneEventClip>
            onEvent)
    {
        cameraRig =
            rig;

        attacker =
            newAttacker;

        target =
            newTarget;

        targetPart =
            newTargetPart;

        authoringFrameRate =
            Mathf.Max(
                1f,
                (float)frameRate);

        restoreOverview =
            shouldRestoreOverview;

        restoreTimeScale =
            shouldRestoreTimeScale;

        eventCallback =
            onEvent;

        if (!capturedTimeScale)
        {
            originalTimeScale =
                Time.timeScale;

            capturedTimeScale =
                true;
        }

        EnsureRuntimeFrames();
        UpdateDynamicFrames();

        if (cameraRig != null &&
            combatFrame != null)
        {
            cameraRig.transform
                .SetPositionAndRotation(
                    combatFrame.position,
                    combatFrame.rotation);

            cameraRig.Rebuild();
        }
    }

    public void PrepareTimelineBlendMap(
        TimelineAsset timeline)
    {
        blendByClip.Clear();

        if (timeline == null)
            return;

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
                    SkillCameraTimelineClip asset)
                {
                    continue;
                }

                double duration =
                    clip.mixInDuration;

                AnimationCurve curve =
                    clip.mixInCurve;

                blendByClip[
                    asset.GetInstanceID()] =
                        new SkillCutsceneBlendInfo(
                            Mathf.Max(
                                0f,
                                (float)duration),
                            curve);
            }
        }
    }

    public Transform ResolvePositionBinding(
        SkillCameraPositionBinding binding,
        string anchorKey)
    {
        UpdateDynamicFrames();

        return binding switch
        {
            SkillCameraPositionBinding
                .RigRootFixed =>
                    cameraRig != null
                        ? cameraRig.RigRoot
                        : transform,

            SkillCameraPositionBinding
                .CombatFrameFollow =>
                    combatFrame,

            SkillCameraPositionBinding
                .AttackerRoot =>
                    attacker?.transform,

            SkillCameraPositionBinding
                .AttackerVisualRoot =>
                    GetVisualRoot(attacker),

            SkillCameraPositionBinding
                .TargetRoot =>
                    target?.transform,

            SkillCameraPositionBinding
                .TargetVisualRoot =>
                    GetVisualRoot(target),

            SkillCameraPositionBinding
                .AttackerAnchor =>
                    ResolveAnchor(
                        attacker,
                        anchorKey),

            SkillCameraPositionBinding
                .TargetAnchor =>
                    ResolveAnchor(
                        target,
                        anchorKey),

            SkillCameraPositionBinding
                .AttackerTargetMidpoint =>
                    midpoint,

            SkillCameraPositionBinding
                .World =>
                    null,

            _ =>
                combatFrame
        };
    }

    public Transform ResolveAimBinding(
        SkillCameraAimBinding binding,
        string anchorKey)
    {
        UpdateDynamicFrames();

        return binding switch
        {
            SkillCameraAimBinding
                .AttackerRoot =>
                    attacker?.transform,

            SkillCameraAimBinding
                .AttackerVisualRoot =>
                    GetVisualRoot(attacker),

            SkillCameraAimBinding
                .TargetRoot =>
                    target?.transform,

            SkillCameraAimBinding
                .TargetVisualRoot =>
                    GetVisualRoot(target),

            SkillCameraAimBinding
                .AttackerAnchor =>
                    ResolveAnchor(
                        attacker,
                        anchorKey),

            SkillCameraAimBinding
                .TargetAnchor =>
                    ResolveAnchor(
                        target,
                        anchorKey),

            SkillCameraAimBinding
                .AttackerTargetMidpoint =>
                    midpoint,

            SkillCameraAimBinding
                .CombatFrame =>
                    combatFrame,

            _ =>
                null
        };
    }

    public void UpdateCameraPose(
        SkillCameraTimelineClip clip,
        float deltaTime)
    {
        if (clip == null ||
            cameraRig == null)
        {
            return;
        }

        CinemachineCamera camera =
            cameraRig.GetCamera(
                clip.CameraKey);

        Transform poseTransform =
            cameraRig.GetPoseTransform(
                clip.CameraKey);

        if (camera == null ||
            poseTransform == null)
        {
            return;
        }

        UpdateDynamicFrames();

        Transform positionBasis =
            ResolvePositionBinding(
                clip.PositionBinding,
                clip.PositionAnchorKey);

        Vector3 desiredPosition =
            clip.PositionBinding ==
                SkillCameraPositionBinding
                    .World ||
            positionBasis == null
                ? clip.CapturedPosition
                : positionBasis
                    .TransformPoint(
                        clip.CapturedPosition);

        Quaternion desiredRotation =
            ResolveDesiredRotation(
                clip,
                desiredPosition,
                positionBasis);

        bool snap =
            !Application.isPlaying ||
            (
                clip.SnapPoseOnEnter &&
                activeCameraClip != clip
            );

        float safeDelta =
            Mathf.Max(
                0f,
                deltaTime);

        if (snap ||
            clip.PositionDamping <=
            0.0001f)
        {
            poseTransform.position =
                desiredPosition;
        }
        else
        {
            float positionT =
                ExponentialDampingFactor(
                    clip.PositionDamping,
                    safeDelta);

            poseTransform.position =
                Vector3.Lerp(
                    poseTransform.position,
                    desiredPosition,
                    positionT);
        }

        if (snap ||
            clip.RotationDamping <=
            0.0001f)
        {
            poseTransform.rotation =
                desiredRotation;
        }
        else
        {
            float rotationT =
                ExponentialDampingFactor(
                    clip.RotationDamping,
                    safeDelta);

            poseTransform.rotation =
                Quaternion.Slerp(
                    poseTransform.rotation,
                    desiredRotation,
                    rotationT);
        }
    }

    public void ActivateCameraClip(
        SkillCameraTimelineClip clip)
    {
        if (clip == null ||
            cameraRig == null)
        {
            return;
        }

        SkillCutsceneBlendInfo blend =
            ResolveBlend(
                clip);

        cameraRig.Activate(
            clip.CameraKey,
            blend.Style,
            blend.Duration,
            blend.Curve);

        activeCameraClip =
            clip;
    }

    public void EmitEvent(
        SkillCutsceneEventClip clip)
    {
        if (clip == null)
            return;

        switch (clip.EventType)
        {
            case SkillCutsceneEventType
                .SetTimeScale:
                Time.timeScale =
                    Mathf.Max(
                        0.01f,
                        clip.TimeScale);
                break;

            case SkillCutsceneEventType
                .RestoreTimeScale:
                RestoreCapturedTimeScale();
                break;

            case SkillCutsceneEventType
                .ReturnOverview:
                cameraRig?
                    .RestoreOverview();
                break;
        }

        if (Application.isPlaying)
        {
            eventCallback?.Invoke(
                clip);
        }
    }

    public void NotifyCameraTrackStopped()
    {
        activeCameraClip =
            null;
    }

    public void Restore()
    {
        activeCameraClip =
            null;

        if (restoreOverview)
        {
            cameraRig?
                .RestoreOverview();
        }
        else
        {
            cameraRig?
                .RestoreOriginalBlend();
        }

        if (restoreTimeScale)
        {
            RestoreCapturedTimeScale();
        }

        eventCallback =
            null;

        blendByClip.Clear();
    }

    private Quaternion ResolveDesiredRotation(
        SkillCameraTimelineClip clip,
        Vector3 desiredPosition,
        Transform positionBasis)
    {
        Quaternion capturedRotation =
            positionBasis != null
                ? positionBasis.rotation *
                  Quaternion.Euler(
                      clip.CapturedEuler)
                : Quaternion.Euler(
                    clip.CapturedEuler);

        if (clip.AimBinding ==
            SkillCameraAimBinding
                .KeepCapturedRotation)
        {
            return capturedRotation;
        }

        Transform aim =
            ResolveAimBinding(
                clip.AimBinding,
                clip.AimAnchorKey);

        if (aim == null)
            return capturedRotation;

        Vector3 aimPosition =
            aim.TransformPoint(
                clip.AimOffset);

        Vector3 direction =
            aimPosition -
            desiredPosition;

        if (direction.sqrMagnitude <=
            0.000001f)
        {
            return capturedRotation;
        }

        Quaternion lookRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

        Quaternion roll =
            Quaternion.Euler(
                0f,
                0f,
                clip.CapturedEuler.z);

        return
            lookRotation *
            roll;
    }

    private SkillCutsceneBlendInfo ResolveBlend(
        SkillCameraTimelineClip clip)
    {
        float fallbackDuration =
            Mathf.Max(
                0,
                clip.FallbackBlendFrames) /
            AuthoringFrameRate;

        CinemachineBlendDefinition.Styles
            style =
                clip.FallbackBlendStyle;

        AnimationCurve curve =
            style ==
                CinemachineBlendDefinition
                    .Styles
                    .Custom
                ? clip.FallbackCustomCurve
                : null;

        if (clip.UseTimelineMixAsBlend &&
            blendByClip.TryGetValue(
                clip.GetInstanceID(),
                out SkillCutsceneBlendInfo
                    timelineBlend) &&
            timelineBlend.Duration > 0f)
        {
            return
                new SkillCutsceneBlendInfo(
                    timelineBlend.Duration,
                    timelineBlend.Curve,
                    CinemachineBlendDefinition
                        .Styles
                        .Custom);
        }

        return
            new SkillCutsceneBlendInfo(
                fallbackDuration,
                curve,
                style);
    }

    private void UpdateDynamicFrames()
    {
        EnsureRuntimeFrames();

        Transform attackerTransform =
            GetVisualRoot(attacker) ??
            attacker?.transform;

        Transform targetTransform =
            ResolveTargetTransform();

        if (attackerTransform == null &&
            targetTransform == null)
        {
            return;
        }

        Vector3 attackerPosition =
            attackerTransform != null
                ? attackerTransform.position
                : targetTransform.position -
                  Vector3.forward * 2f;

        Vector3 targetPosition =
            targetTransform != null
                ? targetTransform.position
                : attackerPosition +
                  Vector3.forward * 2f;

        Vector3 forward =
            targetPosition -
            attackerPosition;

        forward.y =
            0f;

        if (forward.sqrMagnitude <=
            0.000001f)
        {
            forward =
                attackerTransform != null
                    ? attackerTransform.forward
                    : Vector3.forward;
        }

        forward.Normalize();

        Vector3 center =
            (
                attackerPosition +
                targetPosition
            ) *
            0.5f;

        combatFrame
            .SetPositionAndRotation(
                center,
                Quaternion.LookRotation(
                    forward,
                    Vector3.up));

        midpoint.position =
            center;

        midpoint.rotation =
            combatFrame.rotation;
    }

    private Transform ResolveTargetTransform()
    {
        if (targetPart != null)
        {
            CharacterView view =
                target != null
                    ? target.GetComponent<
                        CharacterView>()
                    : null;

            Transform anchor =
                view?.GetBodyPartAnchor(
                    targetPart);

            if (anchor != null)
                return anchor;
        }

        return
            GetVisualRoot(target) ??
            target?.transform;
    }

    private static Transform GetVisualRoot(
        Character character)
    {
        if (character == null)
            return null;

        CharacterActionMover mover =
            character.GetComponent<
                CharacterActionMover>();

        if (mover != null)
            return mover.VisualRoot;

        return character.transform;
    }

    private static Transform ResolveAnchor(
        Character character,
        string key)
    {
        if (character == null)
            return null;

        CharacterCameraPointSet pointSet =
            character.GetComponentInChildren<
                CharacterCameraPointSet>(
                    true);

        Transform point =
            pointSet?.GetPoint(
                key);

        if (point != null)
            return point;

        CharacterView view =
            character.GetComponent<
                CharacterView>();

        if (view != null &&
            TryParsePartType(
                key,
                out PartType partType))
        {
            return view.GetBodyPartAnchor(
                partType);
        }

        return
            GetVisualRoot(character);
    }

    private static bool TryParsePartType(
        string key,
        out PartType partType)
    {
        partType =
            default;

        if (string.IsNullOrWhiteSpace(
                key))
        {
            return false;
        }

        string normalized =
            key.Trim()
                .Replace(
                    "Anchor",
                    string.Empty,
                    StringComparison
                        .OrdinalIgnoreCase)
                .Replace(
                    "_",
                    string.Empty)
                .Replace(
                    " ",
                    string.Empty)
                .ToUpperInvariant();

        return normalized switch
        {
            "HEAD" =>
                Assign(
                    PartType.HEAD,
                    out partType),

            "LEFTHAND" =>
                Assign(
                    PartType.LEFT_HAND,
                    out partType),

            "RIGHTHAND" =>
                Assign(
                    PartType.RIGHT_HAND,
                    out partType),

            "LEGS" =>
                Assign(
                    PartType.LEGS,
                    out partType),

            _ =>
                false
        };
    }

    private static bool Assign(
        PartType value,
        out PartType output)
    {
        output =
            value;

        return true;
    }

    private void EnsureRuntimeFrames()
    {
        if (combatFrame == null)
        {
            combatFrame =
                FindOrCreateChild(
                    "CombatFrame_Runtime");
        }

        if (midpoint == null)
        {
            midpoint =
                FindOrCreateChild(
                    "AttackerTargetMidpoint_Runtime");
        }
    }

    private Transform FindOrCreateChild(
        string childName)
    {
        Transform existing =
            transform.Find(
                childName);

        if (existing != null)
            return existing;

        GameObject child =
            new GameObject(
                childName);

        child.hideFlags =
            HideFlags.DontSave;

        child.transform.SetParent(
            transform,
            false);

        return child.transform;
    }

    private void RestoreCapturedTimeScale()
    {
        if (!capturedTimeScale)
            return;

        Time.timeScale =
            originalTimeScale;
    }

    private static float
        ExponentialDampingFactor(
            float damping,
            float deltaTime)
    {
        if (damping <= 0f)
            return 1f;

        return
            1f -
            Mathf.Exp(
                -deltaTime /
                Mathf.Max(
                    0.0001f,
                    damping));
    }

    private readonly struct
        SkillCutsceneBlendInfo
    {
        public SkillCutsceneBlendInfo(
            float duration,
            AnimationCurve curve,
            CinemachineBlendDefinition
                .Styles style =
                    CinemachineBlendDefinition
                        .Styles
                        .Custom)
        {
            Duration =
                Mathf.Max(
                    0f,
                    duration);

            Curve =
                curve;

            Style =
                style;
        }

        public float Duration { get; }
        public AnimationCurve Curve { get; }

        public CinemachineBlendDefinition
            .Styles Style
        {
            get;
        }
    }
}
