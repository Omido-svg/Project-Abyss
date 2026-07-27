using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 동적 분석 패널을 접었다 펼치는 컨트롤러다.
/// Scene에서 패널이 삭제되어 있어도 Play Mode에서 최소 분석 UI를 자동 복원한다.
/// </summary>
[DefaultExecutionOrder(-1200)]
[DisallowMultipleComponent]
public sealed class BattleAnalysisPanelToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleButtonLabel;

    [Header("Visibility")]
    [SerializeField] private bool startHidden = true;
    [SerializeField] private bool enableF8Shortcut = true;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float animationDuration = 0.22f;
    [SerializeField, Min(0f)] private float hiddenOffset = 380f;
    [SerializeField] private Ease showEase = Ease.OutCubic;
    [SerializeField] private Ease hideEase = Ease.InCubic;

    [Header("Labels")]
    [SerializeField] private string showLabel = "분석 열기 [F8]";
    [SerializeField] private string hideLabel = "분석 접기 [F8]";

    private bool isVisible;
    private bool initialized;
    private RectTransform panelRect;
    private Vector2 visibleAnchoredPosition;
    private Tween movementTween;
    private Tween alphaTween;

    public bool IsVisible => isVisible;

    public void Configure(
        GameObject panel,
        CanvasGroup canvasGroup,
        Button button,
        TMP_Text buttonLabel,
        bool hiddenAtStart)
    {
        panelRoot = panel;
        panelCanvasGroup = canvasGroup;
        toggleButton = button;
        toggleButtonLabel = buttonLabel;
        startHidden = hiddenAtStart;
        initialized = false;

        if (Application.isPlaying)
            InitializeIfNeeded();
    }

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        InitializeIfNeeded();
        BindButton();
        ApplyVisibility(instant: true);
    }

    private void Start()
    {
        InitializeIfNeeded();
        SetPanelVisible(!startHidden, instant: true);
    }

    private void OnDisable()
    {
        UnbindButton();
        KillTweens();
    }

    private void OnDestroy()
    {
        UnbindButton();
        KillTweens();
    }

    private void Update()
    {
        if (enableF8Shortcut && WasF8Pressed())
            TogglePanel();
    }

    public void TogglePanel()
    {
        SetPanelVisible(!isVisible, instant: false);
    }

    public void ShowPanel()
    {
        SetPanelVisible(true, instant: false);
    }

    public void HidePanel()
    {
        SetPanelVisible(false, instant: false);
    }

    public void SetPanelVisible(bool visible)
    {
        SetPanelVisible(visible, instant: false);
    }

    private void SetPanelVisible(bool visible, bool instant)
    {
        InitializeIfNeeded();
        isVisible = visible;
        ApplyVisibility(instant);
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        ResolveReferences();

        if (Application.isPlaying &&
            (panelRoot == null || toggleButton == null))
        {
            BuildRuntimeFallbackUi();
            ResolveReferences();
        }

        panelRect = panelRoot != null
            ? panelRoot.GetComponent<RectTransform>()
            : null;

        if (panelRect != null)
            visibleAnchoredPosition = panelRect.anchoredPosition;

        isVisible = !startHidden;
        initialized = true;
        BindButton();
        ApplyVisibility(instant: true);
    }

    private void ApplyVisibility(bool instant)
    {
        if (panelRoot == null || panelCanvasGroup == null)
            return;

        if (!panelRoot.activeSelf)
            panelRoot.SetActive(true);

        if (isVisible)
            panelRoot.transform.SetAsLastSibling();

        KillTweens();

        Vector2 hiddenPosition =
            visibleAnchoredPosition + Vector2.left * hiddenOffset;
        Vector2 targetPosition =
            isVisible ? visibleAnchoredPosition : hiddenPosition;
        float targetAlpha = isVisible ? 1f : 0f;

        panelCanvasGroup.interactable = isVisible;
        panelCanvasGroup.blocksRaycasts = isVisible;

        if (instant || animationDuration <= 0f)
        {
            if (panelRect != null)
                panelRect.anchoredPosition = targetPosition;

            panelCanvasGroup.alpha = targetAlpha;
        }
        else
        {
            if (panelRect != null)
            {
                movementTween = panelRect
                    .DOAnchorPos(targetPosition, animationDuration)
                    .SetEase(isVisible ? showEase : hideEase)
                    .SetUpdate(true);
            }

            alphaTween = panelCanvasGroup
                .DOFade(targetAlpha, animationDuration * 0.8f)
                .SetEase(isVisible ? Ease.OutQuad : Ease.InQuad)
                .SetUpdate(true);
        }

        if (toggleButtonLabel != null)
            toggleButtonLabel.text = isVisible ? hideLabel : showLabel;
    }

    private void ResolveReferences()
    {
        if (panelRoot == null)
        {
            Transform found = transform.Find("BattleAnalysisPanel");
            if (found != null)
                panelRoot = found.gameObject;
        }

        if (panelCanvasGroup == null && panelRoot != null)
        {
            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null && Application.isPlaying)
                panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        if (toggleButton == null)
        {
            Transform found = transform.Find("BattleAnalysisToggleButton");
            if (found != null)
                toggleButton = found.GetComponent<Button>();
        }

        if (toggleButtonLabel == null && toggleButton != null)
            toggleButtonLabel = toggleButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void BuildRuntimeFallbackUi()
    {
        RectTransform host = transform as RectTransform;
        if (host == null)
            return;

        TMP_FontAsset sharedFont =
            GetComponentInChildren<TMP_Text>(true)?.font;

        if (panelRoot == null)
        {
            GameObject panelObject = CreateUiObject(
                "BattleAnalysisPanel",
                host);

            panelRoot = panelObject;
            panelRect = panelObject.GetComponent<RectTransform>();
            SetBottomLeftRect(
                panelRect,
                new Vector2(22f, 72f),
                new Vector2(360f, 260f));

            Image background = panelObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.055f, 0.09f, 0.94f);
            background.raycastTarget = true;

            Outline outline = panelObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.62f, 0.82f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            panelCanvasGroup = panelObject.AddComponent<CanvasGroup>();

            TMP_Text title = CreateText(
                "Title",
                panelRect,
                "동적 전투 분석",
                22f,
                TextAlignmentOptions.Left,
                sharedFont);
            SetTopStretchRect(title.rectTransform, 14f, 10f, 44f);
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.55f, 0.9f, 1f, 1f);

            Button winRate = CreateButton(
                "WinRateButton",
                panelRect,
                "승률 분석",
                new Vector2(14f, -58f),
                new Vector2(158f, 42f),
                sharedFont);

            Button damage = CreateButton(
                "DamageButton",
                panelRect,
                "피해 분석",
                new Vector2(188f, -58f),
                new Vector2(158f, 42f),
                sharedFont);

            Button stop = CreateButton(
                "StopButton",
                panelRect,
                "여기까지 저장하고 중단",
                new Vector2(14f, -110f),
                new Vector2(158f, 42f),
                sharedFont);

            Button folder = CreateButton(
                "OpenFolderButton",
                panelRect,
                "결과 폴더",
                new Vector2(188f, -110f),
                new Vector2(158f, 42f),
                sharedFont);

            TMP_Text status = CreateText(
                "StatusText",
                panelRect,
                "대기 중",
                16f,
                TextAlignmentOptions.TopLeft,
                sharedFont);
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 14f);
            statusRect.sizeDelta = new Vector2(-28f, 82f);
            status.textWrappingMode =
                TextWrappingModes.Normal;
            status.overflowMode = TextOverflowModes.Overflow;
            status.color = new Color(0.86f, 0.92f, 0.98f, 1f);

            BattleAnalysisDebugPanel debugPanel =
                panelObject.AddComponent<BattleAnalysisDebugPanel>();
            debugPanel.Configure(
                winRate,
                damage,
                stop,
                folder,
                status);

            panelObject.transform.SetAsLastSibling();
        }

        if (toggleButton == null)
        {
            toggleButton = CreateButton(
                "BattleAnalysisToggleButton",
                host,
                showLabel,
                new Vector2(22f, 22f),
                new Vector2(154f, 38f),
                sharedFont);

            toggleButtonLabel =
                toggleButton.GetComponentInChildren<TMP_Text>(true);

            toggleButton.transform.SetAsLastSibling();
        }
    }

    private static GameObject CreateUiObject(
        string objectName,
        Transform parent)
    {
        GameObject result = new(
            objectName,
            typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment,
        TMP_FontAsset font)
    {
        GameObject objectValue = CreateUiObject(objectName, parent);
        TextMeshProUGUI text = objectValue.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableAutoSizing = false;
        text.richText = true;
        text.color = Color.white;
        if (font != null)
            text.font = font;
        return text;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        string labelValue,
        Vector2 anchoredPosition,
        Vector2 size,
        TMP_FontAsset font)
    {
        GameObject objectValue = CreateUiObject(objectName, parent);
        RectTransform rect = objectValue.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = objectValue.AddComponent<Image>();
        image.color = new Color(0.08f, 0.18f, 0.28f, 0.98f);

        Button button = objectValue.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.76f, 0.92f, 1f, 1f);
        colors.pressedColor = new Color(0.58f, 0.8f, 0.92f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TMP_Text label = CreateText(
            "Label",
            rect,
            labelValue,
            16f,
            TextAlignmentOptions.Center,
            font);
        Stretch(label.rectTransform, 6f);
        label.fontStyle = FontStyles.Bold;

        return button;
    }

    private static void SetBottomLeftRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetTopStretchRect(
        RectTransform rect,
        float left,
        float right,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, -8f);
        rect.sizeDelta = new Vector2(-(left + right), height);
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private void BindButton()
    {
        UnbindButton();
        toggleButton?.onClick.AddListener(TogglePanel);
    }

    private void UnbindButton()
    {
        toggleButton?.onClick.RemoveListener(TogglePanel);
    }

    private void KillTweens()
    {
        movementTween?.Kill();
        alphaTween?.Kill();
        movementTween = null;
        alphaTween = null;
    }

    private static bool WasF8Pressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.f8Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F8);
#else
        return false;
#endif
    }
}