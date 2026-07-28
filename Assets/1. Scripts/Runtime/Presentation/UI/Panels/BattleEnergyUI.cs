using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 플레이어의 현재 빛과 계획에 예약된 빛을 표시한다.
/// 선택 중인 스킬 비용도 현재 논리 슬롯 교체 기준으로 즉시 미리 보여준다.
/// </summary>
public sealed class BattleEnergyUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager battleUIManager;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private string title = "빛";
    [SerializeField] private bool showPlannedCost = true;

    [Header("Insufficient Light Feedback")]
    [SerializeField] private Color insufficientColor =
        new Color(1f, 0.18f, 0.18f, 1f);
    [SerializeField, Min(0.05f)] private float feedbackDuration = 0.42f;
    [SerializeField, Min(0f)] private float shakeStrength = 14f;

    private BattleContext context;
    private BattleEvent battleEvent;
    private string lastRenderedText;
    private Sequence feedbackSequence;
    private Color normalColor = Color.white;
    private Vector2 normalPosition;
    private Vector3 normalScale = Vector3.one;
    private bool visualCaptured;
    private bool feedbackActive;

    public void Configure(
        BattleManager manager,
        TMP_Text text)
    {
        battleManager = manager;
        energyText = text;
        ResolveManager();
        CaptureVisual();
        Rebind();
        Refresh(force: true);
    }

    private void Awake()
    {
        energyText ??= GetComponentInChildren<TMP_Text>(true);
        ResolveManager();
        CaptureVisual();
    }

    private void OnEnable()
    {
        ResolveManager();

        if (battleManager != null)
            battleManager.BattlePrepared += HandleBattlePrepared;

        Rebind();
        Refresh(force: true);
    }

    private void OnDisable()
    {
        if (battleManager != null)
            battleManager.BattlePrepared -= HandleBattlePrepared;

        UnbindBattleEvent();
        KillFeedback(restore: true);
    }

    private void LateUpdate()
    {
        ResolveManager();
        Refresh(force: false);
    }

    private void ResolveManager()
    {
        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<BattleManager>(
                    FindObjectsInactive.Include);
        }

        if (battleUIManager == null)
        {
            battleUIManager =
                FindFirstObjectByType<BattleUIManager>(
                    FindObjectsInactive.Include);
        }
    }

    private void HandleBattlePrepared(BattleContext prepared)
    {
        context = prepared;
        Rebind();
        Refresh(force: true);
    }

    private void Rebind()
    {
        ResolveManager();

        BattleContext next = battleManager?.BattleContext;

        if (next != null)
            context = next;

        BattleEvent nextEvent = context?._battleEvent;

        if (battleEvent == nextEvent)
            return;

        UnbindBattleEvent();
        battleEvent = nextEvent;

        if (battleEvent == null || battleEvent.IsDisposed)
            return;

        battleEvent.OnCombatResourceChanged += HandleResourceChanged;
        battleEvent.OnTurnStart += HandleTurnChanged;
        battleEvent.OnTurnEnd += HandleTurnChanged;
        battleEvent.OnActionStart += HandleActionChanged;
        battleEvent.OnActionEnd += HandleActionChanged;
    }

    private void UnbindBattleEvent()
    {
        if (battleEvent != null && !battleEvent.IsDisposed)
        {
            battleEvent.OnCombatResourceChanged -= HandleResourceChanged;
            battleEvent.OnTurnStart -= HandleTurnChanged;
            battleEvent.OnTurnEnd -= HandleTurnChanged;
            battleEvent.OnActionStart -= HandleActionChanged;
            battleEvent.OnActionEnd -= HandleActionChanged;
        }

        battleEvent = null;
    }

    private void HandleResourceChanged(
        CombatResourceChangeContext change)
    {
        if (change?.Owner == context?.Player &&
            change.ResourceKey == CombatResourceKeys.Energy)
        {
            Refresh(force: true);
        }
    }

    private void HandleTurnChanged(int _)
    {
        Refresh(force: true);
    }

    private void HandleActionChanged(BattleAction _)
    {
        Refresh(force: true);
    }

    private void Refresh(bool force)
    {
        if (energyText == null)
            return;

        Character player = context?.Player ??
                           battleManager?.BattleContext?.Player;

        string rendered;

        if (player == null)
        {
            rendered = $"{title} -- / --";
        }
        else
        {
            int current = player.CurrentEnergy;
            int maximum = player.MaxEnergy;

            bool planning =
                showPlannedCost &&
                battleManager?.TurnManager != null &&
                !battleManager.TurnManager.IsResolving;

            if (planning &&
                battleUIManager != null &&
                battleUIManager.TryGetPlayerEnergyDisplay(
                    player,
                    out int available,
                    out maximum,
                    out int planned,
                    out int pending,
                    out bool hasPending))
            {
                string pendingText = hasPending
                    ? $" · 선택 {pending}"
                    : string.Empty;

                rendered =
                    $"<b>{title} {available}/{maximum}</b>\n" +
                    $"계획 {planned}{pendingText}";
            }
            else if (planning && battleManager.ActionManager != null)
            {
                int fallbackPlanned =
                    battleManager.ActionManager
                        .GetPlannedEnergyCost(player);

                int remaining =
                    Mathf.Max(0, current - fallbackPlanned);

                rendered =
                    $"<b>{title} {remaining}/{maximum}</b>\n" +
                    $"계획 {fallbackPlanned}";
            }
            else
            {
                rendered =
                    $"<b>{title} {current}/{maximum}</b>";
            }
        }

        if (feedbackActive)
            rendered = $"<color=#FF3030><b>빛 부족</b></color>\n{rendered}";

        if (!force && rendered == lastRenderedText)
            return;

        energyText.richText = true;
        energyText.text = rendered;
        lastRenderedText = rendered;
    }

    public void PlayInsufficientEnergyFeedback(string _ = null)
    {
        if (energyText == null)
            return;

        CaptureVisual();
        KillFeedback(restore: true);

        feedbackActive = true;
        energyText.color = insufficientColor;

        RectTransform rect = energyText.rectTransform;
        rect.anchoredPosition = normalPosition;
        rect.localScale = normalScale;

        feedbackSequence = DOTween.Sequence()
            .SetUpdate(true);

        feedbackSequence.Join(
            rect.DOShakeAnchorPos(
                feedbackDuration,
                new Vector2(shakeStrength, 2f),
                22,
                80f,
                false,
                true));

        feedbackSequence.Join(
            rect.DOPunchScale(
                new Vector3(0.08f, 0.08f, 0f),
                feedbackDuration * 0.75f,
                8,
                0.75f));

        feedbackSequence.OnComplete(() =>
        {
            feedbackSequence = null;
            feedbackActive = false;
            RestoreVisual();
            Refresh(force: true);
        });

        Refresh(force: true);
    }

    private void CaptureVisual()
    {
        if (visualCaptured || energyText == null)
            return;

        normalColor = energyText.color;
        normalPosition = energyText.rectTransform.anchoredPosition;
        normalScale = energyText.rectTransform.localScale;
        visualCaptured = true;
    }

    private void KillFeedback(bool restore)
    {
        if (feedbackSequence != null)
        {
            feedbackSequence.Kill(false);
            feedbackSequence = null;
        }

        feedbackActive = false;

        if (restore)
            RestoreVisual();
    }

    private void RestoreVisual()
    {
        if (!visualCaptured || energyText == null)
            return;

        energyText.color = normalColor;
        energyText.rectTransform.anchoredPosition = normalPosition;
        energyText.rectTransform.localScale = normalScale;
    }

    private void OnDestroy()
    {
        KillFeedback(restore: false);
    }
}
