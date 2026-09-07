using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleStartButtonStateUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private Button startButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Image readyGlowImage;

    [Header("Unavailable")]
    [SerializeField, Range(0f, 1f)]
    private float unavailableBrightness = 0.23f;

    [SerializeField, Range(0f, 1f)]
    private float unavailableLabelAlpha = 0.56f;

    [Header("Ready Sparkle")]
    [SerializeField] private Color readyGlowColor =
        new(1f, 0.78f, 0.16f, 1f);

    [SerializeField, Min(0.1f)]
    private float sparkleSpeed = 3.2f;

    [SerializeField, Range(0f, 0.25f)]
    private float brightenAmount = 0.18f;

    [SerializeField, Range(0f, 0.15f)]
    private float scalePulseAmount = 0.035f;

    [SerializeField, Range(0f, 1f)]
    private float minimumGlowAlpha = 0.12f;

    [SerializeField, Range(0f, 1f)]
    private float maximumGlowAlpha = 0.62f;

    private Color baseBackgroundColor = Color.white;
    private Color baseLabelColor = Color.white;
    private string baseLabelText = "START";
    private Vector3 baseScale = Vector3.one;
    private bool baseCaptured;
    private bool? lastReadyState;

    public bool IsReady =>
        EvaluateReady();

    public void Configure(
        BattleManager manager,
        Button button,
        Image background,
        TMP_Text label,
        Image glow)
    {
        battleManager = manager;
        startButton = button;
        backgroundImage = background;
        labelText = label;
        readyGlowImage = glow;

        ResolveReferences();
        CaptureBaseVisuals();
        ConfigureButtonColors();
        RefreshImmediate();
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseVisuals();
        ConfigureButtonColors();
        RefreshImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureBaseVisuals();
        ConfigureButtonColors();
        RefreshImmediate();
    }

    private void OnDisable()
    {
        RestoreBaseScale();

        if (readyGlowImage != null)
            readyGlowImage.enabled = false;
    }

    private void Update()
    {
        UpdatePendingAugmentLabel();
        bool ready = EvaluateReady();

        if (startButton != null &&
            startButton.interactable != ready)
        {
            startButton.interactable = ready;
        }

        if (lastReadyState != ready)
        {
            lastReadyState = ready;

            if (!ready)
                ApplyUnavailableVisual();
        }

        if (ready)
            ApplyReadySparkle();
    }

    public void RefreshImmediate()
    {
        UpdatePendingAugmentLabel();
        bool ready = EvaluateReady();
        lastReadyState = ready;

        if (startButton != null)
            startButton.interactable = ready;

        if (ready)
            ApplyReadySparkle();
        else
            ApplyUnavailableVisual();
    }

    private bool EvaluateReady()
    {
        if (battleManager == null ||
            !battleManager.IsInitialized ||
            battleManager.IsEndingOrEnded ||
            battleManager.TurnManager == null ||
            !battleManager.TurnManager.IsBattleRunning ||
            battleManager.TurnManager.IsResolving ||
            battleManager.IsWaitingForEmotionAugmentChoice ||
            battleManager.ActionManager == null ||
            battleManager.ActionManager.IsDisposed ||
            battleManager.BattleContext?.Player == null ||
            battleManager.BattleContext.Player.IsDead)
        {
            return false;
        }

        return battleManager.ActionManager.CountSlots(
                   battleManager.BattleContext.Player) > 0;
    }

    private void ApplyUnavailableVisual()
    {
        CaptureBaseVisuals();

        if (backgroundImage != null)
        {
            float factor =
                Mathf.Clamp01(unavailableBrightness);

            backgroundImage.color =
                new Color(
                    baseBackgroundColor.r * factor,
                    baseBackgroundColor.g * factor,
                    baseBackgroundColor.b * factor,
                    baseBackgroundColor.a);
        }

        if (labelText != null)
        {
            Color color = baseLabelColor;
            color.a *= Mathf.Clamp01(unavailableLabelAlpha);
            labelText.color = color;
        }

        if (readyGlowImage != null)
            readyGlowImage.enabled = false;

        RestoreBaseScale();
    }

    private void ApplyReadySparkle()
    {
        CaptureBaseVisuals();

        float wave =
            0.5f +
            Mathf.Sin(
                Time.unscaledTime *
                Mathf.Max(0.1f, sparkleSpeed) *
                Mathf.PI * 2f) *
            0.5f;

        if (backgroundImage != null)
        {
            backgroundImage.color =
                Color.Lerp(
                    baseBackgroundColor,
                    Color.white,
                    wave * Mathf.Clamp01(brightenAmount));
        }

        if (labelText != null)
        {
            labelText.color =
                Color.Lerp(
                    baseLabelColor,
                    readyGlowColor,
                    wave * 0.32f);
        }

        if (readyGlowImage != null)
        {
            readyGlowImage.enabled = true;

            Color glow = readyGlowColor;
            glow.a = Mathf.Lerp(
                minimumGlowAlpha,
                maximumGlowAlpha,
                wave);

            readyGlowImage.color = glow;
        }

        transform.localScale =
            baseScale *
            (1f + wave * scalePulseAmount);
    }

    private void ResolveReferences()
    {
        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<BattleManager>(
                    FindObjectsInactive.Include);
        }

        startButton ??=
            GetComponent<Button>();

        backgroundImage ??=
            startButton?.targetGraphic as Image;

        backgroundImage ??=
            GetComponent<Image>();

        labelText ??=
            GetComponentInChildren<TMP_Text>(true);

        if (readyGlowImage == null)
        {
            Transform glow =
                transform.Find("ReadyGlow");

            if (glow != null)
                readyGlowImage = glow.GetComponent<Image>();
        }
    }

    private void CaptureBaseVisuals()
    {
        if (baseCaptured)
            return;

        ResolveReferences();

        if (backgroundImage != null)
            baseBackgroundColor = backgroundImage.color;

        if (labelText != null)
        {
            baseLabelColor = labelText.color;
            baseLabelText = labelText.text;
        }

        baseScale = transform.localScale;
        baseCaptured = true;
    }


    private void UpdatePendingAugmentLabel()
    {
        if (labelText == null)
            return;

        if (battleManager != null &&
            battleManager.IsWaitingForEmotionAugmentChoice)
        {
            labelText.text = "증강 선택 대기";
            return;
        }

        if (baseCaptured)
            labelText.text = baseLabelText;
    }

    private void ConfigureButtonColors()
    {
        if (startButton == null)
            return;

        ColorBlock colors = startButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = colors.highlightedColor;

        // disabledColor가 다시 검게 곱해지는 것을 막고,
        // 실제 비활성 색은 backgroundImage.color로 직접 제어한다.
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        startButton.colors = colors;
    }

    private void RestoreBaseScale()
    {
        transform.localScale = baseScale;
    }
}