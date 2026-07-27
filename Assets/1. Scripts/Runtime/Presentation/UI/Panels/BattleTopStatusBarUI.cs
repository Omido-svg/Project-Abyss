using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleTopStatusBarUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text momentumText;
    [SerializeField] private TMP_Text lightText;
    [SerializeField] private TMP_Text enemyText;
    [SerializeField] private Slider momentumSlider;

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
        ApplyReadableTextSettings();
        Refresh();
    }

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        ApplyReadableTextSettings();
    }

    private void LateUpdate()
    {
        Refresh();
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
            int current = player != null ? player.CurrentEnergy : 0;
            int max = player != null ? player.MaxEnergy : 0;
            lightText.text = $"빛 {current}/{max}";
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
}