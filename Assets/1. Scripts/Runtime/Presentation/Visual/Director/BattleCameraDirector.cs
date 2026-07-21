using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class BattleCameraDirector : MonoBehaviour
{
    [Header("Cinemachine 3 Rig")]
    [SerializeField] private BattleCinemachineRig rig;

    [Header("Interaction Camera")]
    [SerializeField, Min(0.01f)]
    private float interactionMoveSpeed = 8f;

    [SerializeField, Min(0.01f)]
    private float interactionRotationSpeed = 360f;

    [SerializeField, Min(0.001f)]
    private float interactionArriveDistance = 0.01f;

    [SerializeField, Min(0.01f)]
    private float interactionArriveAngle = 0.1f;

    [Header("Fallback Framing")]
    [SerializeField, Min(0.1f)]
    private float defaultGroupSideDistance = 7f;

    [SerializeField]
    private float defaultGroupHeight = 2.4f;

    [SerializeField]
    private float defaultGroupLookAtHeight = 1.4f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BattleCinemachineTargetGroupBinder groupBinder;
    private Coroutine restoreBlendRoutine;
    private Coroutine impactPulseRoutine;

    private CinemachineCamera impactPulseCamera;
    private float impactPulseRestoreFov;
    private bool hasImpactPulseRestoreFov;

    private readonly HashSet<string>
        reportedCameraPointFallbacks =
            new();

    private bool interactionCameraActive;
    private Vector3 interactionTargetPosition;
    private Quaternion interactionTargetRotation;

    public bool IsAvailable => rig != null && rig.IsCoreReady;

    public bool IsMoving
    {
        get
        {
            bool interactionMoving =
                interactionCameraActive &&
                rig != null &&
                rig.PoseCamera != null &&
                !HasInteractionCameraArrived(rig.PoseCamera.transform);

            bool brainBlending =
                rig != null &&
                rig.Brain != null &&
                rig.Brain.ActiveBlend != null;

            return interactionMoving || brainBlending;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        BuildGroupBinder();

        if (!IsAvailable)
        {
            Debug.LogError(
                "[BattleCameraDirector] Cinemachine 3 BattleCinemachineRig 구성이 유효하지 않습니다. " +
                "전투 씬에서는 CameraController 폴백을 사용하지 않습니다.",
                this);
        }
    }

    private void OnDisable()
    {
        CancelImpactPulse(restoreLens: true);
        StopAllCoroutines();
        restoreBlendRoutine = null;
        interactionCameraActive = false;
        groupBinder?.Clear();
        rig?.RestoreBaseBlend();
    }

    private void OnDestroy()
    {
        groupBinder?.Dispose();
        groupBinder = null;
    }

    private void Update()
    {
        UpdateInteractionCamera();
    }

    private void ResolveReferences()
    {
        if (rig == null)
            rig = FindFirstObjectByType<BattleCinemachineRig>();

        rig?.ResolveReferences();
    }

    private void BuildGroupBinder()
    {
        groupBinder?.Dispose();
        groupBinder = null;

        if (rig != null && rig.TargetGroup != null)
        {
            groupBinder =
                new BattleCinemachineTargetGroupBinder(
                    rig.TargetGroup);
        }
    }

    public void FocusBetween(Character a, Character b)
    {
        StopInteractionFocus();

        if (!EnsureRig("FocusBetween"))
            return;

        Transform aTarget = BattleCameraTargetResolver.GetLookAtTarget(a);
        Transform bTarget = BattleCameraTargetResolver.GetLookAtTarget(b);

        if (aTarget == null && bTarget == null)
            return;

        if (a == b || aTarget == bTarget)
        {
            FocusSingleTargetWithGroupCamera(
                aTarget != null ? aTarget : bTarget,
                defaultGroupSideDistance,
                defaultGroupHeight,
                defaultGroupLookAtHeight);
        }
        else
        {
            groupBinder?.BindTwoTargets(
                aTarget,
                bTarget,
                1f,
                1f,
                1f,
                1f);

            PlaceGroupCameraSideView(
                aTarget,
                bTarget,
                defaultGroupSideDistance,
                defaultGroupHeight,
                defaultGroupLookAtHeight,
                false);

            rig.SetLive(BattleCameraRigSlot.Group);
        }

        Log(
            $"FocusBetween / A={GetCharacterName(a)}, B={GetCharacterName(b)}");
    }

    public void Focus(Vector3 worldPosition, float distance)
    {
        if (!EnsurePoseCamera("Focus"))
            return;

        Vector3 referenceForward =
            rig.OverviewCamera != null
                ? rig.OverviewCamera.transform.forward
                : rig.PoseCamera.transform.forward;

        if (referenceForward.sqrMagnitude <= 0.0001f)
            referenceForward = Vector3.forward;

        float safeDistance = Mathf.Max(0.1f, distance);

        Vector3 cameraPosition =
            worldPosition -
            referenceForward.normalized * safeDistance;

        Quaternion cameraRotation = CreateLookRotation(
            worldPosition - cameraPosition,
            rig.PoseCamera.transform.rotation);

        BeginInteractionFocus();

        interactionTargetPosition = cameraPosition;
        interactionTargetRotation = cameraRotation;
        interactionCameraActive = true;

        Log($"Focus / Position={worldPosition}, Distance={safeDistance}");
    }

    public void FocusCharacter(
        Character character,
        Vector3 positionOffset,
        Vector3 lookAtOffset,
        float distance)
    {
        if (character == null || !EnsurePoseCamera("FocusCharacter"))
            return;

        StopInteractionFocus();

        Transform focusTarget = BattleCameraTargetResolver.GetLookAtTarget(character);

        if (focusTarget == null)
            return;

        Vector3 lookAtPosition =
            focusTarget.position +
            focusTarget.TransformDirection(lookAtOffset);

        Vector3 offset =
            focusTarget.TransformDirection(positionOffset);

        if (offset.sqrMagnitude <= 0.0001f)
        {
            Vector3 referenceForward =
                rig.OverviewCamera != null
                    ? rig.OverviewCamera.transform.forward
                    : -focusTarget.forward;

            offset =
                -referenceForward.normalized *
                Mathf.Max(0.1f, distance);
        }

        SetPoseCamera(
            lookAtPosition + offset,
            lookAtPosition,
            null);

        Log(
            $"FocusCharacter / Character={GetCharacterName(character)}");
    }

    public void FocusFromTransform(
        Transform cameraPoint,
        Transform lookAtPoint,
        bool useCameraPointRotation)
    {
        if (cameraPoint == null || !EnsurePoseCamera("FocusFromTransform"))
            return;

        StopInteractionFocus();

        Quaternion rotation;

        if (useCameraPointRotation || lookAtPoint == null)
        {
            rotation = cameraPoint.rotation;
        }
        else
        {
            rotation = CreateLookRotation(
                lookAtPoint.position - cameraPoint.position,
                cameraPoint.rotation);
        }

        rig.PoseCamera.transform.SetPositionAndRotation(
            cameraPoint.position,
            rotation);

        rig.SetLive(BattleCameraRigSlot.Pose);
    }

    public void ShowPrestige()
    {
        StopInteractionFocus();

        if (!EnsureRig("ShowPrestige"))
            return;

        rig.SetLive(
            rig.PrestigeCamera != null
                ? BattleCameraRigSlot.Prestige
                : BattleCameraRigSlot.Overview);
    }

    public void SnapToOverview()
    {
        CancelImpactPulse(restoreLens: true);
        StopInteractionFocus();
        groupBinder?.Clear();
        CancelBlendRestore();
        rig?.RestoreBaseBlend();
        rig?.SetLive(BattleCameraRigSlot.Overview);
    }

    public void SnapToFocus(Vector3 worldPosition, float distance = 5f)
    {
        if (!EnsurePoseCamera("SnapToFocus"))
            return;

        Vector3 referenceForward =
            rig.OverviewCamera != null
                ? rig.OverviewCamera.transform.forward
                : rig.PoseCamera.transform.forward;

        if (referenceForward.sqrMagnitude <= 0.0001f)
            referenceForward = Vector3.forward;

        Vector3 cameraPosition =
            worldPosition -
            referenceForward.normalized * Mathf.Max(0.1f, distance);

        rig.PoseCamera.transform.SetPositionAndRotation(
            cameraPosition,
            CreateLookRotation(
                worldPosition - cameraPosition,
                rig.PoseCamera.transform.rotation));

        interactionCameraActive = false;
        rig.SetLive(BattleCameraRigSlot.Pose);
    }

    public void Return()
    {
        ReturnInternal();
    }

    public void ReturnFromInteraction()
    {
        ReturnInternal();
    }

    public void Return(SkillCameraDefinition definition)
    {
        if (definition != null && definition.OverrideReturnBrainBlend)
        {
            ApplyBlendForNextTransition(
                definition.ReturnBlendStyle,
                definition.ReturnBlendTime);
        }

        ReturnInternal(false);
    }

    public void Return(SkillCameraShot shot)
    {
        if (shot != null && shot.OverrideBrainBlend)
        {
            ApplyBlendForNextTransition(
                shot.BlendStyle,
                shot.BlendTime);
        }

        ReturnInternal(false);
    }

    public void Return(
        CinemachineBlendDefinition.Styles blendStyle,
        float blendTime)
    {
        ApplyBlendForNextTransition(blendStyle, blendTime);
        ReturnInternal(false);
    }

    private void ReturnInternal(bool restoreBaseBlend = true)
    {
        CancelImpactPulse(restoreLens: true);
        StopInteractionFocus();
        groupBinder?.Clear();

        if (restoreBaseBlend)
            rig?.RestoreBaseBlend();

        if (rig == null || !rig.SetLive(BattleCameraRigSlot.Overview))
            return;

        Log("Return Overview");
    }

    public IEnumerator WaitUntilArrived(float timeout)
    {
        if (timeout <= 0f)
            yield break;

        float elapsed = 0f;

        // CinemachineBrain이 같은 프레임 LateUpdate에서 Blend를 시작할 수 있으므로
        // 최소 한 프레임 기다린 뒤 상태를 검사한다.
        yield return null;

        while (elapsed < timeout)
        {
            if (!IsMoving)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    public void PlayShake(BattleCameraShakeSettings settings)
    {
        if (settings == null)
        {
            Debug.LogWarning(
                "[BattleCameraDirector] Shake 실패: settings가 null입니다.",
                this);
            return;
        }

        settings.Sanitize();

        if (!settings.CanPlay)
            return;

        if (rig == null || rig.ImpulseSource == null)
        {
            Debug.LogWarning(
                "[BattleCameraDirector] CinemachineImpulseSource가 없습니다.",
                this);
            return;
        }

        float force = settings.GetSafeImpulseForce();
        rig.ImpulseSource.GenerateImpulseWithForce(force);

        Log($"Cinemachine Impulse / Force={force}");
    }


    /// <summary>
    /// 현재 활성 CinemachineCamera의 FOV에 짧은 펄스를 적용합니다.
    /// 기존 Shot의 위치, 회전, CameraPoint 및 Brain Blend는 변경하지 않습니다.
    /// </summary>
    public Coroutine StartImpactPulse(
        SkillCameraImpactPulse pulse)
    {
        if (pulse == null)
            return null;

        pulse.Sanitize();

        if (!pulse.CanPlay)
            return null;

        if (!EnsureRig("StartImpactPulse"))
            return null;

        CancelImpactPulse(restoreLens: true);

        impactPulseRoutine =
            StartCoroutine(
                PlayImpactPulseRoutine(
                    pulse));

        return impactPulseRoutine;
    }

    public void CancelImpactPulse(
        bool restoreLens = true)
    {
        if (impactPulseRoutine != null)
        {
            StopCoroutine(
                impactPulseRoutine);

            impactPulseRoutine = null;
        }

        if (restoreLens &&
            hasImpactPulseRestoreFov &&
            impactPulseCamera != null)
        {
            SetCameraFieldOfView(
                impactPulseCamera,
                impactPulseRestoreFov);
        }

        impactPulseCamera = null;
        impactPulseRestoreFov = 0f;
        hasImpactPulseRestoreFov = false;
    }

    private IEnumerator PlayImpactPulseRoutine(
        SkillCameraImpactPulse pulse)
    {
        if (pulse == null)
            yield break;

        if (pulse.StartDelay > 0f)
        {
            yield return WaitImpactTime(
                pulse.StartDelay,
                pulse.UseUnscaledTime);
        }

        CinemachineCamera camera =
            ResolveImpactCamera();

        if (camera == null)
        {
            impactPulseRoutine = null;
            yield break;
        }

        impactPulseCamera = camera;
        impactPulseRestoreFov =
            Mathf.Clamp(
                camera.Lens.FieldOfView,
                1f,
                179f);

        hasImpactPulseRestoreFov = true;

        float targetFov =
            Mathf.Clamp(
                impactPulseRestoreFov +
                pulse.FieldOfViewDelta,
                1f,
                179f);

        if (pulse.UseShake &&
            pulse.ShakeTiming ==
                SkillCameraImpactShakeTiming.OnPulseStart)
        {
            PlayShake(
                pulse.Shake);
        }

        if (Mathf.Abs(
                targetFov -
                impactPulseRestoreFov) > 0.001f)
        {
            yield return AnimateImpactFov(
                camera,
                impactPulseRestoreFov,
                targetFov,
                pulse.ZoomInDuration,
                pulse.ZoomInCurve,
                pulse.UseUnscaledTime);
        }

        if (pulse.UseShake &&
            pulse.ShakeTiming ==
                SkillCameraImpactShakeTiming.OnZoomPeak)
        {
            PlayShake(
                pulse.Shake);
        }

        if (pulse.HoldDuration > 0f)
        {
            yield return WaitImpactTime(
                pulse.HoldDuration,
                pulse.UseUnscaledTime);
        }

        if (Mathf.Abs(
                targetFov -
                impactPulseRestoreFov) > 0.001f)
        {
            yield return AnimateImpactFov(
                camera,
                targetFov,
                impactPulseRestoreFov,
                pulse.ZoomOutDuration,
                pulse.ZoomOutCurve,
                pulse.UseUnscaledTime);
        }

        if (impactPulseCamera == camera &&
            hasImpactPulseRestoreFov)
        {
            SetCameraFieldOfView(
                camera,
                impactPulseRestoreFov);
        }

        impactPulseCamera = null;
        impactPulseRestoreFov = 0f;
        hasImpactPulseRestoreFov = false;
        impactPulseRoutine = null;
    }

    private IEnumerator AnimateImpactFov(
        CinemachineCamera camera,
        float from,
        float to,
        float duration,
        AnimationCurve curve,
        bool useUnscaledTime)
    {
        if (camera == null)
            yield break;

        if (duration <= 0f)
        {
            SetCameraFieldOfView(
                camera,
                to);

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (camera == null)
                yield break;

            elapsed += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            float normalized =
                Mathf.Clamp01(
                    elapsed /
                    duration);

            float evaluated =
                curve != null
                    ? curve.Evaluate(normalized)
                    : normalized;

            SetCameraFieldOfView(
                camera,
                Mathf.LerpUnclamped(
                    from,
                    to,
                    evaluated));

            yield return null;
        }

        SetCameraFieldOfView(
            camera,
            to);
    }

    private static IEnumerator WaitImpactTime(
        float duration,
        bool useUnscaledTime)
    {
        if (duration <= 0f)
            yield break;

        if (useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(
                duration);

            yield break;
        }

        yield return new WaitForSeconds(
            duration);
    }

    private CinemachineCamera ResolveImpactCamera()
    {
        if (rig == null)
            return null;

        if (rig.ActiveCamera != null)
            return rig.ActiveCamera;

        if (rig.PoseCamera != null)
            return rig.PoseCamera;

        if (rig.GroupCamera != null)
            return rig.GroupCamera;

        return rig.OverviewCamera;
    }

    private static void SetCameraFieldOfView(
        CinemachineCamera camera,
        float fieldOfView)
    {
        if (camera == null)
            return;

        LensSettings lens =
            camera.Lens;

        lens.FieldOfView =
            Mathf.Clamp(
                fieldOfView,
                1f,
                179f);

        camera.Lens = lens;
    }

    public IEnumerator PlayShotsByTiming(
        BattleVisualRequest request,
        SkillCameraDefinition definition,
        SkillCameraShotTiming timing)
    {
        if (request == null || definition?.Shots == null)
            yield break;

        foreach (SkillCameraShot shot in definition.Shots)
        {
            if (shot == null || shot.Timing != timing)
                continue;

            yield return PlayShot(request, shot);
        }
    }

    public IEnumerator PlayShot(
        BattleVisualRequest request,
        SkillCameraShot shot)
    {
        if (request == null || shot == null || !EnsureRig("PlayShot"))
            yield break;

        StopInteractionFocus();

        if (shot.OverrideBrainBlend)
        {
            ApplyBlendForNextTransition(
                shot.BlendStyle,
                shot.BlendTime);
        }

        bool shotStarted = false;

        if (shot.UseSceneCameraPoint)
            shotStarted = PlaySceneCameraPoint(request, shot);

        if (!shotStarted)
        {
            switch (shot.ShotType)
            {
                case SkillCameraShotType.FocusBetween:
                case SkillCameraShotType.ClashWide:
                    shotStarted = PlayGroupShot(request, shot);
                    break;

                case SkillCameraShotType.AttackerClose:
                case SkillCameraShotType.AttackerOverShoulder:
                    shotStarted = PlayCharacterPoseShot(
                        request.Attacker,
                        request.Target,
                        shot);
                    break;

                case SkillCameraShotType.TargetClose:
                case SkillCameraShotType.TargetOverShoulder:
                case SkillCameraShotType.HitImpact:
                    shotStarted = PlayCharacterPoseShot(
                        request.Target,
                        request.Attacker,
                        shot);
                    break;

                case SkillCameraShotType.ReturnOverview:
                    Return(shot);
                    shotStarted = true;
                    break;
            }
        }

        if (!shotStarted)
        {
            Debug.LogWarning(
                $"[BattleCameraDirector] Camera Shot을 시작하지 못했습니다. " +
                $"Type={shot.ShotType}, Timing={shot.Timing}",
                this);
            yield break;
        }

        yield return WaitAndShake(shot);
    }

    private IEnumerator WaitAndShake(SkillCameraShot shot)
    {
        if (shot == null)
            yield break;

        if (shot.BlendWaitTime > 0f)
            yield return WaitUntilArrived(shot.BlendWaitTime);

        if (shot.UseShake)
            PlayShake(shot.Shake);

        if (shot.Duration > 0f)
            yield return new WaitForSeconds(shot.Duration);
    }

    private bool PlayGroupShot(
        BattleVisualRequest request,
        SkillCameraShot shot)
    {
        if (request == null || shot == null || rig?.GroupCamera == null)
            return false;

        Transform attackerTarget =
            BattleCameraTargetResolver.GetLookAtTarget(request.Attacker);

        Transform targetTarget =
            BattleCameraTargetResolver.GetLookAtTarget(request.Target);

        if (attackerTarget == null && targetTarget == null)
            return false;

        if (attackerTarget == targetTarget)
        {
            groupBinder?.BindOneTarget(
                attackerTarget,
                Mathf.Max(shot.AttackerWeight, shot.TargetWeight),
                Mathf.Max(shot.AttackerRadius, shot.TargetRadius));

            PlaceSingleTargetGroupCamera(
                attackerTarget,
                shot.SideDistance,
                shot.SideHeight,
                shot.LookAtHeight,
                shot.FlipSide);
        }
        else
        {
            groupBinder?.BindTwoTargets(
                attackerTarget,
                targetTarget,
                shot.AttackerWeight,
                shot.TargetWeight,
                shot.AttackerRadius,
                shot.TargetRadius);

            if (shot.UseSideViewPlacement)
            {
                PlaceGroupCameraSideView(
                    attackerTarget,
                    targetTarget,
                    shot.SideDistance,
                    shot.SideHeight,
                    shot.LookAtHeight,
                    shot.FlipSide);
            }
        }

        if (!rig.SetLive(BattleCameraRigSlot.Group))
            return false;

        Log(
            $"Group Shot / Timing={shot.Timing}, Type={shot.ShotType}, " +
            $"Attacker={GetCharacterName(request.Attacker)}, " +
            $"Target={GetCharacterName(request.Target)}");

        return true;
    }

    private bool PlayCharacterPoseShot(
        Character focusCharacter,
        Character lookCharacter,
        SkillCameraShot shot)
    {
        if (focusCharacter == null || shot == null || !EnsurePoseCamera("PlayCharacterPoseShot"))
            return false;

        Transform focusTarget =
            BattleCameraTargetResolver.GetLookAtTarget(focusCharacter);

        Transform lookTarget =
            BattleCameraTargetResolver.GetLookAtTarget(lookCharacter);

        if (focusTarget == null)
            return false;

        Vector3 lookAtPosition =
            focusTarget.position +
            focusTarget.TransformDirection(shot.LookAtOffset);

        Vector3 positionOffset =
            focusTarget.TransformDirection(shot.PositionOffset);

        if (positionOffset.sqrMagnitude <= 0.0001f)
        {
            Vector3 fallbackDirection =
                lookTarget != null
                    ? focusTarget.position - lookTarget.position
                    : -focusTarget.forward;

            if (fallbackDirection.sqrMagnitude <= 0.0001f)
                fallbackDirection = Vector3.back;

            positionOffset =
                fallbackDirection.normalized *
                Mathf.Max(1f, shot.FocusDistance);
        }

        SetPoseCamera(
            lookAtPosition + positionOffset,
            lookAtPosition,
            null);

        Log(
            $"Character Pose Shot / Character={GetCharacterName(focusCharacter)}, " +
            $"ShotType={shot.ShotType}");

        return true;
    }

    private bool PlaySceneCameraPoint(
        BattleVisualRequest request,
        SkillCameraShot shot)
    {
        if (request == null || shot == null || !EnsurePoseCamera("PlaySceneCameraPoint"))
            return false;

        if (!BattleCameraTargetResolver.TryResolveCameraPoint(
                request,
                shot.CameraPoint,
                out Transform cameraPoint,
                out string cameraFailure))
        {
            ReportCameraPointFallbackOnce(
                shot,
                cameraFailure);

            // PlayShot()이 ShotType에 맞는 Group/Character Pose Shot으로
            // 즉시 폴백한다.
            return false;
        }

        Transform lookAtPoint = null;

        if (shot.RotationMode == SkillCameraRotationMode.LookAtPoint)
        {
            if (!BattleCameraTargetResolver.TryResolveCameraPoint(
                    request,
                    shot.LookAtPoint,
                    out lookAtPoint,
                    out string lookFailure))
            {
                Character fallbackCharacter =
                    BattleCameraTargetResolver.ResolveOwner(
                        request,
                        shot.LookAtPoint != null
                            ? shot.LookAtPoint.Owner
                            : SkillCameraPointOwner.Target);

                lookAtPoint =
                    BattleCameraTargetResolver.GetLookAtTarget(
                        fallbackCharacter);

                if (lookAtPoint == null)
                {
                    Debug.LogWarning(
                        $"[BattleCameraDirector] LookAtPoint 찾기 실패 / {lookFailure}",
                        this);
                }
            }
        }

        Quaternion rotation =
            shot.RotationMode == SkillCameraRotationMode.CameraPointRotation ||
            lookAtPoint == null
                ? cameraPoint.rotation
                : CreateLookRotation(
                    lookAtPoint.position - cameraPoint.position,
                    cameraPoint.rotation);

        rig.PoseCamera.transform.SetPositionAndRotation(
            cameraPoint.position,
            rotation);

        if (!rig.SetLive(BattleCameraRigSlot.Pose))
            return false;

        Log(
            $"Scene CameraPoint Shot / CameraPoint={cameraPoint.name}");

        return true;
    }

    private void FocusSingleTargetWithGroupCamera(
        Transform target,
        float sideDistance,
        float height,
        float lookAtHeight)
    {
        if (target == null || rig?.GroupCamera == null)
            return;

        groupBinder?.BindOneTarget(target, 1f, 1f);

        PlaceSingleTargetGroupCamera(
            target,
            sideDistance,
            height,
            lookAtHeight,
            false);

        rig.SetLive(BattleCameraRigSlot.Group);
    }

    private void PlaceSingleTargetGroupCamera(
        Transform target,
        float sideDistance,
        float height,
        float lookAtHeight,
        bool flipSide)
    {
        if (target == null || rig?.GroupCamera == null)
            return;

        Vector3 side = target.right;

        if (side.sqrMagnitude <= 0.0001f)
            side = Vector3.right;

        if (flipSide)
            side = -side;

        Vector3 cameraPosition =
            target.position +
            side.normalized * Mathf.Max(0.1f, sideDistance) +
            Vector3.up * height;

        Vector3 lookAtPosition =
            target.position +
            Vector3.up * lookAtHeight;

        rig.GroupCamera.transform.SetPositionAndRotation(
            cameraPosition,
            CreateLookRotation(
                lookAtPosition - cameraPosition,
                rig.GroupCamera.transform.rotation));
    }

    private void PlaceGroupCameraSideView(
        Transform attackerTarget,
        Transform targetTarget,
        float sideDistance,
        float height,
        float lookAtHeight,
        bool flipSide)
    {
        if (rig?.GroupCamera == null || attackerTarget == null || targetTarget == null)
            return;

        Vector3 attackerPosition = attackerTarget.position;
        Vector3 targetPosition = targetTarget.position;
        Vector3 center = (attackerPosition + targetPosition) * 0.5f;

        Vector3 line = targetPosition - attackerPosition;
        line.y = 0f;

        if (line.sqrMagnitude <= 0.0001f)
            line = Vector3.right;

        line.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, line).normalized;

        if (flipSide)
            side = -side;

        Vector3 cameraPosition =
            center +
            side * Mathf.Max(0.1f, sideDistance) +
            Vector3.up * height;

        Vector3 lookAtPosition =
            center +
            Vector3.up * lookAtHeight;

        rig.GroupCamera.transform.SetPositionAndRotation(
            cameraPosition,
            CreateLookRotation(
                lookAtPosition - cameraPosition,
                rig.GroupCamera.transform.rotation));
    }

    private void SetPoseCamera(
        Vector3 position,
        Vector3 lookAtPosition,
        Quaternion? explicitRotation)
    {
        if (rig?.PoseCamera == null)
            return;

        Quaternion rotation =
            explicitRotation ??
            CreateLookRotation(
                lookAtPosition - position,
                rig.PoseCamera.transform.rotation);

        rig.PoseCamera.transform.SetPositionAndRotation(position, rotation);
        rig.SetLive(BattleCameraRigSlot.Pose);
    }

    private void BeginInteractionFocus()
    {
        if (!EnsurePoseCamera("BeginInteractionFocus"))
            return;

        Transform sourceTransform =
            Camera.main != null
                ? Camera.main.transform
                : rig.ActiveCamera != null
                    ? rig.ActiveCamera.transform
                    : rig.OverviewCamera != null
                        ? rig.OverviewCamera.transform
                        : rig.PoseCamera.transform;

        rig.PoseCamera.transform.SetPositionAndRotation(
            sourceTransform.position,
            sourceTransform.rotation);

        ApplyBlendForNextTransition(
            CinemachineBlendDefinition.Styles.Cut,
            0f);
        rig.SetLive(BattleCameraRigSlot.Pose);
    }

    private void UpdateInteractionCamera()
    {
        if (!interactionCameraActive || rig?.PoseCamera == null)
            return;

        Transform cameraTransform = rig.PoseCamera.transform;
        float deltaTime = Time.deltaTime;

        cameraTransform.position =
            Vector3.MoveTowards(
                cameraTransform.position,
                interactionTargetPosition,
                Mathf.Max(0.01f, interactionMoveSpeed) * deltaTime);

        cameraTransform.rotation =
            Quaternion.RotateTowards(
                cameraTransform.rotation,
                interactionTargetRotation,
                Mathf.Max(0.01f, interactionRotationSpeed) * deltaTime);

        if (HasInteractionCameraArrived(cameraTransform))
            interactionCameraActive = false;
    }

    private bool HasInteractionCameraArrived(Transform cameraTransform)
    {
        if (cameraTransform == null)
            return true;

        return
            Vector3.Distance(
                cameraTransform.position,
                interactionTargetPosition) <= interactionArriveDistance &&
            Quaternion.Angle(
                cameraTransform.rotation,
                interactionTargetRotation) <= interactionArriveAngle;
    }

    private void StopInteractionFocus()
    {
        interactionCameraActive = false;
    }

    private void ApplyBlendForNextTransition(
        CinemachineBlendDefinition.Styles style,
        float time)
    {
        if (rig?.Brain == null)
            return;

        CancelBlendRestore();
        rig.ApplyBrainBlend(style, time);

        restoreBlendRoutine =
            StartCoroutine(
                RestoreBaseBlendAfterTransitionStarts());

        Log(
            $"Brain Blend 변경 / Style={style}, Time={Mathf.Max(0f, time)}");
    }

    private IEnumerator RestoreBaseBlendAfterTransitionStarts()
    {
        int waitFrames = 0;

        while (waitFrames < 3)
        {
            waitFrames++;
            yield return null;

            if (rig == null || rig.Brain == null)
                yield break;

            if (rig.Brain.ActiveBlend != null)
                break;
        }

        rig?.RestoreBaseBlend();
        restoreBlendRoutine = null;
    }

    private void CancelBlendRestore()
    {
        if (restoreBlendRoutine == null)
            return;

        StopCoroutine(restoreBlendRoutine);
        restoreBlendRoutine = null;
    }

    private bool EnsureRig(string operation)
    {
        if (rig != null && rig.IsCoreReady)
            return true;

        ResolveReferences();

        if (rig != null && rig.IsCoreReady)
        {
            if (groupBinder == null)
                BuildGroupBinder();

            return true;
        }

        Debug.LogWarning(
            $"[BattleCameraDirector] {operation} 실패: BattleCinemachineRig가 준비되지 않았습니다.",
            this);
        return false;
    }

    private bool EnsurePoseCamera(string operation)
    {
        if (!EnsureRig(operation))
            return false;

        if (rig.PoseCamera != null)
            return true;

        Debug.LogWarning(
            $"[BattleCameraDirector] {operation} 실패: PoseCamera가 없습니다.",
            this);
        return false;
    }

    private static Quaternion CreateLookRotation(
        Vector3 direction,
        Quaternion fallback)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return fallback;

        return Quaternion.LookRotation(
            direction.normalized,
            Vector3.up);
    }

    private static string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        return character.Data != null
            ? character.Data.CharacterName
            : character.name;
    }

    private void ReportCameraPointFallbackOnce(
        SkillCameraShot shot,
        string failure)
    {
        string safeFailure =
            string.IsNullOrWhiteSpace(failure)
                ? "UNKNOWN"
                : failure;

        string key =
            $"{shot?.CameraPoint}:{shot?.ShotType}:{safeFailure}";

        if (!reportedCameraPointFallbacks.Add(key))
            return;

        Log(
            $"CameraPoint 폴백 / " +
            $"ShotType={shot?.ShotType}, " +
            $"{safeFailure}");
    }

    private void Log(string message)
    {
        if (logDebug)
            Debug.Log($"[BattleCameraDirector] {message}", this);
    }
}