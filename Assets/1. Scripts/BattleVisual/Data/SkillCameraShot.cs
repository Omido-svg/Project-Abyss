using System;
using UnityEngine;
using Unity.Cinemachine;

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
    
    [Header("Cinemachine Brain Blend")]
    public bool OverrideBrainBlend = true;
    public CinemachineBlendDefinition.Styles BlendStyle =
        CinemachineBlendDefinition.Styles.Cut;
    public float BlendTime = 0f;

    [Header("Cinemachine Group Framing")]
    public float AttackerWeight = 1f;
    public float TargetWeight = 1f;
    public float AttackerRadius = 1f;
    public float TargetRadius = 1f;

    [Header("Side View")]
    public bool UseSideViewPlacement = false;
    public float SideDistance = 7f;
    public float SideHeight = 2.4f;
    public float LookAtHeight = 1.4f;
    public bool FlipSide = false;

    [Header("Offset Position Mode")]
    public Vector3 PositionOffset = new Vector3(0f, 2f, -5f);
    public Vector3 LookAtOffset = new Vector3(0f, 1.2f, 0f);
    public float FocusDistance = 5f;

    [Header("Scene Camera Point Mode")]
    public bool UseSceneCameraPoint = false;

    public SkillCameraPointReference CameraPoint =
        new SkillCameraPointReference
        {
            Owner = SkillCameraPointOwner.Attacker,
            Key = "OverShoulder"
        };

    [Header("Rotation")]
    public SkillCameraRotationMode RotationMode = SkillCameraRotationMode.LookAtPoint;

    public SkillCameraPointReference LookAtPoint =
        new SkillCameraPointReference
        {
            Owner = SkillCameraPointOwner.Target,
            Key = "LookAt"
        };

    [Header("Shake")]
    public bool UseShake;
    public BattleCameraShakeSettings Shake = new BattleCameraShakeSettings();
}

[Serializable]
public class SkillCameraPointReference
{
    public SkillCameraPointOwner Owner = SkillCameraPointOwner.Attacker;
    public string Key = "LookAt";
}

public enum SkillCameraShotTiming
{
    OnActionStart,
    BeforeAttackAnimation,
    OnHitFrame,
    AfterAction,
    OnClashRoll
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

public enum SkillCameraRotationMode
{
    LookAtPoint,
    CameraPointRotation
}