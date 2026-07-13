using Unity.Cinemachine;
using UnityEngine;

public class BattleCinemachineRig : MonoBehaviour
{
    [Header("Brain")]
    public CinemachineBrain Brain;

    [Header("Cinemachine Cameras")]
    public CinemachineCamera OverviewCamera;
    public CinemachineCamera GroupCamera;
    public CinemachineCamera PoseCamera;

    [Header("Target Group")]
    public CinemachineTargetGroup TargetGroup;

    [Header("Impulse")]
    public CinemachineImpulseSource ImpulseSource;

    [Header("Priority")]
    public int ActivePriority = 100;
    public int InactivePriority = 0;

    private void Awake()
    {
        if (Brain == null)
            Brain = FindFirstObjectByType<CinemachineBrain>();

        if (TargetGroup == null)
            TargetGroup = GetComponentInChildren<CinemachineTargetGroup>(true);

        if (ImpulseSource == null)
            ImpulseSource = GetComponentInChildren<CinemachineImpulseSource>(true);
    }

    public void SetLive(CinemachineCamera targetCamera)
    {
        SetPriority(
            OverviewCamera,
            targetCamera == OverviewCamera);

        SetPriority(
            GroupCamera,
            targetCamera == GroupCamera);

        SetPriority(
            PoseCamera,
            targetCamera == PoseCamera);
    }

    private void SetPriority(
        CinemachineCamera camera,
        bool active)
    {
        if (camera == null)
            return;

        camera.Priority =
            active
                ? ActivePriority
                : InactivePriority;
    }
}