using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 열광 레벨업으로 생성된 감정 증강 3택 Overlay.
///
/// - 열광 1/2/3에 맞는 Tier 1/2/3 증강 3개를 제시한다.
/// - 선택 전에는 다음 턴 진행이 BattleManager에서 잠긴다.
/// - Overlay는 잠시 최소화할 수 있지만 PendingOffer는 유지된다.
/// - 최소화 상태에서 START를 누르면 BattleManager가 다시 Overlay 표시를 요청한다.
/// - 선택 시 카드 강조/축소/페이드 애니메이션 후 실제 증강을 획득한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EmotionAugmentChoiceUI : MonoBehaviour
{
    private const string RootName = "EmotionAugmentChoiceOverlay";
    private const string MinimizedName = "EmotionAugmentPendingChip";

    [SerializeField] private BattleManager battleManager;
    [SerializeField, Min(0.05f)] private float showDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float chooseDuration = 0.46f;

    private EmotionAugmentManager boundManager;
    private TMP_Text templateText;

    private GameObject overlayRoot;
    private CanvasGroup overlayCanvasGroup;
    private RectTransform panelRect;
    private CanvasGroup panelCanvasGroup;
    private Image panelImage;
    private Outline panelOutline;
    private Image accent;
    private TMP_Text titleText;
    private TMP_Text subtitleText;
    private Button minimizeButton;

    private GameObject minimizedRoot;
    private Button reopenButton;
    private TMP_Text minimizedText;

    private ChoiceCard[] cards;
    private bool manuallyMinimized;
    private bool isChoosing;
    private bool blocksBattleArrows;

    /// <summary>
    /// 증강 3택 Overlay가 실제 화면을 덮고 있는 동안 전투 계획/합 화살표를 숨긴다.
    /// "잠시 닫기"로 최소화한 경우에는 false가 되어 전투 화면을 다시 읽을 수 있다.
    /// </summary>
    public static bool IsAnyChoiceOverlayBlockingBattleArrows { get; private set; }
    private Coroutine showRoutine;
    private Coroutine chooseRoutine;

    private sealed class ChoiceCard
    {
        public GameObject Root;
        public RectTransform Rect;
        public CanvasGroup CanvasGroup;
        public Button Button;
        public Image Background;
        public Outline Outline;
        public TMP_Text Title;
        public TMP_Text Body;
    }

    public void Configure(
        BattleManager manager,
        TMP_Text textTemplate = null)
    {
        battleManager = manager;

        if (textTemplate != null)
            templateText = textTemplate;

        EnsureView();
        TryBindManager();
        ShowPendingIfNeeded();
    }

    private void Awake()
    {
        ResolveManager();
        ResolveTemplateText();
        EnsureView();
    }

    private void OnEnable()
    {
        ResolveManager();
        TryBindManager();
        ShowPendingIfNeeded();
    }

    private void LateUpdate()
    {
        ResolveManager();
        TryBindManager();
        ShowPendingIfNeeded();
    }

    private void OnDisable()
    {
        SetBattleArrowBlock(false);
        StopAnimations();
        UnbindManager();
    }

    private void OnDestroy()
    {
        SetBattleArrowBlock(false);
        StopAnimations();
        UnbindManager();
    }

    private void ResolveManager()
    {
        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<BattleManager>(
                    FindObjectsInactive.Include);
        }
    }

    private void ResolveTemplateText()
    {
        if (templateText != null)
            return;

        templateText =
            GetComponentInChildren<TMP_Text>(true);

        if (templateText == null)
        {
            BattleTopStatusBarUI topBar =
                FindFirstObjectByType<BattleTopStatusBarUI>(
                    FindObjectsInactive.Include);

            templateText =
                topBar != null
                    ? topBar.GetComponentInChildren<TMP_Text>(true)
                    : null;
        }
    }

    private void TryBindManager()
    {
        EmotionAugmentManager manager =
            battleManager?.BattleContext?
                .Services?.EmotionAugmentManager;

        if (ReferenceEquals(manager, boundManager))
            return;

        UnbindManager();
        boundManager = manager;

        if (boundManager == null)
            return;

        boundManager.OfferCreated += HandleOfferCreated;
        boundManager.OfferPresentationRequested +=
            HandleOfferPresentationRequested;
        boundManager.AugmentAcquired += HandleAugmentAcquired;
    }

    private void UnbindManager()
    {
        if (boundManager == null)
            return;

        boundManager.OfferCreated -= HandleOfferCreated;
        boundManager.OfferPresentationRequested -=
            HandleOfferPresentationRequested;
        boundManager.AugmentAcquired -= HandleAugmentAcquired;
        boundManager = null;
    }

    private void HandleOfferCreated(
        EmotionAugmentOffer offer)
    {
        manuallyMinimized = false;

        if (!isChoosing)
            Show(offer, animate: true);
    }

    private void HandleOfferPresentationRequested(
        EmotionAugmentOffer offer)
    {
        manuallyMinimized = false;
        Show(offer, animate: true);
    }

    private void HandleAugmentAcquired(
        EmotionAugmentDefinition _)
    {
        if (isChoosing)
            return;

        if (boundManager?.PendingOffer == null)
            HideCompletely();
    }

    private void ShowPendingIfNeeded()
    {
        EmotionAugmentOffer pending =
            boundManager?.PendingOffer;

        if (pending == null)
        {
            SetMinimizedVisible(false);
            return;
        }

        if (manuallyMinimized)
        {
            UpdateMinimizedText(pending);
            SetMinimizedVisible(true);
            return;
        }

        if (overlayRoot != null && overlayRoot.activeSelf)
            return;

        Show(pending, animate: false);
    }

    private void EnsureView()
    {
        if (overlayRoot != null && cards != null)
            return;

        BuildOverlay();
        BuildMinimizedChip();
    }

    private void BuildOverlay()
    {
        Transform existing = transform.Find(RootName);
        if (existing != null)
            DestroyViewObject(existing.gameObject);

        GameObject overlay = new GameObject(
            RootName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));

        overlay.transform.SetParent(transform, false);

        RectTransform overlayRect =
            overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image dimmer = overlay.GetComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.80f);
        dimmer.raycastTarget = true;

        overlayCanvasGroup = overlay.GetComponent<CanvasGroup>();

        GameObject panel = new GameObject(
            "Panel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(CanvasGroup));

        panel.transform.SetParent(overlay.transform, false);

        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.17f, 0.16f);
        panelRect.anchorMax = new Vector2(0.83f, 0.84f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        panelCanvasGroup = panel.GetComponent<CanvasGroup>();

        panelImage = panel.GetComponent<Image>();
        panelImage.color =
            new Color(0.025f, 0.03f, 0.045f, 0.985f);

        panelOutline = panel.GetComponent<Outline>();
        panelOutline.effectDistance = new Vector2(2f, -2f);
        panelOutline.effectColor =
            new Color(0.72f, 0.76f, 0.84f, 0.75f);

        accent = CreateImage(
            panelRect,
            "EmotionAccent",
            new Vector2(0f, 0f),
            new Vector2(0.012f, 1f)).GetComponent<Image>();

        titleText = CreateText(
            panelRect,
            "Title",
            new Vector2(0.055f, 0.84f),
            new Vector2(0.945f, 0.96f),
            36f,
            TextAlignmentOptions.Center);

        subtitleText = CreateText(
            panelRect,
            "Subtitle",
            new Vector2(0.055f, 0.74f),
            new Vector2(0.945f, 0.845f),
            19f,
            TextAlignmentOptions.Center);

        minimizeButton = CreateButton(
            panelRect,
            "Minimize",
            new Vector2(0.83f, 0.90f),
            new Vector2(0.975f, 0.975f),
            "잠시 닫기",
            15f);

        minimizeButton.onClick.AddListener(
            MinimizeTemporarily);

        cards = new ChoiceCard[3];

        float width = 0.275f;
        float gap = 0.035f;
        float start = 0.055f;

        for (int i = 0; i < cards.Length; i++)
        {
            float xMin = start + i * (width + gap);
            float xMax = xMin + width;

            cards[i] = CreateCard(
                panelRect,
                i,
                new Vector2(xMin, 0.10f),
                new Vector2(xMax, 0.70f));
        }

        overlayRoot = overlay;
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(false);
    }

    private void BuildMinimizedChip()
    {
        Transform existing = transform.Find(MinimizedName);
        if (existing != null)
            DestroyViewObject(existing.gameObject);

        GameObject go = new GameObject(
            MinimizedName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(Button));

        go.transform.SetParent(transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.74f, 0.86f);
        rect.anchorMax = new Vector2(0.975f, 0.925f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.07f, 0.055f, 0.11f, 0.98f);

        Outline outline = go.GetComponent<Outline>();
        outline.effectDistance = new Vector2(2f, -2f);
        outline.effectColor = new Color(0.72f, 0.54f, 1f, 0.8f);

        reopenButton = go.GetComponent<Button>();
        reopenButton.targetGraphic = image;
        reopenButton.onClick.AddListener(ReopenPendingOffer);

        minimizedText = CreateText(
            rect,
            "Text",
            new Vector2(0.04f, 0.08f),
            new Vector2(0.96f, 0.92f),
            16f,
            TextAlignmentOptions.Center);

        minimizedRoot = go;
        minimizedRoot.SetActive(false);
    }

    private ChoiceCard CreateCard(
        Transform parent,
        int index,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject go = new GameObject(
            $"Choice_{index + 1}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(CanvasGroup),
            typeof(Button));

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.09f, 0.10f, 0.14f, 0.98f);

        Outline cardOutline = go.GetComponent<Outline>();
        cardOutline.effectDistance = new Vector2(1f, -1f);
        cardOutline.effectColor = new Color(1f, 1f, 1f, 0.25f);

        CanvasGroup canvasGroup = go.GetComponent<CanvasGroup>();

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.5f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TMP_Text title = CreateText(
            rect,
            "Title",
            new Vector2(0.07f, 0.68f),
            new Vector2(0.93f, 0.94f),
            24f,
            TextAlignmentOptions.Center);

        TMP_Text body = CreateText(
            rect,
            "Body",
            new Vector2(0.08f, 0.08f),
            new Vector2(0.92f, 0.67f),
            18f,
            TextAlignmentOptions.TopLeft);

        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Ellipsis;

        int captured = index;
        button.onClick.AddListener(() => Choose(captured));

        return new ChoiceCard
        {
            Root = go,
            Rect = rect,
            CanvasGroup = canvasGroup,
            Button = button,
            Background = image,
            Outline = cardOutline,
            Title = title,
            Body = body
        };
    }

    private void Show(
        EmotionAugmentOffer offer,
        bool animate)
    {
        if (offer == null || isChoosing)
            return;

        EnsureView();
        manuallyMinimized = false;
        SetMinimizedVisible(false);
        ConfigureOfferVisuals(offer);

        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();
        SetBattleArrowBlock(true);
        SetCardInteraction(true);

        if (showRoutine != null)
            StopCoroutine(showRoutine);

        if (animate && isActiveAndEnabled)
            showRoutine = StartCoroutine(AnimateShow());
        else
            ApplyShownState();
    }

    private void ConfigureOfferVisuals(
        EmotionAugmentOffer offer)
    {
        GameplayV5EmotionTheme theme =
            GameplayV5UiPresentation.GetEmotionTheme(
                offer.Emotion);

        if (accent != null)
            accent.color = theme.Accent;

        if (panelOutline != null)
        {
            Color color = theme.Accent;
            color.a = 0.78f;
            panelOutline.effectColor = color;
        }

        if (panelImage != null)
        {
            Color soft = theme.SoftAccent;
            panelImage.color = new Color(
                Mathf.Lerp(0.025f, soft.r, 0.14f),
                Mathf.Lerp(0.030f, soft.g, 0.14f),
                Mathf.Lerp(0.045f, soft.b, 0.14f),
                0.985f);
        }

        if (titleText != null)
        {
            titleText.text =
                $"열광 Lv.{offer.FervorLevel} · {theme.DisplayName}";
            titleText.color = theme.Accent;
        }

        if (subtitleText != null)
        {
            int pendingCount =
                boundManager?.PendingOfferCount ?? 1;

            subtitleText.text =
                $"Tier {Mathf.Clamp(offer.FervorLevel, 1, 3)} 증강 3택 · " +
                "하나를 선택해야 다음 턴을 시작할 수 있습니다" +
                (pendingCount > 1
                    ? $" · 연속 선택 {pendingCount}회 남음"
                    : string.Empty);
        }

        for (int i = 0; i < cards.Length; i++)
        {
            ChoiceCard card = cards[i];
            EmotionAugmentDefinition choice =
                i < offer.Choices.Count
                    ? offer.Choices[i]
                    : null;

            bool active = choice != null;
            card.Root.SetActive(active);

            if (!active)
                continue;

            card.Title.text =
                string.IsNullOrWhiteSpace(choice.DisplayName)
                    ? choice.name
                    : choice.DisplayName;

            card.Body.text =
                $"TIER {Mathf.Clamp(choice.Tier, 1, 3)}\n\n" +
                (string.IsNullOrWhiteSpace(choice.Description)
                    ? "설명이 없습니다."
                    : choice.Description.Trim());

            Color cardColor = theme.SoftAccent;
            card.Background.color = new Color(
                Mathf.Lerp(0.09f, cardColor.r, 0.28f),
                Mathf.Lerp(0.10f, cardColor.g, 0.28f),
                Mathf.Lerp(0.14f, cardColor.b, 0.28f),
                0.98f);

            Color edge = theme.Accent;
            edge.a = 0.50f;
            card.Outline.effectColor = edge;

            card.Rect.localScale = Vector3.one;
            card.CanvasGroup.alpha = 1f;
        }

        UpdateMinimizedText(offer);
    }

    private IEnumerator AnimateShow()
    {
        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 0f;

        if (panelCanvasGroup != null)
            panelCanvasGroup.alpha = 0f;

        if (panelRect != null)
            panelRect.localScale = Vector3.one * 0.88f;

        for (int i = 0; i < cards.Length; i++)
        {
            if (!cards[i].Root.activeSelf)
                continue;

            cards[i].Rect.localScale = Vector3.one * 0.90f;
            cards[i].CanvasGroup.alpha = 0f;
        }

        float duration = Mathf.Max(0.05f, showDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);

            if (overlayCanvasGroup != null)
                overlayCanvasGroup.alpha = Mathf.Clamp01(t * 1.35f);

            if (panelCanvasGroup != null)
                panelCanvasGroup.alpha = eased;

            if (panelRect != null)
            {
                float scale = Mathf.Lerp(0.88f, 1f, eased);
                panelRect.localScale = Vector3.one * scale;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                ChoiceCard card = cards[i];
                if (!card.Root.activeSelf)
                    continue;

                float local = Mathf.Clamp01(
                    (t - i * 0.10f) / 0.80f);
                float cardEase = EaseOutBack(local);
                card.CanvasGroup.alpha = local;
                card.Rect.localScale =
                    Vector3.one * Mathf.Lerp(0.90f, 1f, cardEase);
            }

            yield return null;
        }

        ApplyShownState();
        showRoutine = null;
    }

    private void ApplyShownState()
    {
        if (overlayCanvasGroup != null)
            overlayCanvasGroup.alpha = 1f;

        if (panelCanvasGroup != null)
            panelCanvasGroup.alpha = 1f;

        if (panelRect != null)
            panelRect.localScale = Vector3.one;

        if (cards == null)
            return;

        foreach (ChoiceCard card in cards)
        {
            if (card == null || !card.Root.activeSelf)
                continue;

            card.CanvasGroup.alpha = 1f;
            card.Rect.localScale = Vector3.one;
        }
    }

    private void Choose(int index)
    {
        if (boundManager == null ||
            isChoosing ||
            cards == null ||
            index < 0 ||
            index >= cards.Length ||
            !cards[index].Root.activeSelf)
        {
            return;
        }

        if (!isActiveAndEnabled)
            return;

        chooseRoutine = StartCoroutine(
            AnimateChoose(index));
    }

    private IEnumerator AnimateChoose(
        int index)
    {
        isChoosing = true;
        SetCardInteraction(false);

        if (minimizeButton != null)
            minimizeButton.interactable = false;

        GameplayV5EmotionTheme theme =
            GameplayV5UiPresentation.GetEmotionTheme(
                boundManager.PendingOffer?.Emotion ?? EmotionType.Awe);

        ChoiceCard selected = cards[index];
        Color selectedEdge = theme.Accent;
        selectedEdge.a = 1f;
        selected.Outline.effectColor = selectedEdge;

        float duration = Mathf.Max(0.05f, chooseDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float pulse =
                1f + Mathf.Sin(t * Mathf.PI) * 0.09f;
            selected.Rect.localScale = Vector3.one * pulse;

            for (int i = 0; i < cards.Length; i++)
            {
                if (i == index || !cards[i].Root.activeSelf)
                    continue;

                cards[i].CanvasGroup.alpha =
                    Mathf.Lerp(1f, 0.16f, EaseOutCubic(t));
                cards[i].Rect.localScale =
                    Vector3.one * Mathf.Lerp(1f, 0.94f, t);
            }

            yield return null;
        }

        bool chosen = boundManager != null &&
                      boundManager.Choose(index);

        isChoosing = false;
        chooseRoutine = null;

        if (minimizeButton != null)
            minimizeButton.interactable = true;

        if (!chosen)
        {
            ApplyShownState();
            SetCardInteraction(true);
            yield break;
        }

        EmotionAugmentOffer next =
            boundManager?.PendingOffer;

        if (next != null)
        {
            Show(next, animate: true);
            yield break;
        }

        HideCompletely();
        battleManager?.ContinueAfterEmotionAugmentChoice();
    }

    private void MinimizeTemporarily()
    {
        if (isChoosing ||
            boundManager?.PendingOffer == null)
        {
            return;
        }

        manuallyMinimized = true;
        HideOverlayOnly();
        UpdateMinimizedText(boundManager.PendingOffer);
        SetMinimizedVisible(true);
    }

    private void ReopenPendingOffer()
    {
        if (boundManager?.PendingOffer == null)
        {
            HideCompletely();
            return;
        }

        manuallyMinimized = false;
        Show(boundManager.PendingOffer, animate: true);
    }

    private void UpdateMinimizedText(
        EmotionAugmentOffer offer)
    {
        if (minimizedText == null || offer == null)
            return;

        GameplayV5EmotionTheme theme =
            GameplayV5UiPresentation.GetEmotionTheme(
                offer.Emotion);

        minimizedText.text =
            $"증강 선택 대기 · {theme.DisplayName} T{offer.FervorLevel} · 열기";
        minimizedText.color = theme.Accent;
    }

    private void HideOverlayOnly()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (overlayRoot != null)
            overlayRoot.SetActive(false);

        SetBattleArrowBlock(false);
    }

    private void SetBattleArrowBlock(bool block)
    {
        if (blocksBattleArrows == block)
            return;

        blocksBattleArrows = block;
        IsAnyChoiceOverlayBlockingBattleArrows = block;
    }

    private void HideCompletely()
    {
        manuallyMinimized = false;
        HideOverlayOnly();
        SetMinimizedVisible(false);
    }

    private void SetMinimizedVisible(
        bool visible)
    {
        if (minimizedRoot == null)
            return;

        minimizedRoot.SetActive(visible);

        if (visible)
            minimizedRoot.transform.SetAsLastSibling();
    }

    private void SetCardInteraction(
        bool enabled)
    {
        if (cards != null)
        {
            foreach (ChoiceCard card in cards)
            {
                if (card?.Button != null)
                    card.Button.interactable = enabled;
            }
        }

        if (reopenButton != null)
            reopenButton.interactable = enabled;
    }

    private void StopAnimations()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (chooseRoutine != null)
        {
            StopCoroutine(chooseRoutine);
            chooseRoutine = null;
        }

        isChoosing = false;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text text = go.GetComponent<TMP_Text>();
        CopyTextStyle(text, fontSize, alignment);
        return text;
    }

    private Button CreateButton(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string label,
        float fontSize)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(Button));

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.10f, 0.11f, 0.15f, 0.96f);

        Outline outline = go.GetComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.effectColor = new Color(1f, 1f, 1f, 0.18f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText(
            rect,
            "Text",
            new Vector2(0.05f, 0.05f),
            new Vector2(0.95f, 0.95f),
            fontSize,
            TextAlignmentOptions.Center);
        text.text = label;

        return button;
    }

    private static RectTransform CreateImage(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return rect;
    }

    private void CopyTextStyle(
        TMP_Text target,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        if (target == null)
            return;

        ResolveTemplateText();

        if (templateText != null)
        {
            target.font = templateText.font;
            target.fontSharedMaterial = templateText.fontSharedMaterial;
        }

        target.richText = true;
        target.enableAutoSizing = false;
        target.fontSize = fontSize;
        target.alignment = alignment;
        target.color = Color.white;
        target.raycastTarget = false;
        target.margin = Vector4.zero;
        target.overflowMode = TextOverflowModes.Overflow;
    }


    private static void DestroyViewObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
