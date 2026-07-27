
using TMPro;
using UnityEngine;

/// <summary>
/// 플레이어의 현재 에너지와 계획에 예약된 에너지를 표시한다.
/// ActionManager에는 슬롯 변경 이벤트가 없으므로 값이 바뀔 때만 텍스트를 갱신한다.
/// </summary>
public sealed class BattleEnergyUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private string title = "에너지";
    [SerializeField] private bool showPlannedCost = true;

    private BattleContext context;
    private BattleEvent battleEvent;
    private string lastRenderedText;

    public void Configure(
        BattleManager manager,
        TMP_Text text)
    {
        battleManager = manager;
        energyText = text;
        Rebind();
        Refresh(force: true);
    }

    private void Awake()
    {
        energyText ??= GetComponentInChildren<TMP_Text>(true);
        ResolveManager();
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
    }

    private void LateUpdate()
    {
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

            if (planning && battleManager.ActionManager != null)
            {
                int planned =
                    battleManager.ActionManager
                        .GetPlannedEnergyCost(player);

                int remaining = Mathf.Max(0, current - planned);

                rendered =
                    $"<b>{title} {current}/{maximum}</b>\n" +
                    $"계획 {planned} · 잔여 {remaining}";
            }
            else
            {
                rendered =
                    $"<b>{title} {current}/{maximum}</b>";
            }
        }

        if (!force && rendered == lastRenderedText)
            return;

        energyText.richText = true;
        energyText.text = rendered;
        lastRenderedText = rendered;
    }
}