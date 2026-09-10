using System;
using UnityEngine;

/// <summary>
/// 전투 Planning 단계의 상위 입력 모드.
///
/// ClashAssignment:
/// - 기존 전투 UI/포인터 입력 사용
/// - 커서 표시
///
/// CameraMovement:
/// - Tab으로 진입/복귀
/// - WASD 이동, E 상승, Q 하강
/// - Mouse 이동으로 Yaw/Pitch 회전
/// - R로 기존 Overview Home Pose 복귀
/// - 전투 UI와 월드 Planning 입력 차단
///
/// Resolution/Cutscene 중에는 항상 비활성화되며 Tab/R을 받지 않는다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-250)]
public sealed class BattlePlanningCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleCinemachineRig cameraRig;
    [SerializeField] private BattlePlanningCameraBounds movementBounds;
    [SerializeField] private BattleScreenModeController screenModeController;
    [SerializeField] private CanvasGroup battleUiCanvasGroup;

    [Header("Mode")]
    [SerializeField] private BattlePlanningControlMode initialMode =
        BattlePlanningControlMode.ClashAssignment;

    [SerializeField] private KeyCode toggleModeKey = KeyCode.Tab;
    [SerializeField] private KeyCode resetOverviewKey = KeyCode.R;

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 5f;
    [SerializeField, Min(0.01f)] private float mouseLookSensitivity = 2.2f;
    [SerializeField, Range(-89f, 0f)] private float minimumPitch = -80f;
    [SerializeField, Range(0f, 89f)] private float maximumPitch = 80f;

    [Header("Diagnostics")]
    [SerializeField] private bool logModeChanges = false;

    private BattlePlanningControlMode currentMode;
    private float yaw;
    private float pitch;

    private bool uiInteractableCaptured;
    private bool previousUiInteractable = true;

    public static BattlePlanningCameraController Instance
    {
        get;
        private set;
    }

    public static bool IsCameraMovementActive =>
        Instance != null &&
        Instance.currentMode == BattlePlanningControlMode.CameraMovement;

    public BattlePlanningControlMode CurrentMode => currentMode;

    /// <summary>
    /// 현재 Tab 기반 Planning 상위 입력 전환을 사용할 수 있는지 UI가 조회한다.
    /// Resolution/Cutscene/증강 선택/전투 종료 중에는 false다.
    /// </summary>
    public bool PlanningControlsAvailable => CanUsePlanningControls();

    public event Action<BattlePlanningControlMode> ModeChanged;

#if UNITY_EDITOR
    /// <summary>
    /// Scene authoring tool 전용 참조 주입.
    /// 런타임 동작에는 사용하지 않는다.
    /// </summary>
    public void EditorAssignReferences(
        BattleManager manager,
        BattleCinemachineRig rig,
        BattlePlanningCameraBounds bounds,
        BattleScreenModeController screenMode,
        CanvasGroup uiCanvasGroup)
    {
        battleManager = manager;
        cameraRig = rig;
        movementBounds = bounds;
        screenModeController = screenMode;
        battleUiCanvasGroup = uiCanvasGroup;
    }
#endif

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveReferences();

        currentMode = BattlePlanningControlMode.ClashAssignment;
        SyncLookAnglesFromOverview();
        ApplyAssignmentCursor();

        if (initialMode == BattlePlanningControlMode.CameraMovement &&
            CanUsePlanningControls())
        {
            SetMode(BattlePlanningControlMode.CameraMovement);
        }
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        ResolveReferences();
        BattlePresentationInteractionLock.LockChanged +=
            HandlePresentationLockChanged;
    }

    private void OnDisable()
    {
        BattlePresentationInteractionLock.LockChanged -=
            HandlePresentationLockChanged;

        ForceAssignmentMode();

        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            return;

        if (currentMode == BattlePlanningControlMode.CameraMovement)
            ApplyMovementCursor();
        else
            ApplyAssignmentCursor();
    }

    private void Update()
    {
        ResolveReferences();

        if (!CanUsePlanningControls())
        {
            if (currentMode != BattlePlanningControlMode.ClashAssignment)
                ForceAssignmentMode();

            return;
        }

        if (Input.GetKeyDown(toggleModeKey))
        {
            ToggleMode();
            return;
        }

        if (currentMode != BattlePlanningControlMode.CameraMovement)
            return;

        if (Input.GetKeyDown(resetOverviewKey))
            ResetOverviewToHome();

        UpdateCameraMovement();
    }

    public void ToggleMode()
    {
        if (!CanUsePlanningControls())
            return;

        SetMode(
            currentMode == BattlePlanningControlMode.CameraMovement
                ? BattlePlanningControlMode.ClashAssignment
                : BattlePlanningControlMode.CameraMovement);
    }

    public void SetMode(BattlePlanningControlMode mode)
    {
        if (mode == BattlePlanningControlMode.CameraMovement &&
            !CanUsePlanningControls())
        {
            mode = BattlePlanningControlMode.ClashAssignment;
        }

        if (currentMode == mode)
        {
            ApplyModeState(mode);
            return;
        }

        currentMode = mode;
        ApplyModeState(mode);

        if (logModeChanges)
        {
            Debug.Log(
                $"[BattlePlanningCamera] Mode={currentMode}",
                this);
        }

        ModeChanged?.Invoke(currentMode);
    }

    public void ResetOverviewToHome()
    {
        ResolveReferences();

        if (!CanUsePlanningControls() || cameraRig == null)
            return;

        cameraRig.ResetOverviewPoseToHome();
        cameraRig.SetLive(BattleCameraRigSlot.Overview);
        SyncLookAnglesFromOverview();
    }

    private void ForceAssignmentMode()
    {
        bool changed =
            currentMode != BattlePlanningControlMode.ClashAssignment;

        currentMode = BattlePlanningControlMode.ClashAssignment;
        ApplyAssignmentUiState();
        ApplyAssignmentCursor();

        if (changed)
            ModeChanged?.Invoke(currentMode);
    }

    private void ApplyModeState(BattlePlanningControlMode mode)
    {
        if (mode == BattlePlanningControlMode.CameraMovement)
        {
            cameraRig?.SetLive(BattleCameraRigSlot.Overview);
            SyncLookAnglesFromOverview();
            ApplyMovementUiState();
            ApplyMovementCursor();
        }
        else
        {
            ApplyAssignmentUiState();
            ApplyAssignmentCursor();
        }
    }

    private void ApplyMovementUiState()
    {
        ResolveBattleUiCanvasGroup();

        if (battleUiCanvasGroup == null)
            return;

        if (!uiInteractableCaptured)
        {
            previousUiInteractable = battleUiCanvasGroup.interactable;
            uiInteractableCaptured = true;
        }

        // Graphic은 그대로 보여주되 모든 UGUI Selectable 입력을 막는다.
        battleUiCanvasGroup.interactable = false;
    }

    private void ApplyAssignmentUiState()
    {
        if (battleUiCanvasGroup == null)
            return;

        if (uiInteractableCaptured)
            battleUiCanvasGroup.interactable = previousUiInteractable;
        else
            battleUiCanvasGroup.interactable = true;

        uiInteractableCaptured = false;
    }

    private static void ApplyMovementCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void ApplyAssignmentCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void UpdateCameraMovement()
    {
        if (cameraRig?.OverviewCamera == null)
            return;

        Transform cameraTransform =
            cameraRig.OverviewCamera.transform;

        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        yaw += mouseX * mouseLookSensitivity;
        pitch -= mouseY * mouseLookSensitivity;
        pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);

        cameraTransform.rotation =
            Quaternion.Euler(pitch, yaw, 0f);

        Vector3 flatForward =
            Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 flatRight =
            Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        Vector3 direction = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
            direction += flatForward;

        if (Input.GetKey(KeyCode.S))
            direction -= flatForward;

        if (Input.GetKey(KeyCode.D))
            direction += flatRight;

        if (Input.GetKey(KeyCode.A))
            direction -= flatRight;

        // Unity Scene View Flythrough 방식: E = 위, Q = 아래.
        if (Input.GetKey(KeyCode.E))
            direction += Vector3.up;

        if (Input.GetKey(KeyCode.Q))
            direction += Vector3.down;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.Normalize();

        Vector3 nextPosition =
            cameraTransform.position +
            direction * moveSpeed * Time.unscaledDeltaTime;

        if (movementBounds != null)
            nextPosition = movementBounds.ClampWorldPoint(nextPosition);

        cameraTransform.position = nextPosition;
    }

    private void SyncLookAnglesFromOverview()
    {
        if (cameraRig?.OverviewCamera == null)
            return;

        Vector3 euler =
            cameraRig.OverviewCamera.transform.eulerAngles;

        yaw = NormalizeSignedAngle(euler.y);
        pitch = Mathf.Clamp(
            NormalizeSignedAngle(euler.x),
            minimumPitch,
            maximumPitch);
    }

    private bool CanUsePlanningControls()
    {
        if (battleManager == null ||
            !battleManager.IsInitialized ||
            battleManager.IsEndingOrEnded ||
            battleManager.TurnManager == null ||
            !battleManager.TurnManager.IsBattleRunning ||
            battleManager.TurnManager.IsResolving ||
            battleManager.IsWaitingForEmotionAugmentChoice ||
            BattlePresentationInteractionLock.IsLocked)
        {
            return false;
        }

        return cameraRig?.OverviewCamera != null;
    }

    private void HandlePresentationLockChanged(bool locked)
    {
        // Resolution UI가 Lock을 거는 바로 그 프레임에 커서를 반환하고
        // Tab/R 카메라 조작권을 해제한다.
        if (locked)
            ForceAssignmentMode();
    }

    private void ResolveReferences()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (cameraRig == null)
            cameraRig = FindFirstObjectByType<BattleCinemachineRig>();

        if (movementBounds == null)
        {
            movementBounds =
                FindFirstObjectByType<BattlePlanningCameraBounds>(
                    FindObjectsInactive.Include);
        }

        if (screenModeController == null)
        {
            screenModeController =
                FindFirstObjectByType<BattleScreenModeController>(
                    FindObjectsInactive.Include);
        }

        ResolveBattleUiCanvasGroup();
    }

    private void ResolveBattleUiCanvasGroup()
    {
        if (battleUiCanvasGroup != null)
            return;

        if (screenModeController == null)
            return;

        GameObject root = screenModeController.gameObject;

        battleUiCanvasGroup = root.GetComponent<CanvasGroup>();

        if (battleUiCanvasGroup == null && Application.isPlaying)
            battleUiCanvasGroup = root.AddComponent<CanvasGroup>();
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle %= 360f;

        if (angle > 180f)
            angle -= 360f;
        else if (angle < -180f)
            angle += 360f;

        return angle;
    }
}
