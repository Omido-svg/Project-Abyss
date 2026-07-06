using Unity.Cinemachine;
using UnityEngine;

public class BattleCameraDirector : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera overviewCamera;
    [SerializeField] private CinemachineCamera attackerCamera;
    [SerializeField] private CinemachineCamera targetCamera;

    [Header("Priority")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;

    private void Start()
    {
        ShowOverview();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            ShowOverview();

        if (Input.GetKeyDown(KeyCode.F2))
            ShowAttacker();

        if (Input.GetKeyDown(KeyCode.F3))
            ShowTarget();
    }

    public void ShowOverview()
    {
        SetActiveCamera(overviewCamera);
    }

    public void ShowAttacker()
    {
        SetActiveCamera(attackerCamera);
    }

    public void ShowTarget()
    {
        SetActiveCamera(targetCamera);
    }

    private void SetActiveCamera(
        CinemachineCamera activeCamera)
    {
        SetPriority(overviewCamera, inactivePriority);
        SetPriority(attackerCamera, inactivePriority);
        SetPriority(targetCamera, inactivePriority);

        SetPriority(activeCamera, activePriority);
    }

    private void SetPriority(
        CinemachineCamera camera,
        int priority)
    {
        if (camera == null)
            return;

        camera.Priority = priority;
    }
}