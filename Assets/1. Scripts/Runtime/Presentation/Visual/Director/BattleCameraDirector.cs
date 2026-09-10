using System.Collections;
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
                "전투 씬의 Cinemachine 3 Rig 구성을 확인하세요.",
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
                ? rig.OverviewReferenceForward
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
                    ? rig.OverviewReferenceForward
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
                ? rig.OverviewReferenceForward
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
            yield return
                BattlePlaybackSpeedController
                    .WaitForBattleUnscaledSeconds(duration);

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

    private void Log(string message)
    {
        if (logDebug)
            Debug.Log($"[BattleCameraDirector] {message}", this);
    }
}