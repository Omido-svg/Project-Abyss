using TMPro;
using UnityEngine;

/// <summary>
/// 턴/입력 단계/기세/보스 페이즈와 양측 상태를 한눈에 보여주는 읽기 전용 HUD.
/// </summary>
public sealed class BattleReadableHudUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text momentumText;
    [SerializeField] private TMP_Text bossPhaseText;
    [SerializeField] private BattleStatusListUI playerStatusList;
    [SerializeField] private BattleStatusListUI enemyStatusList;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.1f;

    private float nextRefresh;
    private Character boundPlayer;
    private Character boundEnemy;

    public void Configure(
        BattleManager manager,
        TMP_Text turnLabel,
        TMP_Text momentumLabel,
        TMP_Text bossPhaseLabel,
        BattleStatusListUI playerStatuses,
        BattleStatusListUI enemyStatuses)
    {
        battleManager = manager;
        turnText = turnLabel;
        momentumText = momentumLabel;
        bossPhaseText = bossPhaseLabel;
        playerStatusList = playerStatuses;
        enemyStatusList = enemyStatuses;

        Refresh(force: true);
    }

    private void Awake()
    {
        ResolveManager();
    }

    private void OnEnable()
    {
        ResolveManager();

        if (battleManager != null)
            battleManager.BattlePrepared += HandleBattlePrepared;

        Refresh(force: true);
    }

    private void OnDisable()
    {
        if (battleManager != null)
            battleManager.BattlePrepared -= HandleBattlePrepared;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh =
            Time.unscaledTime + refreshInterval;

        Refresh(force: false);
    }

    private void ResolveManager()
    {
        battleManager ??=
            FindFirstObjectByType<BattleManager>(
                FindObjectsInactive.Include);
    }

    private void HandleBattlePrepared(
        BattleContext _)
    {
        Refresh(force: true);
    }

    private void Refresh(bool force)
    {
        ResolveManager();

        BattleContext context =
            battleManager?.BattleContext;

        Character player =
            context?.Player;

        Character enemy =
            context?.Enemies != null &&
            context.Enemies.Count > 0
                ? context.Enemies[0]
                : null;

        if (force || player != boundPlayer)
        {
            boundPlayer = player;
            playerStatusList?.SetOwner(player);
        }

        if (force || enemy != boundEnemy)
        {
            boundEnemy = enemy;
            enemyStatusList?.SetOwner(enemy);
        }

        RefreshTurn();
        RefreshMomentum(player);
        RefreshBossPhase(enemy);
    }

    private void RefreshTurn()
    {
        if (turnText == null)
            return;

        TurnManager turnManager =
            battleManager?.TurnManager;

        if (turnManager == null)
        {
            turnText.text = "<b>전투 준비</b>";
            return;
        }

        string phase =
            turnManager.IsResolving
                ? "행동 해결"
                : turnManager.IsBattleRunning
                    ? "조준 / 계획"
                    : "전투 대기";

        turnText.richText = true;
        turnText.text =
            $"<b>TURN {Mathf.Max(0, turnManager.CurrentTurn)}</b>\n" +
            phase;
    }

    private void RefreshMomentum(
        Character player)
    {
        if (momentumText == null)
            return;

        MomentumManager manager =
            battleManager?.MomentumManager;

        if (manager == null)
        {
            momentumText.text = "<b>기세 0</b>\n균형 ×1";
            return;
        }

        int value =
            manager.GetPerspectiveValue(player);

        MomentumState state =
            manager.GetState(player);

        float multiplier =
            manager.GetDamageMultiplier(player);

        string stateName =
            state switch
            {
                MomentumState.LastStand => "발악 / 열세",
                MomentumState.Disadvantage => "열세",
                MomentumState.Balance => "균형",
                MomentumState.Advantage => "우세",
                MomentumState.Overwhelm => "짓누름",
                _ => state.ToString()
            };

        momentumText.richText = true;
        momentumText.text =
            $"<b>기세 {value:+#;-#;0}</b>\n" +
            $"{stateName} · 피해 ×{multiplier:0.##}";
    }

    private void RefreshBossPhase(
        Character enemy)
    {
        if (bossPhaseText == null)
            return;

        BossPhaseData phase =
            enemy?.CombatRulesRuntime?
                .CurrentBossPhase;

        bossPhaseText.richText = true;
        bossPhaseText.text =
            phase == null
                ? "<b>적 행동</b>\n기본 패턴"
                : $"<b>보스 페이즈</b>\n{phase.DisplayName}";
    }
}
