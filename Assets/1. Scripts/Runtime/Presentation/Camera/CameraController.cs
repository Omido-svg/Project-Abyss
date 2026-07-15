using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraController : MonoBehaviour
{
    [Header("Legacy Compatibility Adapter")]
    [SerializeField] private BattleCameraDirector battleCameraDirector;
    [SerializeField] private bool logLegacyCalls = true;

    private bool warnedNoiseOffset;

    public static CameraController Instance { get; private set; }

    public bool IsMoving =>
        battleCameraDirector != null &&
        battleCameraDirector.IsMoving;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[CameraController] 씬에 Legacy CameraController가 2개 이상 있습니다.",
                this);
        }

        Instance = this;
        ResolveDirector();

        if (battleCameraDirector == null)
        {
            Debug.LogWarning(
                "[CameraController] BattleCameraDirector가 없습니다. " +
                "이 컴포넌트는 더 이상 독립 카메라 시스템으로 동작하지 않습니다.",
                this);
        }
        else
        {
            LogLegacy(
                "Legacy CameraController가 BattleCameraDirector 호환 어댑터로 동작합니다. " +
                "전투 씬에서는 제거해도 됩니다.");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void FocusFromTransform(
        Transform cameraPoint,
        Transform lookAtPoint,
        bool useCameraPointRotation)
    {
        ResolveDirector();
        battleCameraDirector?.FocusFromTransform(
            cameraPoint,
            lookAtPoint,
            useCameraPointRotation);
    }

    public IEnumerator WaitUntilArrived(float timeout = 1.5f)
    {
        ResolveDirector();

        if (battleCameraDirector != null)
        {
            yield return battleCameraDirector.WaitUntilArrived(timeout);
            yield break;
        }

        if (timeout > 0f)
            yield return new WaitForSeconds(timeout);
    }

    public void Focus(Vector3 worldPosition)
    {
        ResolveDirector();
        battleCameraDirector?.Focus(worldPosition, 5f);
    }

    public void Return()
    {
        ResolveDirector();
        battleCameraDirector?.Return();
    }

    public void ShowPrestige()
    {
        ResolveDirector();
        battleCameraDirector?.ShowPrestige();
    }

    public void SnapToOverview()
    {
        ResolveDirector();
        battleCameraDirector?.SnapToOverview();
    }

    public void SnapToFocus(Vector3 worldPosition)
    {
        ResolveDirector();
        battleCameraDirector?.SnapToFocus(worldPosition);
    }

    public void FocusBetween(Character a, Character b)
    {
        ResolveDirector();
        battleCameraDirector?.FocusBetween(a, b);
    }

    public void FocusCharacter(
        Character character,
        Vector3 positionOffset,
        Vector3 lookAtOffset,
        float distance)
    {
        ResolveDirector();
        battleCameraDirector?.FocusCharacter(
            character,
            positionOffset,
            lookAtOffset,
            distance);
    }

    public void SetNoiseOffset(
        Vector3 positionOffset,
        Vector3 eulerOffset)
    {
        if (warnedNoiseOffset)
            return;

        warnedNoiseOffset = true;

        Debug.LogWarning(
            "[CameraController] SetNoiseOffset은 Cinemachine 3 경로에서 사용하지 않습니다. " +
            "BattleCameraDirector.PlayShake와 CinemachineImpulseSource를 사용하세요.",
            this);
    }

    private void ResolveDirector()
    {
        if (battleCameraDirector == null)
            battleCameraDirector = FindFirstObjectByType<BattleCameraDirector>();
    }

    private void LogLegacy(string message)
    {
        if (logLegacyCalls)
            Debug.Log($"[CameraController] {message}", this);
    }
}
