using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 스킬 전용 Camera Rig Prefab의 Runtime Registry.
/// 자식 CinemachineCamera 이름을 CameraKey로 사용한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillCutsceneCameraRig :
    MonoBehaviour
{
    [SerializeField]
    private string overviewCameraKey =
        "CM_Overview";

    [SerializeField]
    private int activePriority =
        250;

    [SerializeField]
    private int inactivePriority =
        -10;

    [SerializeField]
    private CinemachineBrain brain;

    private readonly Dictionary<
        string,
        CinemachineCamera> cameras =
            new Dictionary<
                string,
                CinemachineCamera>(
                    StringComparer
                        .OrdinalIgnoreCase);

    private CinemachineBlendDefinition
        originalBlend;

    private bool hasOriginalBlend;

    public Transform RigRoot =>
        transform;

    public CinemachineBrain Brain =>
        brain;

    public string OverviewCameraKey =>
        overviewCameraKey;

    private void Awake()
    {
        Rebuild();
        ResolveBrain();
        CaptureOriginalBlend();
    }

    private void OnEnable()
    {
        Rebuild();
        ResolveBrain();
        CaptureOriginalBlend();
    }

    public void ConfigureBrain(
        CinemachineBrain value)
    {
        brain =
            value;

        CaptureOriginalBlend();
    }

    public void Rebuild()
    {
        cameras.Clear();

        CinemachineCamera[] found =
            GetComponentsInChildren<
                CinemachineCamera>(
                    true);

        foreach (CinemachineCamera camera
                 in found)
        {
            if (camera == null)
                continue;

            cameras[camera.name] =
                camera;

            camera.Priority =
                inactivePriority;
        }
    }

    public IReadOnlyCollection<string>
        GetCameraKeys()
    {
        Rebuild();
        return cameras.Keys;
    }

    public CinemachineCamera GetCamera(
        string key)
    {
        if (string.IsNullOrWhiteSpace(
                key))
        {
            return null;
        }

        if (cameras.Count == 0)
            Rebuild();

        return cameras.TryGetValue(
                key,
                out CinemachineCamera camera)
            ? camera
            : null;
    }

    public Transform GetPoseTransform(
        string key)
    {
        CinemachineCamera camera =
            GetCamera(key);

        if (camera == null)
            return null;

        SkillCutsceneCameraBindingRoot
            bindingRoot =
                camera.GetComponentInParent<
                    SkillCutsceneCameraBindingRoot>();

        return bindingRoot != null
            ? bindingRoot.transform
            : camera.transform;
    }

    public bool Activate(
        string key,
        CinemachineBlendDefinition.Styles style,
        float duration,
        AnimationCurve customCurve)
    {
        CinemachineCamera target =
            GetCamera(key);

        if (target == null)
        {
            Debug.LogWarning(
                "[SkillCutsceneCameraRig] " +
                $"CameraKey를 찾지 못했습니다: {key}",
                this);

            return false;
        }

        ResolveBrain();
        ApplyBlend(
            style,
            duration,
            customCurve);

        foreach (CinemachineCamera camera
                 in cameras.Values)
        {
            if (camera == null)
                continue;

            camera.Priority =
                camera == target
                    ? activePriority
                    : inactivePriority;
        }

        return true;
    }

    public void RestoreOverview()
    {
        ResolveBrain();

        if (brain != null)
        {
            brain.DefaultBlend =
                new CinemachineBlendDefinition(
                    CinemachineBlendDefinition
                        .Styles
                        .EaseOut,
                    0.2f);
        }

        // Skill Rig의 모든 카메라를 비활성 우선순위로 내리면
        // 기존 BattleCinemachineRig의 Overview가 다시 Live가 된다.
        foreach (CinemachineCamera camera
                 in cameras.Values)
        {
            if (camera != null)
            {
                camera.Priority =
                    inactivePriority;
            }
        }
    }

    public void RestoreOriginalBlend()
    {
        if (brain != null &&
            hasOriginalBlend)
        {
            brain.DefaultBlend =
                originalBlend;
        }
    }

    private void ResolveBrain()
    {
        if (brain == null)
        {
            brain =
                FindFirstObjectByType<
                    CinemachineBrain>(
                        FindObjectsInactive
                            .Include);
        }
    }

    private void CaptureOriginalBlend()
    {
        if (brain == null ||
            hasOriginalBlend)
        {
            return;
        }

        originalBlend =
            brain.DefaultBlend;

        hasOriginalBlend =
            true;
    }

    private void ApplyBlend(
        CinemachineBlendDefinition.Styles style,
        float duration,
        AnimationCurve customCurve)
    {
        if (brain == null)
            return;

        CinemachineBlendDefinition definition =
            new CinemachineBlendDefinition(
                style,
                Mathf.Max(
                    0f,
                    duration));

        if (style ==
                CinemachineBlendDefinition
                    .Styles
                    .Custom &&
            customCurve != null)
        {
            definition =
                TryAssignCustomCurve(
                    definition,
                    customCurve);
        }

        brain.DefaultBlend =
            definition;
    }

    private static CinemachineBlendDefinition
        TryAssignCustomCurve(
            CinemachineBlendDefinition definition,
            AnimationCurve curve)
    {
        object boxed =
            definition;

        Type type =
            typeof(
                CinemachineBlendDefinition);

        FieldInfo field =
            type.GetField(
                "CustomCurve",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        if (field != null)
        {
            field.SetValue(
                boxed,
                curve);

            return
                (CinemachineBlendDefinition)
                boxed;
        }

        PropertyInfo property =
            type.GetProperty(
                "CustomCurve",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        if (property != null &&
            property.CanWrite)
        {
            property.SetValue(
                boxed,
                curve);

            return
                (CinemachineBlendDefinition)
                boxed;
        }

        return definition;
    }
}