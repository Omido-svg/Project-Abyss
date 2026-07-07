using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    private enum CameraMode
    {
        Overview,
        Focus,
        FocusBetween,
        Prestige
    }

    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera overviewCamera;
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private CinemachineCamera prestigeCamera;

    [Header("Main Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Priority")]
    [SerializeField] private int livePriority = 100;
    [SerializeField] private int referencePriority = 0;

    [Header("Focus")]
    [SerializeField] private float focusDistance = 5f;

    // StatusPopup에서 이미 focusOffset을 더해서 넘기고 있으므로 기본값은 0 추천
    [SerializeField] private Vector3 extraFocusOffset = Vector3.zero;

    [Header("Move Speed - Runtime Editable")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Snap")]
    [SerializeField] private bool snapToOverviewOnStart = true;
    
    private Character focusA;
    private Character focusB;

    public static CameraController Instance { get; private set; }

    private CameraMode currentMode = CameraMode.Overview;

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private Vector3 currentFocusPoint;
    
    private Vector3 rigPosition;
    private Quaternion rigRotation;
    private bool rigInitialized;

    private Vector3 noisePositionOffset;
    private Vector3 noiseEulerOffset;

    public bool IsMoving
    {
        get
        {
            if (focusCamera == null)
                return false;

            float positionDistance =
                Vector3.Distance(
                    focusCamera.transform.position,
                    targetPosition);

            float rotationDistance =
                Quaternion.Angle(
                    focusCamera.transform.rotation,
                    targetRotation);

            return positionDistance > 0.01f ||
                   rotationDistance > 0.1f;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CameraController] 씬에 CameraController가 2개 이상 있습니다.");
        }

        Instance = this;

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        SetOnlyFocusCameraLive();

        SetTargetToOverview();

        if (snapToOverviewOnStart)
            SnapToTarget();
    }

    private void Update()
    {
        RefreshTargetPose();
        MoveLiveCamera();
    }

    public void Focus(Vector3 worldPosition)
    {
        currentMode = CameraMode.Focus;

        focusA = null;
        focusB = null;

        currentFocusPoint = worldPosition;

        RefreshTargetPose();
    }

    public void Return()
    {
        currentMode = CameraMode.Overview;

        focusA = null;
        focusB = null;

        RefreshTargetPose();
    }

    public void ShowPrestige()
    {
        currentMode = CameraMode.Prestige;

        RefreshTargetPose();
    }

    public void SnapToOverview()
    {
        currentMode = CameraMode.Overview;

        RefreshTargetPose();
        SnapToTarget();
    }

    public void SnapToFocus(Vector3 worldPosition)
    {
        currentMode = CameraMode.Focus;
        currentFocusPoint = worldPosition;

        RefreshTargetPose();
        SnapToTarget();
    }

    private void SetOnlyFocusCameraLive()
    {
        SetPriority(overviewCamera, referencePriority);
        SetPriority(prestigeCamera, referencePriority);

        // 이제 실제로 Main Camera를 움직이는 CinemachineCamera는 focusCamera 하나만 사용
        SetPriority(focusCamera, livePriority);
    }

    private void RefreshTargetPose()
    {
        switch (currentMode)
        {
            case CameraMode.Overview:
                SetTargetToOverview();
                break;

            case CameraMode.Focus:
                SetTargetToFocus();
                break;

            case CameraMode.FocusBetween:
                SetTargetToFocusBetween();
                break;

            case CameraMode.Prestige:
                SetTargetToPrestige();
                break;
        }
    }
    
    private void SetTargetToFocusBetween()
    {
        if (focusA == null || focusB == null)
        {
            SetTargetToOverview();
            return;
        }

        Vector3 aPoint =
            GetCharacterFocusPoint(focusA);

        Vector3 bPoint =
            GetCharacterFocusPoint(focusB);

        currentFocusPoint =
            (aPoint + bPoint) * 0.5f;

        SetTargetToFocus();
    }

    private void SetTargetToOverview()
    {
        if (overviewCamera == null)
            return;

        targetPosition =
            overviewCamera.transform.position;

        targetRotation =
            overviewCamera.transform.rotation;
    }

    private void SetTargetToPrestige()
    {
        if (prestigeCamera == null)
        {
            SetTargetToOverview();
            return;
        }

        targetPosition =
            prestigeCamera.transform.position;

        targetRotation =
            prestigeCamera.transform.rotation;
    }

    private void SetTargetToFocus()
    {
        Vector3 focusPoint =
            currentFocusPoint + extraFocusOffset;

        Vector3 cameraForward =
            GetReferenceForward();

        if (cameraForward.sqrMagnitude <= 0.0001f)
            cameraForward = Vector3.forward;

        targetPosition =
            focusPoint - cameraForward.normalized * focusDistance;

        targetRotation =
            Quaternion.LookRotation(
                focusPoint - targetPosition,
                Vector3.up);
    }

    private Vector3 GetReferenceForward()
    {
        // Overview 기준의 방향을 유지하면 Player/Enemy 이동 시 구도가 안정적임
        if (overviewCamera != null)
            return overviewCamera.transform.forward.normalized;

        if (mainCamera != null)
            return mainCamera.transform.forward.normalized;

        if (focusCamera != null)
            return focusCamera.transform.forward.normalized;

        return transform.forward.normalized;
    }

    private void MoveLiveCamera()
    {
        if (focusCamera == null)
            return;

        if (!rigInitialized)
        {
            rigPosition = focusCamera.transform.position;
            rigRotation = focusCamera.transform.rotation;
            rigInitialized = true;
        }

        float deltaTime =
            Time.deltaTime;

        float safeMoveSpeed =
            Mathf.Max(0.01f, moveSpeed);

        float safeRotationSpeed =
            Mathf.Max(0.01f, rotationSpeed);

        rigPosition =
            Vector3.MoveTowards(
                rigPosition,
                targetPosition,
                safeMoveSpeed * deltaTime);

        rigRotation =
            Quaternion.RotateTowards(
                rigRotation,
                targetRotation,
                safeRotationSpeed * deltaTime);

        ApplyRigPose();
    }

    private void SnapToTarget()
    {
        if (focusCamera == null)
            return;

        rigPosition = targetPosition;
        rigRotation = targetRotation;
        rigInitialized = true;

        ApplyRigPose();
    }

    private void SetPriority(
        CinemachineCamera camera,
        int priority)
    {
        if (camera == null)
            return;

        camera.Priority = priority;
    }
    
    public void SetNoiseOffset(
        Vector3 positionOffset,
        Vector3 eulerOffset)
    {
        noisePositionOffset = positionOffset;
        noiseEulerOffset = eulerOffset;

        ApplyRigPose();
    }

    private void ApplyRigPose()
    {
        if (focusCamera == null)
            return;

        focusCamera.transform.position =
            rigPosition + noisePositionOffset;

        focusCamera.transform.rotation =
            rigRotation *
            Quaternion.Euler(noiseEulerOffset);
    }
    
    public void FocusBetween(Character a, Character b)
    {
        if (a == null || b == null)
            return;

        currentMode = CameraMode.FocusBetween;

        focusA = a;
        focusB = b;

        RefreshTargetPose();
    }

    private Vector3 GetCharacterFocusPoint(Character character)
    {
        if (character == null)
            return Vector3.zero;

        CharacterView view =
            character.GetComponent<CharacterView>();

        if (view != null && view.LookAtPoint != null)
            return view.LookAtPoint.position;

        return character.transform.position + Vector3.up * 1.5f;
    }
    
    public IEnumerator WaitUntilArrived(float timeout = 1.5f)
    {
        float elapsed = 0f;

        while (IsMoving && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}