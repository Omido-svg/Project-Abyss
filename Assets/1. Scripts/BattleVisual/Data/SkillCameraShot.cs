using System;
using UnityEngine;

[Serializable]
public class SkillCameraShot
{
    [Header("When")]
    public SkillCameraShotTiming Timing = SkillCameraShotTiming.OnActionStart;

    [Header("Shot")]
    public SkillCameraShotType ShotType = SkillCameraShotType.FocusBetween;

    [Header("Time")]
    public float Duration = 0.35f;
    public float BlendWaitTime = 0.15f;

    [Header("Offset Position Mode")]
    public Vector3 PositionOffset = new Vector3(0f, 2f, -5f);
    public Vector3 LookAtOffset = new Vector3(0f, 1.2f, 0f);
    public float FocusDistance = 5f;

    [Header("Scene Camera Point Mode")]
    public bool UseSceneCameraPoint = false;

    public SkillCameraPointOwner CameraPointOwner = SkillCameraPointOwner.Attacker;
    public SkillCameraPointType CameraPoint = SkillCameraPointType.OverShoulder;

    public SkillCameraPointOwner LookAtOwner = SkillCameraPointOwner.Target;
    public SkillCameraPointType LookAtCameraPoint = SkillCameraPointType.LookAt;

    [Header("Shake")]
    public bool UseShake;
    public BattleCameraShakeSettings Shake = new BattleCameraShakeSettings();
}

public enum SkillCameraShotTiming
{
    OnActionStart,
    BeforeAttackAnimation,
    OnHitFrame,
    AfterAction
}

public enum SkillCameraShotType
{
    FocusBetween,
    AttackerClose,
    TargetClose,
    AttackerOverShoulder,
    TargetOverShoulder,
    ClashWide,
    HitImpact,
    ReturnOverview
}

public enum SkillCameraPointOwner
{
    Attacker,
    Target
}

public enum SkillCameraPointType
{
    LookAt,
    Close,
    OverShoulder,
    HitImpact,
    Side
}