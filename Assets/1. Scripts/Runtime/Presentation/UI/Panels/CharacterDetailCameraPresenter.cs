using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 캐릭터 상세 패널의 카메라 포인트 탐색/검증/보정/재확인을 담당한다.
/// 패널 자체는 열기/닫기와 탭 표시만 조율한다.
/// </summary>
internal sealed class CharacterDetailCameraPresenter : IDisposable
{
    private readonly MonoBehaviour coroutineHost;
    private readonly Func<Character> currentCharacterProvider;
    private readonly Func<bool> detailModeActiveProvider;

    private BattleCameraDirector cameraDirector;
    private string detailCameraPointKey;
    private bool allowCloseCameraFallback;
    private float minimumTargetFacingDot;
    private bool autoCorrectMisalignedCameraPoint;
    private float minimumDetailCameraDistance;

    private Coroutine focusRoutine;
    private Transform runtimeCorrectedCameraPoint;

    public CharacterDetailCameraPresenter(
        MonoBehaviour coroutineHost,
        Func<Character> currentCharacterProvider,
        Func<bool> detailModeActiveProvider)
    {
        this.coroutineHost = coroutineHost;
        this.currentCharacterProvider = currentCharacterProvider;
        this.detailModeActiveProvider = detailModeActiveProvider;
    }

    public void Configure(
        BattleCameraDirector cameraDirector,
        string detailCameraPointKey,
        bool allowCloseCameraFallback,
        float minimumTargetFacingDot,
        bool autoCorrectMisalignedCameraPoint,
        float minimumDetailCameraDistance)
    {
        this.cameraDirector = cameraDirector;
        this.detailCameraPointKey =
            string.IsNullOrWhiteSpace(detailCameraPointKey)
                ? "DetailCameraPoint"
                : detailCameraPointKey;
        this.allowCloseCameraFallback = allowCloseCameraFallback;
        this.minimumTargetFacingDot = minimumTargetFacingDot;
        this.autoCorrectMisalignedCameraPoint =
            autoCorrectMisalignedCameraPoint;
        this.minimumDetailCameraDistance =
            Mathf.Max(0.1f, minimumDetailCameraDistance);
    }

    public void RequestFocus(Character target)
    {
        CancelPendingFocus();
        FocusCameraOnCharacter(target);

        if (coroutineHost != null &&
            coroutineHost.isActiveAndEnabled)
        {
            focusRoutine =
                coroutineHost.StartCoroutine(
                    ReassertAtEndOfFrame(target));
        }
    }

    public void CancelPendingFocus()
    {
        if (focusRoutine == null ||
            coroutineHost == null)
        {
            focusRoutine = null;
            return;
        }

        coroutineHost.StopCoroutine(focusRoutine);
        focusRoutine = null;
    }

    public void ReturnFromInteraction()
    {
        CancelPendingFocus();
        cameraDirector?.ReturnFromInteraction();
    }

    public void Dispose()
    {
        CancelPendingFocus();

        if (runtimeCorrectedCameraPoint != null)
        {
            UnityEngine.Object.Destroy(
                runtimeCorrectedCameraPoint.gameObject);

            runtimeCorrectedCameraPoint = null;
        }
    }

    private IEnumerator ReassertAtEndOfFrame(Character target)
    {
        yield return new WaitForEndOfFrame();
        focusRoutine = null;

        if (target == null ||
            currentCharacterProvider?.Invoke() != target ||
            detailModeActiveProvider?.Invoke() != true)
        {
            yield break;
        }

        FocusCameraOnCharacter(target);
    }

    private void FocusCameraOnCharacter(Character target)
    {
        if (target == null || cameraDirector == null)
            return;

        Transform cameraPoint =
            FindOwnedCameraPoint(
                target,
                detailCameraPointKey);

        string resolvedKey = detailCameraPointKey;

        if (cameraPoint == null &&
            allowCloseCameraFallback)
        {
            cameraPoint =
                FindOwnedCameraPoint(
                    target,
                    "CloseCameraPoint");
            resolvedKey = "CloseCameraPoint";

            if (cameraPoint == null)
            {
                cameraPoint =
                    FindOwnedCameraPoint(
                        target,
                        "FrontCameraPoint");
                resolvedKey = "FrontCameraPoint";
            }

            if (cameraPoint != null)
            {
                Debug.LogWarning(
                    "[CharacterDetailCamera] DetailCameraPoint가 없어 " +
                    $"Fallback을 사용합니다. " +
                    $"Target={GetCharacterName(target)}#{target.GetInstanceID()}, " +
                    $"Fallback={resolvedKey}, " +
                    $"PointPath={BattleCharacterPointerRouter.GetHierarchyPath(cameraPoint)}",
                    target);
            }
        }

        if (cameraPoint == null)
        {
            Debug.LogError(
                "[CharacterDetailCamera] 대상 캐릭터 하위에서 상세 카메라 포인트를 찾지 못했습니다. " +
                $"Target={GetCharacterName(target)}#{target.GetInstanceID()}, " +
                $"TargetPath={BattleCharacterPointerRouter.GetHierarchyPath(target.transform)}, " +
                $"Key={detailCameraPointKey}",
                target);
            return;
        }

        if (!cameraPoint.IsChildOf(target.transform))
        {
            Debug.LogError(
                "[CharacterDetailCamera] 다른 캐릭터의 CameraPoint 사용을 차단했습니다. " +
                $"Target={GetCharacterName(target)}#{target.GetInstanceID()}, " +
                $"TargetPath={BattleCharacterPointerRouter.GetHierarchyPath(target.transform)}, " +
                $"PointPath={BattleCharacterPointerRouter.GetHierarchyPath(cameraPoint)}",
                target);
            return;
        }

        Vector3 lookTarget =
            ResolveCharacterLookTarget(
                target,
                out Bounds visualBounds,
                out bool hasVisualBounds);

        Transform focusPoint = cameraPoint;

        bool pointValid =
            IsCameraPointAimingAtTarget(
                cameraPoint,
                lookTarget,
                out float targetFacingDot,
                out float targetDistance);

        if (!pointValid &&
            autoCorrectMisalignedCameraPoint)
        {
            focusPoint =
                BuildCorrectedRuntimeCameraPoint(
                    target,
                    cameraPoint,
                    lookTarget,
                    visualBounds,
                    hasVisualBounds,
                    targetDistance);

            Debug.LogWarning(
                "[CharacterDetailCamera][CAMERA_POINT_CORRECTED] " +
                $"Target={GetCharacterName(target)}#{target.GetInstanceID()}, " +
                $"SourcePoint={BattleCharacterPointerRouter.GetHierarchyPath(cameraPoint)}, " +
                $"SourcePosition={cameraPoint.position}, " +
                $"SourceRotation={cameraPoint.rotation.eulerAngles}, " +
                $"TargetFacingDot={targetFacingDot:0.###}, " +
                $"TargetDistance={targetDistance:0.###}, " +
                $"CorrectedPosition={focusPoint.position}, " +
                $"CorrectedRotation={focusPoint.rotation.eulerAngles}, " +
                $"LookTarget={lookTarget}",
                target);
        }
        else if (!pointValid)
        {
            Debug.LogError(
                "[CharacterDetailCamera] CameraPoint가 대상 캐릭터를 바라보지 않습니다. " +
                $"Target={GetCharacterName(target)}#{target.GetInstanceID()}, " +
                $"PointPath={BattleCharacterPointerRouter.GetHierarchyPath(cameraPoint)}, " +
                $"TargetFacingDot={targetFacingDot:0.###}, " +
                $"TargetDistance={targetDistance:0.###}",
                target);
            return;
        }

        Debug.Log(
            "[CharacterDetailCamera][CAMERA_FOCUS] " +
            $"Target={GetCharacterName(target)}#{target.GetInstanceID()}, " +
            $"Key={resolvedKey}, " +
            $"SourcePoint={cameraPoint.name}#{cameraPoint.GetInstanceID()}, " +
            $"SourcePointPath={BattleCharacterPointerRouter.GetHierarchyPath(cameraPoint)}, " +
            $"AppliedPoint={focusPoint.name}#{focusPoint.GetInstanceID()}, " +
            $"Position={focusPoint.position}, " +
            $"Rotation={focusPoint.rotation.eulerAngles}, " +
            $"LookTarget={lookTarget}",
            target);

        cameraDirector.FocusFromTransform(
            focusPoint,
            null,
            useCameraPointRotation: true);
    }

    private bool IsCameraPointAimingAtTarget(
        Transform cameraPoint,
        Vector3 lookTarget,
        out float targetFacingDot,
        out float targetDistance)
    {
        targetFacingDot = -1f;
        targetDistance = 0f;

        if (cameraPoint == null)
            return false;

        Vector3 toTarget =
            lookTarget - cameraPoint.position;

        targetDistance = toTarget.magnitude;

        if (targetDistance < minimumDetailCameraDistance)
            return false;

        targetFacingDot =
            Vector3.Dot(
                cameraPoint.forward.normalized,
                toTarget / targetDistance);

        return targetFacingDot >= minimumTargetFacingDot;
    }

    private Transform BuildCorrectedRuntimeCameraPoint(
        Character target,
        Transform sourcePoint,
        Vector3 lookTarget,
        Bounds visualBounds,
        bool hasVisualBounds,
        float sourceDistance)
    {
        if (runtimeCorrectedCameraPoint == null)
        {
            GameObject runtimePointObject =
                new("__RuntimeCorrectedDetailCameraPoint");

            runtimePointObject.hideFlags =
                HideFlags.HideInHierarchy |
                HideFlags.DontSaveInBuild;

            runtimeCorrectedCameraPoint =
                runtimePointObject.transform;
        }

        if (runtimeCorrectedCameraPoint.parent !=
            target.transform)
        {
            runtimeCorrectedCameraPoint.SetParent(
                target.transform,
                worldPositionStays: true);
        }

        Vector3 correctedPosition =
            sourcePoint != null
                ? sourcePoint.position
                : lookTarget;

        float safeDistance =
            Mathf.Max(
                minimumDetailCameraDistance,
                sourceDistance);

        if (sourcePoint == null ||
            sourceDistance < minimumDetailCameraDistance)
        {
            Camera sourceCamera = Camera.main;

            if (sourceCamera == null)
            {
                sourceCamera =
                    UnityEngine.Object
                        .FindFirstObjectByType<Camera>();
            }

            Vector3 viewDirection =
                sourceCamera != null
                    ? sourceCamera.transform.position -
                      lookTarget
                    : -target.transform.forward;

            if (viewDirection.sqrMagnitude < 0.0001f)
                viewDirection = -target.transform.forward;

            viewDirection.Normalize();

            float boundsDistance =
                hasVisualBounds
                    ? Mathf.Max(
                        2.5f,
                        visualBounds.extents.magnitude *
                        2.2f)
                    : 3f;

            safeDistance =
                Mathf.Max(
                    safeDistance,
                    boundsDistance);

            correctedPosition =
                lookTarget +
                viewDirection * safeDistance;
        }

        Vector3 correctedForward =
            lookTarget - correctedPosition;

        if (correctedForward.sqrMagnitude < 0.0001f)
            correctedForward = target.transform.forward;

        runtimeCorrectedCameraPoint.position =
            correctedPosition;

        runtimeCorrectedCameraPoint.rotation =
            Quaternion.LookRotation(
                correctedForward.normalized,
                Vector3.up);

        return runtimeCorrectedCameraPoint;
    }

    private static Vector3 ResolveCharacterLookTarget(
        Character target,
        out Bounds visualBounds,
        out bool hasVisualBounds)
    {
        hasVisualBounds =
            TryGetCharacterVisualBounds(
                target,
                out visualBounds);

        if (!hasVisualBounds)
        {
            visualBounds =
                new Bounds(
                    target.transform.position +
                    Vector3.up * 1.4f,
                    Vector3.one);
        }

        return visualBounds.center +
               Vector3.up *
               visualBounds.extents.y *
               0.08f;
    }

    private static bool TryGetCharacterVisualBounds(
        Character target,
        out Bounds bounds)
    {
        bounds = default;

        if (target == null)
            return false;

        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>(true);

        bool initialized = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;

            if (!IsFinite(rendererBounds.center) ||
                !IsFinite(rendererBounds.extents) ||
                rendererBounds.extents.sqrMagnitude <=
                0.000001f)
            {
                continue;
            }

            if (!initialized)
            {
                bounds = rendererBounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(rendererBounds);
            }
        }

        return initialized;
    }

    private static bool IsFinite(Vector3 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y) &&
            !float.IsInfinity(value.z);
    }

    private static Transform FindOwnedCameraPoint(
        Character target,
        string key)
    {
        if (target == null ||
            string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        Transform[] transforms =
            target.GetComponentsInChildren<Transform>(true);

        Transform fallback = null;

        foreach (Transform candidate in transforms)
        {
            if (candidate == null ||
                candidate == target.transform ||
                !string.Equals(
                    candidate.name,
                    key,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!candidate.IsChildOf(target.transform))
                continue;

            if (candidate.parent != null &&
                string.Equals(
                    candidate.parent.name,
                    "CameraPoints",
                    StringComparison.Ordinal))
            {
                return candidate;
            }

            fallback ??= candidate;
        }

        return fallback;
    }

    private static string GetCharacterName(Character target)
    {
        if (target == null)
            return "NULL";

        return target.Data?.CharacterName ?? target.name;
    }
}
