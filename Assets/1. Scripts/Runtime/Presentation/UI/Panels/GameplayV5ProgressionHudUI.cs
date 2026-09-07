using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gameplay v5 전용 상단 HUD.
/// 기존 TopStatusBar 오른쪽의 빈 공간에 현재 감정 / 고조 / 열광 / 감정 증강 상태를
/// 런타임으로 구성한다. Scene/Pefab 직렬화에 의존하지 않으므로 Play Mode에서 생성된
/// 하이어라키와 실제 Scene 에셋을 혼동하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayV5ProgressionHudUI : MonoBehaviour
{
    private const string RootName = "GameplayV5ProgressionHUD";

    [SerializeField] private BattleManager battleManager;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.1f;

    [Header("Exaltation Animation")]
    [SerializeField, Min(0f)] private float exaltationFillDuration = 0.42f;
    [SerializeField, Min(0f)] private float levelUpPulseDuration = 0.30f;
    [SerializeField, Range(1f, 1.5f)] private float levelUpPulseScale = 1.10f;

    private RectTransform root;
    private Image background;
    private Image accent;
    private Outline outline;
    private TMP_Text emotionText;
    private TMP_Text exaltationText;
    private TMP_Text fervorText;
    private RectTransform exaltationFillRect;
    private Image exaltationFill;
    private TMP_Text templateText;
    private float nextRefresh;

    private Coroutine exaltationAnimationRoutine;
    private int observedFervorLevel = -1;
    private int observedExaltation = -1;
    private float displayedExaltationRatio = -1f;
    private Color currentAccentColor = Color.white;

    public void Configure(
        BattleManager manager,
        TMP_Text textTemplate = null)
    {
        battleManager = manager;

        if (textTemplate != null)
            templateText = textTemplate;

        EnsureView();
        EnsureChoiceUi();
        Refresh();
    }

    private void Awake()
    {
        ResolveManager();
        ResolveTemplateText();
        EnsureView();
        EnsureChoiceUi();
    }

    private void OnEnable()
    {
        ResolveManager();
        EnsureView();
        EnsureChoiceUi();
        Refresh();
    }

    private void OnDisable()
    {
        if (exaltationAnimationRoutine != null)
        {
            StopCoroutine(exaltationAnimationRoutine);
            exaltationAnimationRoutine = null;
        }

        if (exaltationText != null)
            exaltationText.rectTransform.localScale = Vector3.one;

        if (exaltationFillRect != null)
            exaltationFillRect.localScale = Vector3.one;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh =
            Time.unscaledTime + refreshInterval;

        ResolveManager();
        EnsureView();
        EnsureChoiceUi();
        Refresh();
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
            GetComponent<BattleTopStatusBarUI>() != null
                ? GetComponentInChildren<TMP_Text>(true)
                : null;

        if (templateText == null)
        {
            Transform parent = transform.parent;
            templateText = parent != null
                ? parent.GetComponentInChildren<TMP_Text>(true)
                : null;
        }
    }

    private void EnsureView()
    {
        if (root != null)
            return;

        Transform host = transform.parent != null
            ? transform.parent
            : transform;

        Transform existing = host.Find(RootName);

        if (existing != null)
        {
            root = existing as RectTransform;
            CacheExistingReferences(existing);
            return;
        }

        GameObject rootGo = new GameObject(
            RootName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));

        rootGo.transform.SetParent(host, false);

        root = rootGo.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.765f, 0.900f);
        root.anchorMax = new Vector2(0.985f, 0.988f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);

        background = rootGo.GetComponent<Image>();
        background.color =
            new Color(0.025f, 0.03f, 0.045f, 0.94f);
        background.raycastTarget = false;

        outline = rootGo.GetComponent<Outline>();
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        outline.effectColor =
            new Color(0.55f, 0.62f, 0.75f, 0.62f);
        outline.useGraphicAlpha = true;

        RectTransform accentRect = CreateImage(
            root,
            "EmotionAccent",
            new Vector2(0f, 0f),
            new Vector2(0.018f, 1f));
        accent = accentRect.GetComponent<Image>();

        emotionText = CreateText(
            root,
            "EmotionText",
            new Vector2(0.055f, 0.60f),
            new Vector2(0.98f, 0.96f),
            22f,
            TextAlignmentOptions.Left);

        exaltationText = CreateText(
            root,
            "ExaltationText",
            new Vector2(0.055f, 0.30f),
            new Vector2(0.98f, 0.62f),
            18f,
            TextAlignmentOptions.Left);

        RectTransform track = CreateImage(
            root,
            "ExaltationTrack",
            new Vector2(0.055f, 0.20f),
            new Vector2(0.98f, 0.29f));

        Image trackImage = track.GetComponent<Image>();
        trackImage.color =
            new Color(1f, 1f, 1f, 0.10f);
        trackImage.raycastTarget = false;

        exaltationFillRect = CreateImage(
            track,
            "Fill",
            Vector2.zero,
            Vector2.one);
        exaltationFillRect.pivot = new Vector2(0f, 0.5f);
        exaltationFill = exaltationFillRect.GetComponent<Image>();
        exaltationFill.raycastTarget = false;

        fervorText = CreateText(
            root,
            "FervorText",
            new Vector2(0.055f, 0.00f),
            new Vector2(0.98f, 0.20f),
            16f,
            TextAlignmentOptions.Left);

        root.SetAsLastSibling();
    }

    private void CacheExistingReferences(
        Transform existing)
    {
        background = existing.GetComponent<Image>();
        outline = existing.GetComponent<Outline>();
        accent = existing.Find("EmotionAccent")?.GetComponent<Image>();
        emotionText = existing.Find("EmotionText")?.GetComponent<TMP_Text>();
        exaltationText = existing.Find("ExaltationText")?.GetComponent<TMP_Text>();
        fervorText = existing.Find("FervorText")?.GetComponent<TMP_Text>();

        Transform track = existing.Find("ExaltationTrack");
        exaltationFillRect =
            track?.Find("Fill") as RectTransform;
        exaltationFill =
            exaltationFillRect != null
                ? exaltationFillRect.GetComponent<Image>()
                : null;
    }

    private void EnsureChoiceUi()
    {
        if (battleManager == null)
            return;

        Transform uiRoot = FindUiRoot();
        if (uiRoot == null)
            return;

        EmotionAugmentChoiceUI choiceUi =
            uiRoot.GetComponent<EmotionAugmentChoiceUI>();

        if (choiceUi == null)
            choiceUi = uiRoot.gameObject.AddComponent<EmotionAugmentChoiceUI>();

        choiceUi.Configure(
            battleManager,
            templateText);
    }

    private Transform FindUiRoot()
    {
        Transform current = transform;

        while (current != null)
        {
            if (current.GetComponent<BattleScreenModeController>() != null ||
                current.name == "AbyssBattleUI")
            {
                return current;
            }

            current = current.parent;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null
            ? canvas.transform
            : transform.root;
    }

    public void Refresh()
    {
        if (root == null)
            return;

        BattleContext context =
            battleManager?.BattleContext;

        FervorManager fervor =
            context?.Services?.FervorManager;

        EmotionAugmentManager augmentManager =
            context?.Services?.EmotionAugmentManager;

        int level = fervor?.FervorLevel ?? 0;
        int exaltation = fervor?.Exaltation ?? 0;

        FervorRuleSettings rules =
            context?.Rules?.Fervor ??
            battleManager?.BattleRules?.Fervor;

        int maximumLevel =
            rules != null
                ? Mathf.Max(0, rules.MaximumLevel)
                : 3;

        int nextCost =
            level >= maximumLevel
                ? 0
                : Mathf.Max(
                    1,
                    rules?.GetCostForNextLevel(level) ?? 1);

        GameplayV5EmotionTheme theme;
        string emotionLabel;

        if (context?.SelectedEmotion.HasValue == true)
        {
            theme = GameplayV5UiPresentation.GetEmotionTheme(
                context.SelectedEmotion.Value);
            emotionLabel = theme.DisplayName;
        }
        else
        {
            theme = new GameplayV5EmotionTheme(
                "미선택",
                new Color(0.72f, 0.76f, 0.84f, 1f),
                new Color(0.28f, 0.30f, 0.36f, 1f));
            emotionLabel = "미선택";
        }

        currentAccentColor = theme.Accent;

        if (accent != null)
            accent.color = theme.Accent;

        if (exaltationFill != null &&
            exaltationAnimationRoutine == null)
        {
            exaltationFill.color = theme.Accent;
        }

        if (outline != null)
        {
            Color outlineColor = theme.Accent;
            outlineColor.a = 0.58f;
            outline.effectColor = outlineColor;
        }

        if (background != null)
        {
            Color soft = theme.SoftAccent;
            background.color = new Color(
                Mathf.Lerp(0.025f, soft.r, 0.16f),
                Mathf.Lerp(0.030f, soft.g, 0.16f),
                Mathf.Lerp(0.045f, soft.b, 0.16f),
                0.95f);
        }

        if (emotionText != null)
        {
            emotionText.text =
                $"<b>감정 · {emotionLabel}</b>";
            emotionText.color = theme.Accent;
        }

        float ratio =
            level >= maximumLevel
                ? 1f
                : nextCost <= 0
                    ? 0f
                    : Mathf.Clamp01((float)exaltation / nextCost);

        UpdateExaltationPresentation(
            level,
            exaltation,
            maximumLevel,
            nextCost,
            ratio,
            rules);

        if (fervorText != null)
        {
            int acquired =
                augmentManager?.Acquired?.Count ?? 0;

            string pips =
                GameplayV5UiPresentation.BuildFervorPips(
                    level,
                    maximumLevel);

            string pending =
                augmentManager?.PendingOffer != null
                    ? " · 증강 선택 대기"
                    : string.Empty;

            string dataState =
                context?.SelectedEmotion.HasValue == true &&
                context.EmotionAugmentCatalog == null
                    ? " · 증강 데이터 미연결"
                    : string.Empty;

            fervorText.text =
                $"열광 {pips}  Lv.{level} · 증강 {acquired}" +
                pending +
                dataState;
        }
    }

    private void UpdateExaltationPresentation(
        int level,
        int exaltation,
        int maximumLevel,
        int nextCost,
        float targetRatio,
        FervorRuleSettings rules)
    {
        if (observedFervorLevel < 0 ||
            observedExaltation < 0 ||
            displayedExaltationRatio < 0f)
        {
            observedFervorLevel = level;
            observedExaltation = exaltation;
            displayedExaltationRatio = targetRatio;
            SetExaltationFill(targetRatio);
            SetExaltationText(
                level,
                exaltation,
                maximumLevel,
                nextCost);
            return;
        }

        if (level == observedFervorLevel &&
            exaltation == observedExaltation)
        {
            if (exaltationAnimationRoutine == null)
            {
                SetExaltationFill(displayedExaltationRatio);
                SetExaltationText(
                    level,
                    exaltation,
                    maximumLevel,
                    nextCost);
            }

            return;
        }

        int previousLevel = observedFervorLevel;
        int previousExaltation = observedExaltation;

        observedFervorLevel = level;
        observedExaltation = exaltation;

        if (exaltationAnimationRoutine != null)
        {
            StopCoroutine(exaltationAnimationRoutine);
            exaltationAnimationRoutine = null;
        }

        exaltationAnimationRoutine =
            StartCoroutine(
                AnimateExaltationChange(
                    previousLevel,
                    previousExaltation,
                    level,
                    exaltation,
                    maximumLevel,
                    nextCost,
                    targetRatio,
                    rules));
    }

    private IEnumerator AnimateExaltationChange(
        int previousLevel,
        int previousExaltation,
        int level,
        int exaltation,
        int maximumLevel,
        int nextCost,
        float targetRatio,
        FervorRuleSettings rules)
    {
        bool leveledUp =
            level > previousLevel;

        if (leveledUp)
        {
            int previousCost =
                Mathf.Max(
                    1,
                    rules?.GetCostForNextLevel(previousLevel) ?? 1);

            // 고조가 문턱까지 차오르는 모습을 먼저 보여준 뒤 열광 레벨업을 강조한다.
            yield return AnimateExaltationFill(
                displayedExaltationRatio,
                1f,
                Mathf.Max(0.08f, exaltationFillDuration * 0.70f),
                previousExaltation,
                previousCost,
                previousLevel,
                maximumLevel,
                previousCost,
                " · 열광 상승!");

            yield return PlayLevelUpPulse();

            displayedExaltationRatio = 0f;
            SetExaltationFill(0f);

            // 소비 후 남은 고조가 다음 열광 게이지에 다시 차오른다.
            yield return AnimateExaltationFill(
                0f,
                targetRatio,
                Mathf.Max(0.08f, exaltationFillDuration * 0.55f),
                0,
                exaltation,
                level,
                maximumLevel,
                nextCost,
                string.Empty);
        }
        else
        {
            int gain =
                exaltation - previousExaltation;

            string suffix =
                gain > 0
                    ? $"  <color=#FFFFFF>+{gain}</color>"
                    : string.Empty;

            yield return AnimateExaltationFill(
                displayedExaltationRatio,
                targetRatio,
                exaltationFillDuration,
                previousExaltation,
                exaltation,
                level,
                maximumLevel,
                nextCost,
                suffix);
        }

        displayedExaltationRatio = targetRatio;
        SetExaltationFill(targetRatio);
        SetExaltationText(
            level,
            exaltation,
            maximumLevel,
            nextCost);

        exaltationAnimationRoutine = null;
    }

    private IEnumerator AnimateExaltationFill(
        float fromRatio,
        float toRatio,
        float duration,
        int fromValue,
        int toValue,
        int level,
        int maximumLevel,
        int cost,
        string suffix)
    {
        if (duration <= 0f)
        {
            displayedExaltationRatio = toRatio;
            SetExaltationFill(toRatio);
            SetAnimatedExaltationText(
                level,
                toValue,
                maximumLevel,
                cost,
                suffix);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration);
            float eased =
                1f - Mathf.Pow(1f - t, 3f);

            displayedExaltationRatio =
                Mathf.Lerp(fromRatio, toRatio, eased);
            int value =
                Mathf.RoundToInt(
                    Mathf.Lerp(fromValue, toValue, eased));

            SetExaltationFill(
                displayedExaltationRatio);
            SetAnimatedExaltationText(
                level,
                value,
                maximumLevel,
                cost,
                suffix);

            if (exaltationFill != null)
            {
                exaltationFill.color =
                    Color.Lerp(
                        currentAccentColor,
                        Color.white,
                        Mathf.Sin(Mathf.PI * t) * 0.28f);
            }

            yield return null;
        }

        displayedExaltationRatio = toRatio;
        SetExaltationFill(toRatio);

        if (exaltationFill != null)
            exaltationFill.color = currentAccentColor;
    }

    private IEnumerator PlayLevelUpPulse()
    {
        if (levelUpPulseDuration <= 0f)
            yield break;

        float elapsed = 0f;

        while (elapsed < levelUpPulseDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / levelUpPulseDuration);
            float pulse =
                1f +
                (levelUpPulseScale - 1f) *
                Mathf.Sin(Mathf.PI * t);

            if (exaltationText != null)
                exaltationText.rectTransform.localScale =
                    Vector3.one * pulse;

            if (exaltationFillRect != null)
            {
                exaltationFillRect.localScale =
                    new Vector3(
                        1f,
                        Mathf.Lerp(1f, pulse, 0.55f),
                        1f);
            }

            if (exaltationFill != null)
            {
                exaltationFill.color =
                    Color.Lerp(
                        currentAccentColor,
                        Color.white,
                        Mathf.Sin(Mathf.PI * t) * 0.72f);
            }

            yield return null;
        }

        if (exaltationText != null)
            exaltationText.rectTransform.localScale = Vector3.one;

        if (exaltationFillRect != null)
            exaltationFillRect.localScale = Vector3.one;

        if (exaltationFill != null)
            exaltationFill.color = currentAccentColor;
    }

    private void SetExaltationFill(
        float ratio)
    {
        if (exaltationFillRect == null)
            return;

        float clamped =
            Mathf.Clamp01(ratio);

        Vector2 max =
            exaltationFillRect.anchorMax;
        max.x = clamped;

        exaltationFillRect.anchorMin =
            Vector2.zero;
        exaltationFillRect.anchorMax =
            max;
        exaltationFillRect.offsetMin =
            Vector2.zero;
        exaltationFillRect.offsetMax =
            Vector2.zero;
    }

    private void SetExaltationText(
        int level,
        int exaltation,
        int maximumLevel,
        int nextCost)
    {
        SetAnimatedExaltationText(
            level,
            exaltation,
            maximumLevel,
            nextCost,
            string.Empty);
    }

    private void SetAnimatedExaltationText(
        int level,
        int exaltation,
        int maximumLevel,
        int cost,
        string suffix)
    {
        if (exaltationText == null)
            return;

        exaltationText.text =
            level >= maximumLevel
                ? $"고조 <b>{exaltation}</b> · 열광 MAX{suffix}"
                : $"고조 <b>{exaltation}/{Mathf.Max(1, cost)}</b> · 다음 열광{suffix}";
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
        image.color = Color.white;
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
            target.fontSharedMaterial =
                templateText.fontSharedMaterial;
        }

        target.richText = true;
        target.textWrappingMode = TextWrappingModes.NoWrap;
        target.enableAutoSizing = false;
        target.fontSize = fontSize;
        target.alignment = alignment;
        target.overflowMode = TextOverflowModes.Overflow;
        target.color = Color.white;
        target.raycastTarget = false;
        target.margin = Vector4.zero;
    }
}
