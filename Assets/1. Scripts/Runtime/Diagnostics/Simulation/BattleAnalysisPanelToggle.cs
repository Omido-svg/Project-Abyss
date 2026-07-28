using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
public sealed class BattleAnalysisPanelToggle : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleButtonLabel;

    [Header("Visibility")]
    [SerializeField] private bool startHidden = true;
    [SerializeField] private bool enableF8Shortcut = true;

    [Header("Layout / Runtime Position")]
    [SerializeField] private Vector2 toggleButtonAnchoredPosition =
        new Vector2(22f, -24f);
    [SerializeField] private Vector2 panelAnchoredPosition =
        new Vector2(22f, 72f);
    [SerializeField] private bool allowToggleButtonDrag = true;
    [SerializeField] private bool rememberToggleButtonPosition = true;
    [SerializeField] private Vector2 toggleSafePadding =
        new Vector2(12f, 12f);
    [SerializeField] private string togglePositionPlayerPrefsKey =
        "ProjectAbyss.BattleAnalysis.TogglePosition";

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

    private bool togglePositionLoaded;
    private bool draggingToggle;
    private Vector2 dragStartPointerLocal;
    private Vector2 dragStartAnchoredPosition;
    private float suppressToggleClickUntil;

    public bool IsVisible => isVisible;
    public Vector2 ToggleButtonAnchoredPosition =>
        toggleButton != null
            ? ((RectTransform)toggleButton.transform).anchoredPosition
            : toggleButtonAnchoredPosition;

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

    /// <summary>
    /// 배치 분석 Runner가 별도의 DontDestroyOnLoad 제어 Canvas를 만들었을 때
    /// Scene에 붙어 있는 중복 분석 UI만 비활성화한다.
    /// 이 컴포넌트가 붙은 Battle UI Canvas 자체는 끄지 않는다.
    /// </summary>
    public void DisableRuntimeControls()
    {
        InitializeIfNeeded();
        KillTweens();
        UnbindButton();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (toggleButton != null)
            toggleButton.gameObject.SetActive(false);

        enabled = false;
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

        ApplyConfiguredLayout(loadSavedPosition: true);

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
                panelAnchoredPosition,
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
                "중단 [F9]",
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
                toggleButtonAnchoredPosition,
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
        toggleButton?.onClick.AddListener(HandleToggleButtonClicked);
    }

    private void UnbindButton()
    {
        toggleButton?.onClick.RemoveListener(HandleToggleButtonClicked);
    }

    private void HandleToggleButtonClicked()
    {
        if (Time.unscaledTime <= suppressToggleClickUntil)
            return;

        TogglePanel();
    }

    public void SetToggleButtonAnchoredPosition(
        Vector2 position,
        bool save = true)
    {
        toggleButtonAnchoredPosition = ClampTogglePosition(position);

        if (toggleButton != null)
        {
            RectTransform rect =
                toggleButton.transform as RectTransform;

            if (rect != null)
                rect.anchoredPosition = toggleButtonAnchoredPosition;
        }

        if (save)
            SaveTogglePosition();
    }

    public void ResetToggleButtonPosition()
    {
        togglePositionLoaded = true;

        if (rememberToggleButtonPosition &&
            !string.IsNullOrWhiteSpace(togglePositionPlayerPrefsKey))
        {
            PlayerPrefs.DeleteKey(togglePositionPlayerPrefsKey + ".x");
            PlayerPrefs.DeleteKey(togglePositionPlayerPrefsKey + ".y");
            PlayerPrefs.Save();
        }

        SetToggleButtonAnchoredPosition(
            new Vector2(22f, -24f),
            save: false);
    }

    private void ApplyConfiguredLayout(bool loadSavedPosition)
    {
        if (panelRoot != null)
        {
            RectTransform rect =
                panelRoot.transform as RectTransform;

            if (rect != null)
                rect.anchoredPosition = panelAnchoredPosition;
        }

        if (loadSavedPosition)
            LoadTogglePositionIfNeeded();

        SetToggleButtonAnchoredPosition(
            toggleButtonAnchoredPosition,
            save: false);
    }

    private void LoadTogglePositionIfNeeded()
    {
        if (togglePositionLoaded)
            return;

        togglePositionLoaded = true;

        if (!Application.isPlaying ||
            !rememberToggleButtonPosition ||
            string.IsNullOrWhiteSpace(togglePositionPlayerPrefsKey))
        {
            return;
        }

        string xKey = togglePositionPlayerPrefsKey + ".x";
        string yKey = togglePositionPlayerPrefsKey + ".y";

        if (!PlayerPrefs.HasKey(xKey) ||
            !PlayerPrefs.HasKey(yKey))
        {
            return;
        }

        toggleButtonAnchoredPosition =
            new Vector2(
                PlayerPrefs.GetFloat(
                    xKey,
                    toggleButtonAnchoredPosition.x),
                PlayerPrefs.GetFloat(
                    yKey,
                    toggleButtonAnchoredPosition.y));
    }

    private void SaveTogglePosition()
    {
        if (!Application.isPlaying ||
            !rememberToggleButtonPosition ||
            string.IsNullOrWhiteSpace(togglePositionPlayerPrefsKey))
        {
            return;
        }

        PlayerPrefs.SetFloat(
            togglePositionPlayerPrefsKey + ".x",
            toggleButtonAnchoredPosition.x);
        PlayerPrefs.SetFloat(
            togglePositionPlayerPrefsKey + ".y",
            toggleButtonAnchoredPosition.y);
        PlayerPrefs.Save();
    }

    private Vector2 ClampTogglePosition(Vector2 value)
    {
        RectTransform host = transform as RectTransform;
        RectTransform buttonRect =
            toggleButton != null
                ? toggleButton.transform as RectTransform
                : null;

        if (host == null ||
            buttonRect == null ||
            host.rect.width <= 1f ||
            host.rect.height <= 1f)
        {
            return value;
        }

        float minX = Mathf.Max(0f, toggleSafePadding.x);
        float maxX = Mathf.Max(
            minX,
            host.rect.width - buttonRect.rect.width -
            Mathf.Max(0f, toggleSafePadding.x));

        float maxY = -Mathf.Max(0f, toggleSafePadding.y);
        float minY = -Mathf.Max(
            Mathf.Max(0f, toggleSafePadding.y),
            host.rect.height - buttonRect.rect.height -
            Mathf.Max(0f, toggleSafePadding.y));

        return new Vector2(
            Mathf.Clamp(value.x, minX, maxX),
            Mathf.Clamp(value.y, minY, maxY));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!allowToggleButtonDrag ||
            !IsPointerOnToggleButton(eventData))
        {
            return;
        }

        RectTransform host = transform as RectTransform;
        RectTransform buttonRect =
            toggleButton?.transform as RectTransform;

        if (host == null || buttonRect == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                host,
                eventData.position,
                eventData.pressEventCamera,
                out dragStartPointerLocal))
        {
            return;
        }

        dragStartAnchoredPosition = buttonRect.anchoredPosition;
        draggingToggle = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!draggingToggle)
            return;

        RectTransform host = transform as RectTransform;

        if (host == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                host,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 pointerLocal))
        {
            return;
        }

        SetToggleButtonAnchoredPosition(
            dragStartAnchoredPosition +
            (pointerLocal - dragStartPointerLocal),
            save: false);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!draggingToggle)
            return;

        draggingToggle = false;
        suppressToggleClickUntil = Time.unscaledTime + 0.15f;
        SaveTogglePosition();
    }

    private bool IsPointerOnToggleButton(
        PointerEventData eventData)
    {
        if (toggleButton == null || eventData == null)
            return false;

        GameObject source =
            eventData.pointerPress ??
            eventData.pointerDrag ??
            eventData.pointerEnter;

        if (source == null)
            return false;

        Transform sourceTransform = source.transform;
        Transform toggleTransform = toggleButton.transform;

        return sourceTransform == toggleTransform ||
               sourceTransform.IsChildOf(toggleTransform);
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