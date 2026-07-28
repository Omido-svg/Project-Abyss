using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleTopStatusBarUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager battleUIManager;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text momentumText;
    [SerializeField] private TMP_Text lightText;
    [SerializeField] private TMP_Text enemyText;
    [SerializeField] private Slider momentumSlider;

    [Header("Insufficient Light Feedback")]
    [SerializeField] private Color insufficientColor =
        new Color(1f, 0.18f, 0.18f, 1f);
    [SerializeField, Min(0.05f)] private float feedbackDuration = 0.42f;
    [SerializeField, Min(0f)] private float shakeStrength = 14f;
    [SerializeField, Range(1, 40)] private int shakeVibrato = 22;

    private Sequence lightFeedbackSequence;
    private Color normalLightColor = Color.white;
    private Vector2 normalLightPosition;
    private Vector3 normalLightScale = Vector3.one;
    private bool lightVisualCaptured;
    private bool insufficientFeedbackActive;

    public void Configure(
        BattleManager manager,
        TMP_Text turn,
        TMP_Text momentum,
        TMP_Text light,
        TMP_Text enemy,
        Slider slider)
    {
        battleManager = manager;
        turnText = turn;
        momentumText = momentum;
        lightText = light;
        enemyText = enemy;
        momentumSlider = slider;
        ResolveUiManager();
        ApplyReadableTextSettings();
        CaptureLightVisual();
        Refresh();
    }

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        ResolveUiManager();
        ApplyReadableTextSettings();
        CaptureLightVisual();
    }

    private void LateUpdate()
    {
        ResolveUiManager();
        Refresh();
    }

    private void ResolveUiManager()
    {
        if (battleUIManager == null)
        {
            battleUIManager =
                FindFirstObjectByType<BattleUIManager>(
                    FindObjectsInactive.Include);
        }
    }

    public void Refresh()
    {
        if (battleManager == null)
            return;

        int turn = battleManager.TurnManager != null
            ? Mathf.Max(1, battleManager.TurnManager.CurrentTurn)
            : 1;

        if (turnText != null)
            turnText.text = $"턴 {turn:00}";

        int momentum = battleManager.MomentumManager != null
            ? battleManager.MomentumManager.CurrentMomentum
            : 0;

        if (momentumText != null)
        {
            string state = momentum > 0
                ? "우세"
                : momentum < 0
                    ? "열세"
                    : "균형";

            momentumText.text =
                $"기세 {momentum:+#;-#;0}  ·  {state}";
        }

        if (momentumSlider != null)
        {
            momentumSlider.minValue = MomentumManager.MinMomentum;
            momentumSlider.maxValue = MomentumManager.MaxMomentum;
            momentumSlider.value = momentum;
            momentumSlider.interactable = false;
        }

        Character player = battleManager.BattleContext?.Player;

        if (lightText != null)
        {
            int available = player != null ? player.CurrentEnergy : 0;
            int maximum = player != null ? player.MaxEnergy : 0;
            bool pending = false;

            if (player != null &&
                battleUIManager != null)
            {
                battleUIManager.TryGetPlayerEnergyDisplay(
                    player,
                    out available,
                    out maximum,
                    out _,
                    out _,
                    out pending);
            }

            string prefix =
                insufficientFeedbackActive
                    ? "빛 부족"
                    : "빛";

            string pendingMark = pending ? "  ◀ 선택 반영" : string.Empty;
            lightText.text =
                $"{prefix} {available}/{maximum}{pendingMark}";
        }

        if (enemyText != null)
        {
            int alive = 0;
            int total = 0;

            if (battleManager.BattleContext?.Enemies != null)
            {
                foreach (Character enemy in battleManager.BattleContext.Enemies)
                {
                    if (enemy == null)
                        continue;

                    total++;

                    if (!enemy.IsDead)
                        alive++;
                }
            }

            enemyText.text = $"적 {alive}/{total}";
        }
    }

    public void PlayInsufficientEnergyFeedback(string _ = null)
    {
        if (lightText == null)
            return;

        CaptureLightVisual();
        KillLightFeedback(restore: true);

        insufficientFeedbackActive = true;
        lightText.color = insufficientColor;

        RectTransform rect = lightText.rectTransform;
        rect.anchoredPosition = normalLightPosition;
        rect.localScale = normalLightScale;

        lightFeedbackSequence = DOTween.Sequence()
            .SetUpdate(true);

        lightFeedbackSequence.Join(
            rect.DOShakeAnchorPos(
                feedbackDuration,
                new Vector2(shakeStrength, 2f),
                shakeVibrato,
                80f,
                false,
                true));

        lightFeedbackSequence.Join(
            rect.DOPunchScale(
                new Vector3(0.08f, 0.08f, 0f),
                feedbackDuration * 0.75f,
                8,
                0.75f));

        lightFeedbackSequence.OnComplete(() =>
        {
            lightFeedbackSequence = null;
            insufficientFeedbackActive = false;
            RestoreLightVisual();
            Refresh();
        });

        Refresh();
    }

    private void CaptureLightVisual()
    {
        if (lightVisualCaptured || lightText == null)
            return;

        normalLightColor = lightText.color;
        normalLightPosition = lightText.rectTransform.anchoredPosition;
        normalLightScale = lightText.rectTransform.localScale;
        lightVisualCaptured = true;
    }

    private void KillLightFeedback(bool restore)
    {
        if (lightFeedbackSequence != null)
        {
            lightFeedbackSequence.Kill(false);
            lightFeedbackSequence = null;
        }

        insufficientFeedbackActive = false;

        if (restore)
            RestoreLightVisual();
    }

    private void RestoreLightVisual()
    {
        if (!lightVisualCaptured || lightText == null)
            return;

        lightText.color = normalLightColor;
        lightText.rectTransform.anchoredPosition = normalLightPosition;
        lightText.rectTransform.localScale = normalLightScale;
    }

    private void ApplyReadableTextSettings()
    {
        ConfigureText(turnText, 27f, TextAlignmentOptions.Center);
        ConfigureText(momentumText, 25f, TextAlignmentOptions.Left);
        ConfigureText(lightText, 25f, TextAlignmentOptions.Center);
        ConfigureText(enemyText, 25f, TextAlignmentOptions.Center);
    }

    private static void ConfigureText(
        TMP_Text text,
        float size,
        TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        text.richText = false;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.alignment = alignment;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero;
    }

    private void OnDestroy()
    {
        KillLightFeedback(restore: false);
    }
}
