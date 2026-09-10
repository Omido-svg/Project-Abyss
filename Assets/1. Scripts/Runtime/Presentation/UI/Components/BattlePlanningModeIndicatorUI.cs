using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Planning 단계의 Tab 상위 입력 모드를 표시하는 비상호작용 UI.
///
/// CameraMovement  -> "전환"
/// ClashAssignment -> "지정"
///
/// BattlePlanningCameraController.ModeChanged를 구독하며,
/// 모드가 바뀔 때 하이라이트가 좌/우로 슬라이드하고 활성 텍스트가 짧게 팝된다.
/// Resolution/컷신 등 Planning 입력이 불가능한 동안에는 자동으로 숨는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlanningModeIndicatorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattlePlanningCameraController controller;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform highlightRect;
    [SerializeField] private Image highlightImage;
    [SerializeField] private TMP_Text cameraModeText;
    [SerializeField] private TMP_Text assignmentModeText;
    [SerializeField] private TMP_Text tabHintText;

    [Header("Labels")]
    [SerializeField] private string cameraModeLabel = "전환";
    [SerializeField] private string assignmentModeLabel = "지정";
    [SerializeField] private string tabHintLabel = "TAB";

    [Header("Highlight Layout")]
    [SerializeField] private Vector2 cameraHighlightPosition =
        new(-36f, 0f);
    [SerializeField] private Vector2 assignmentHighlightPosition =
        new(36f, 0f);

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float slideDuration = 0.16f;
    [SerializeField, Min(0.01f)] private float popDuration = 0.18f;
    [SerializeField, Range(1f, 1.35f)] private float activePopScale = 1.10f;
    [SerializeField, Min(0.1f)] private float visibilityFadeSpeed = 12f;

    [Header("Colors")]
    [SerializeField] private Color cameraHighlightColor =
        new(0.20f, 0.55f, 0.82f, 0.94f);
    [SerializeField] private Color assignmentHighlightColor =
        new(0.90f, 0.68f, 0.18f, 0.94f);
    [SerializeField] private Color activeTextColor = Color.white;
    [SerializeField] private Color inactiveTextColor =
        new(0.68f, 0.72f, 0.76f, 1f);

    private Coroutine animationRoutine;
    private BattlePlanningCameraController subscribedController;
    private BattlePlanningControlMode displayedMode =
        BattlePlanningControlMode.ClashAssignment;

    private void Awake()
    {
        ResolveReferences();
        ApplyStaticLabels();

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindController();
        ApplyStaticLabels();
        SnapToCurrentMode();
        RefreshVisibility(immediate: true);
    }

    private void OnDisable()
    {
        UnbindController();

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
    }

    private void Update()
    {
        // Scene 초기화 순서상 Controller가 늦게 준비되는 경우도 복구한다.
        if (controller == null)
        {
            ResolveReferences();
            BindController();
            SnapToCurrentMode();
        }
        else if (subscribedController != controller)
        {
            BindController();
            SnapToCurrentMode();
        }

        RefreshVisibility(immediate: false);
    }

    private void ResolveReferences()
    {
        if (controller == null)
        {
            controller = FindFirstObjectByType<BattlePlanningCameraController>(
                FindObjectsInactive.Include);
        }

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (highlightImage == null && highlightRect != null)
            highlightImage = highlightRect.GetComponent<Image>();
    }

    private void BindController()
    {
        if (subscribedController == controller)
            return;

        UnbindController();

        subscribedController = controller;
        if (subscribedController != null)
            subscribedController.ModeChanged += HandleModeChanged;
    }

    private void UnbindController()
    {
        if (subscribedController != null)
            subscribedController.ModeChanged -= HandleModeChanged;

        subscribedController = null;
    }

    private void ApplyStaticLabels()
    {
        if (cameraModeText != null)
            cameraModeText.text = cameraModeLabel;

        if (assignmentModeText != null)
            assignmentModeText.text = assignmentModeLabel;

        if (tabHintText != null)
            tabHintText.text = tabHintLabel;
    }

    private void SnapToCurrentMode()
    {
        BattlePlanningControlMode mode =
            controller != null
                ? controller.CurrentMode
                : BattlePlanningControlMode.ClashAssignment;

        displayedMode = mode;
        ApplyModeVisualImmediate(mode);
    }

    private void HandleModeChanged(BattlePlanningControlMode mode)
    {
        displayedMode = mode;

        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            ApplyModeVisualImmediate(mode);
            return;
        }

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine = StartCoroutine(AnimateModeChange(mode));
    }

    private IEnumerator AnimateModeChange(BattlePlanningControlMode mode)
    {
        if (highlightRect == null)
        {
            ApplyModeVisualImmediate(mode);
            animationRoutine = null;
            yield break;
        }

        Vector2 startPosition = highlightRect.anchoredPosition;
        Vector2 targetPosition = GetHighlightPosition(mode);

        Color startHighlightColor =
            highlightImage != null
                ? highlightImage.color
                : Color.white;
        Color targetHighlightColor = GetHighlightColor(mode);

        Color cameraStartColor =
            cameraModeText != null
                ? cameraModeText.color
                : inactiveTextColor;
        Color assignmentStartColor =
            assignmentModeText != null
                ? assignmentModeText.color
                : inactiveTextColor;

        Color cameraTargetColor =
            mode == BattlePlanningControlMode.CameraMovement
                ? activeTextColor
                : inactiveTextColor;
        Color assignmentTargetColor =
            mode == BattlePlanningControlMode.ClashAssignment
                ? activeTextColor
                : inactiveTextColor;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, slideDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            highlightRect.anchoredPosition =
                Vector2.LerpUnclamped(startPosition, targetPosition, eased);

            if (highlightImage != null)
            {
                highlightImage.color =
                    Color.LerpUnclamped(
                        startHighlightColor,
                        targetHighlightColor,
                        eased);
            }

            if (cameraModeText != null)
            {
                cameraModeText.color =
                    Color.LerpUnclamped(
                        cameraStartColor,
                        cameraTargetColor,
                        eased);
            }

            if (assignmentModeText != null)
            {
                assignmentModeText.color =
                    Color.LerpUnclamped(
                        assignmentStartColor,
                        assignmentTargetColor,
                        eased);
            }

            ApplyPopScale(mode, elapsed);
            yield return null;
        }

        ApplyModeVisualImmediate(mode);
        animationRoutine = null;
    }

    private void ApplyPopScale(
        BattlePlanningControlMode mode,
        float elapsed)
    {
        float duration = Mathf.Max(0.01f, popDuration);
        float t = Mathf.Clamp01(elapsed / duration);

        // 0 -> 1 -> 0 형태의 짧은 팝.
        float pulse = Mathf.Sin(t * Mathf.PI);
        float scale = Mathf.Lerp(1f, activePopScale, pulse);

        if (cameraModeText != null)
        {
            cameraModeText.rectTransform.localScale =
                mode == BattlePlanningControlMode.CameraMovement
                    ? Vector3.one * scale
                    : Vector3.one;
        }

        if (assignmentModeText != null)
        {
            assignmentModeText.rectTransform.localScale =
                mode == BattlePlanningControlMode.ClashAssignment
                    ? Vector3.one * scale
                    : Vector3.one;
        }
    }

    private void ApplyModeVisualImmediate(BattlePlanningControlMode mode)
    {
        displayedMode = mode;

        if (highlightRect != null)
            highlightRect.anchoredPosition = GetHighlightPosition(mode);

        if (highlightImage != null)
            highlightImage.color = GetHighlightColor(mode);

        if (cameraModeText != null)
        {
            cameraModeText.color =
                mode == BattlePlanningControlMode.CameraMovement
                    ? activeTextColor
                    : inactiveTextColor;
            cameraModeText.rectTransform.localScale = Vector3.one;
        }

        if (assignmentModeText != null)
        {
            assignmentModeText.color =
                mode == BattlePlanningControlMode.ClashAssignment
                    ? activeTextColor
                    : inactiveTextColor;
            assignmentModeText.rectTransform.localScale = Vector3.one;
        }
    }

    private Vector2 GetHighlightPosition(BattlePlanningControlMode mode)
    {
        return mode == BattlePlanningControlMode.CameraMovement
            ? cameraHighlightPosition
            : assignmentHighlightPosition;
    }

    private Color GetHighlightColor(BattlePlanningControlMode mode)
    {
        return mode == BattlePlanningControlMode.CameraMovement
            ? cameraHighlightColor
            : assignmentHighlightColor;
    }

    private void RefreshVisibility(bool immediate)
    {
        if (canvasGroup == null)
            return;

        bool shouldShow =
            controller != null &&
            controller.PlanningControlsAvailable;

        float target = shouldShow ? 1f : 0f;

        canvasGroup.alpha = immediate
            ? target
            : Mathf.MoveTowards(
                canvasGroup.alpha,
                target,
                visibilityFadeSpeed * Time.unscaledDeltaTime);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Scene authoring tool 전용 참조/레이아웃 주입.
    /// </summary>
    public void EditorAssignReferences(
        BattlePlanningCameraController planningController,
        CanvasGroup rootCanvasGroup,
        RectTransform highlight,
        Image highlightGraphic,
        TMP_Text cameraText,
        TMP_Text assignmentText,
        TMP_Text tabText,
        Vector2 cameraPosition,
        Vector2 assignmentPosition)
    {
        controller = planningController;
        canvasGroup = rootCanvasGroup;
        highlightRect = highlight;
        highlightImage = highlightGraphic;
        cameraModeText = cameraText;
        assignmentModeText = assignmentText;
        tabHintText = tabText;
        cameraHighlightPosition = cameraPosition;
        assignmentHighlightPosition = assignmentPosition;

        ApplyStaticLabels();
        ApplyModeVisualImmediate(
            controller != null
                ? controller.CurrentMode
                : BattlePlanningControlMode.ClashAssignment);
    }
#endif
}
