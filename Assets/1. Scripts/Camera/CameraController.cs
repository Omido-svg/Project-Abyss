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
        CharacterFocus,
        DirectPose,
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
    
    private Character focusCharacter;
    private Vector3 characterPositionOffset;
    private Vector3 characterLookAtOffset;
    private float characterFocusDistance;

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
    
    private Vector3 directCameraPosition;
    private Vector3 directLookAtPosition;
    
    private Transform directCameraPoint;
    private Transform directLookAtPoint;
    private bool directUseCameraPointRotation;

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

            case CameraMode.CharacterFocus:
                SetTargetToCharacterFocus();
                break;

            case CameraMode.DirectPose:
                SetTargetToDirectPose();
                break;

            case CameraMode.Prestige:
                SetTargetToPrestige();
                break;
        }
    }
    
    public void FocusFromTransform(
        Transform cameraPoint,
        Transform lookAtPoint,
        bool useCameraPointRotation)
    {
        if (cameraPoint == null)
            return;

        currentMode = CameraMode.DirectPose;

        focusA = null;
        focusB = null;
        focusCharacter = null;

        directCameraPoint = cameraPoint;
        directLookAtPoint = lookAtPoint;
        directUseCameraPointRotation = useCameraPointRotation;

        RefreshTargetPose();
    }

    private void SetTargetToDirectPose()
    {
        if (directCameraPoint == null)
        {
            SetTargetToOverview();
            return;
        }

        targetPosition =
            directCameraPoint.position;

        if (directUseCameraPointRotation ||
            directLookAtPoint == null)
        {
            targetRotation =
                directCameraPoint.rotation;

            return;
        }

        Vector3 lookDirection =
            directLookAtPoint.position - targetPosition;

        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = directCameraPoint.forward;

        targetRotation =
            Quaternion.LookRotation(
                lookDirection.normalized,
                Vector3.up);
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
    
    private void SetTargetToCharacterFocus()
    {
        if (focusCharacter == null)
        {
            SetTargetToOverview();
            return;
        }

        Transform referenceTransform =
            GetCharacterViewTransform(focusCharacter);

        Vector3 basePoint =
            GetCharacterFocusPoint(focusCharacter);

        Vector3 lookAtPoint =
            basePoint +
            referenceTransform.TransformDirection(
                characterLookAtOffset);

        Vector3 offset =
            referenceTransform.TransformDirection(
                characterPositionOffset);

        if (offset.sqrMagnitude <= 0.0001f)
        {
            Vector3 fallbackForward =
                GetReferenceForward();

            if (fallbackForward.sqrMagnitude <= 0.0001f)
                fallbackForward = Vector3.forward;

            float distance =
                characterFocusDistance > 0f
                    ? characterFocusDistance
                    : focusDistance;

            offset =
                -fallbackForward.normalized * distance;
        }

        targetPosition =
            lookAtPoint + offset;

        Vector3 lookDirection =
            lookAtPoint - targetPosition;

        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = referenceTransform.forward;

        targetRotation =
            Quaternion.LookRotation(
                lookDirection.normalized,
                Vector3.up);
    }
    
    private Transform GetCharacterViewTransform(
        Character character)
    {
        if (character == null)
            return transform;

        CharacterView view =
            character.GetComponent<CharacterView>();

        if (view != null)
            return view.transform;

        return character.transform;
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
    
    public void Focus(Vector3 worldPosition)
    {
        currentMode = CameraMode.Focus;

        focusA = null;
        focusB = null;
        focusCharacter = null;

        currentFocusPoint = worldPosition;

        RefreshTargetPose();
    }

    public void Return()
    {
        currentMode = CameraMode.Overview;

        focusA = null;
        focusB = null;
        focusCharacter = null;

        directCameraPoint = null;
        directLookAtPoint = null;
        directUseCameraPointRotation = false;

        RefreshTargetPose();
    }

    public void ShowPrestige()
    {
        currentMode = CameraMode.Prestige;

        focusA = null;
        focusB = null;
        focusCharacter = null;

        RefreshTargetPose();
    }

    public void SnapToOverview()
    {
        currentMode = CameraMode.Overview;

        focusA = null;
        focusB = null;
        focusCharacter = null;

        RefreshTargetPose();
        SnapToTarget();
    }

    public void SnapToFocus(Vector3 worldPosition)
    {
        currentMode = CameraMode.Focus;

        focusA = null;
        focusB = null;
        focusCharacter = null;

        currentFocusPoint = worldPosition;

        RefreshTargetPose();
        SnapToTarget();
    }

    public void FocusBetween(Character a, Character b)
    {
        if (a == null || b == null)
            return;

        currentMode = CameraMode.FocusBetween;

        focusA = a;
        focusB = b;
        focusCharacter = null;

        directCameraPoint = null;
        directLookAtPoint = null;
        directUseCameraPointRotation = false;

        RefreshTargetPose();
    }

    public void FocusCharacter(
        Character character,
        Vector3 positionOffset,
        Vector3 lookAtOffset,
        float distance)
    {
        if (character == null)
            return;

        currentMode = CameraMode.CharacterFocus;

        focusA = null;
        focusB = null;

        focusCharacter = character;
        characterPositionOffset = positionOffset;
        characterLookAtOffset = lookAtOffset;
        characterFocusDistance = distance;

        RefreshTargetPose();
        
        directCameraPoint = null;
        directLookAtPoint = null;
        directUseCameraPointRotation = false;
    }
}