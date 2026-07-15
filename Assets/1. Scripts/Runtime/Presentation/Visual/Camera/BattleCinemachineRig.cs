using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public enum BattleCameraRigSlot
{
    None,
    Overview,
    Group,
    Pose,
    Prestige
}

[DisallowMultipleComponent]
public class BattleCinemachineRig : MonoBehaviour
{
    [Header("Brain")]
    public CinemachineBrain Brain;

    [Header("Cinemachine Cameras")]
    public CinemachineCamera OverviewCamera;
    public CinemachineCamera GroupCamera;
    public CinemachineCamera PoseCamera;

    [Tooltip("선택 사항입니다. 연결하지 않으면 위세 카메라는 Overview로 폴백합니다.")]
    public CinemachineCamera PrestigeCamera;

    [Header("Target Group")]
    public CinemachineTargetGroup TargetGroup;

    [Header("Impulse")]
    public CinemachineImpulseSource ImpulseSource;

    [Header("Priority")]
    public int ActivePriority = 100;
    public int InactivePriority = 0;

    [Header("Startup")]
    [SerializeField] private bool activateOverviewOnStart = true;
    [SerializeField] private bool validateOnAwake = true;

    private CinemachineBlendDefinition baseBlend;
    private bool hasBaseBlend;

    public BattleCameraRigSlot ActiveSlot { get; private set; }
    public CinemachineCamera ActiveCamera { get; private set; }

    public bool HasBrain => Brain != null;
    public bool HasOverview => OverviewCamera != null;
    public bool IsCoreReady => Brain != null && OverviewCamera != null;

    public CinemachineBlendDefinition BaseBlend
    {
        get
        {
            CaptureBaseBlendIfNeeded();
            return baseBlend;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        NormalizePriorities();
        CaptureBaseBlendIfNeeded();

        if (validateOnAwake)
            ValidateConfiguration(true);
    }

    private void Start()
    {
        if (activateOverviewOnStart && OverviewCamera != null)
            SetLive(BattleCameraRigSlot.Overview);
    }

    private void OnDisable()
    {
        RestoreBaseBlend();
    }

    public void ResolveReferences()
    {
        if (Brain == null)
        {
            Brain = GetComponentInChildren<CinemachineBrain>(true);

            if (Brain == null)
                Brain = FindFirstObjectByType<CinemachineBrain>();
        }

        if (TargetGroup == null)
            TargetGroup = GetComponentInChildren<CinemachineTargetGroup>(true);

        if (ImpulseSource == null)
            ImpulseSource = GetComponentInChildren<CinemachineImpulseSource>(true);

        ResolveCamerasByNameWhenMissing();
        CaptureBaseBlendIfNeeded();
    }

    public bool SetLive(BattleCameraRigSlot slot)
    {
        CinemachineCamera camera = GetCamera(slot);

        if (camera == null)
        {
            if (slot == BattleCameraRigSlot.Prestige && OverviewCamera != null)
            {
                slot = BattleCameraRigSlot.Overview;
                camera = OverviewCamera;
            }
            else
            {
                Debug.LogWarning(
                    $"[BattleCinemachineRig] 활성화할 카메라가 없습니다. Slot={slot}",
                    this);
                return false;
            }
        }

        return SetLive(camera, slot);
    }

    public bool SetLive(CinemachineCamera targetCamera)
    {
        BattleCameraRigSlot slot = ResolveSlot(targetCamera);
        return SetLive(targetCamera, slot);
    }

    public CinemachineCamera GetCamera(BattleCameraRigSlot slot)
    {
        return slot switch
        {
            BattleCameraRigSlot.Overview => OverviewCamera,
            BattleCameraRigSlot.Group => GroupCamera,
            BattleCameraRigSlot.Pose => PoseCamera,
            BattleCameraRigSlot.Prestige => PrestigeCamera,
            _ => null
        };
    }

    public void ApplyBrainBlend(
        CinemachineBlendDefinition.Styles style,
        float time)
    {
        if (Brain == null)
            return;

        Brain.DefaultBlend =
            new CinemachineBlendDefinition(
                style,
                Mathf.Max(0f, time));
    }

    public void RestoreBaseBlend()
    {
        if (Brain == null || !hasBaseBlend)
            return;

        Brain.DefaultBlend = baseBlend;
    }

    public void CaptureCurrentBlendAsBase()
    {
        if (Brain == null)
            return;

        baseBlend = Brain.DefaultBlend;
        hasBaseBlend = true;
    }

    public bool ValidateConfiguration(bool log)
    {
        bool valid = true;

        if (Brain == null)
        {
            valid = false;
            LogValidation(log, "CinemachineBrain이 연결되지 않았습니다.");
        }

        if (OverviewCamera == null)
        {
            valid = false;
            LogValidation(log, "OverviewCamera가 연결되지 않았습니다.");
        }

        if (GroupCamera == null)
            LogValidation(log, "GroupCamera가 없습니다. 합/양자 구도는 재생되지 않습니다.");

        if (PoseCamera == null)
            LogValidation(log, "PoseCamera가 없습니다. 클로즈업/CameraPoint Shot은 재생되지 않습니다.");

        if (TargetGroup == null)
            LogValidation(log, "CinemachineTargetGroup이 없습니다. GroupCamera 타겟 바인딩을 사용할 수 없습니다.");

        if (ActivePriority <= InactivePriority)
        {
            valid = false;
            LogValidation(
                log,
                $"ActivePriority({ActivePriority})는 InactivePriority({InactivePriority})보다 커야 합니다.");
        }

        ValidateDistinctCamera(log, OverviewCamera, GroupCamera, "OverviewCamera", "GroupCamera", ref valid);
        ValidateDistinctCamera(log, OverviewCamera, PoseCamera, "OverviewCamera", "PoseCamera", ref valid);
        ValidateDistinctCamera(log, GroupCamera, PoseCamera, "GroupCamera", "PoseCamera", ref valid);

        return valid;
    }

    public void RestoreOverviewImmediate()
    {
        RestoreBaseBlend();
        SetLive(BattleCameraRigSlot.Overview);
    }

    private bool SetLive(
        CinemachineCamera targetCamera,
        BattleCameraRigSlot slot)
    {
        if (targetCamera == null)
            return false;

        if (!IsRegisteredCamera(targetCamera))
        {
            Debug.LogWarning(
                $"[BattleCinemachineRig] Rig에 등록되지 않은 CinemachineCamera입니다. Camera={targetCamera.name}",
                this);
            return false;
        }

        NormalizePriorities();

        SetPriority(OverviewCamera, targetCamera == OverviewCamera);
        SetPriority(GroupCamera, targetCamera == GroupCamera);
        SetPriority(PoseCamera, targetCamera == PoseCamera);
        SetPriority(PrestigeCamera, targetCamera == PrestigeCamera);

        ActiveCamera = targetCamera;
        ActiveSlot = slot != BattleCameraRigSlot.None
            ? slot
            : ResolveSlot(targetCamera);

        return true;
    }

    private void ResolveCamerasByNameWhenMissing()
    {
        CinemachineCamera[] cameras =
            GetComponentsInChildren<CinemachineCamera>(true);

        foreach (CinemachineCamera camera in cameras)
        {
            if (camera == null)
                continue;

            string lowerName = camera.name.ToLowerInvariant();

            if (OverviewCamera == null && lowerName.Contains("overview"))
                OverviewCamera = camera;
            else if (GroupCamera == null && (lowerName.Contains("group") || lowerName.Contains("clash")))
                GroupCamera = camera;
            else if (PoseCamera == null && (lowerName.Contains("pose") || lowerName.Contains("focus")))
                PoseCamera = camera;
            else if (PrestigeCamera == null && lowerName.Contains("prestige"))
                PrestigeCamera = camera;
        }
    }

    private void CaptureBaseBlendIfNeeded()
    {
        if (hasBaseBlend || Brain == null)
            return;

        baseBlend = Brain.DefaultBlend;
        hasBaseBlend = true;
    }

    private void NormalizePriorities()
    {
        if (ActivePriority <= InactivePriority)
            ActivePriority = InactivePriority + 1;
    }

    private bool IsRegisteredCamera(CinemachineCamera camera)
    {
        return camera == OverviewCamera ||
               camera == GroupCamera ||
               camera == PoseCamera ||
               camera == PrestigeCamera;
    }

    private BattleCameraRigSlot ResolveSlot(CinemachineCamera camera)
    {
        if (camera == OverviewCamera)
            return BattleCameraRigSlot.Overview;

        if (camera == GroupCamera)
            return BattleCameraRigSlot.Group;

        if (camera == PoseCamera)
            return BattleCameraRigSlot.Pose;

        if (camera == PrestigeCamera)
            return BattleCameraRigSlot.Prestige;

        return BattleCameraRigSlot.None;
    }

    private void SetPriority(CinemachineCamera camera, bool active)
    {
        if (camera == null)
            return;

        camera.Priority = active
            ? ActivePriority
            : InactivePriority;
    }

    private static void ValidateDistinctCamera(
        bool log,
        CinemachineCamera first,
        CinemachineCamera second,
        string firstName,
        string secondName,
        ref bool valid)
    {
        if (first == null || second == null || first != second)
            return;

        valid = false;

        if (log)
        {
            Debug.LogError(
                $"[BattleCinemachineRig] {firstName}와 {secondName}가 같은 카메라를 참조합니다.");
        }
    }

    private void LogValidation(bool log, string message)
    {
        if (!log)
            return;

        Debug.LogWarning(
            $"[BattleCinemachineRig] {message}",
            this);
    }
}
