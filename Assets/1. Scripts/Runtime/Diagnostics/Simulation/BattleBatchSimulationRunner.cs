using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class BattleBatchSimulationRunner : MonoBehaviour
{
    [Header("Batch Counts")]
    [SerializeField, Min(1)] private int winRateRunCount = 100;
    [SerializeField, Min(1)] private int damageRunCount = 50;
    [SerializeField, Min(1)] private int maximumTurnsPerBattle = 50;

    [Header("Execution")]
    [SerializeField] private bool suppressInfoLogs = true;
    [SerializeField] private bool reloadCleanSceneAfterBatch = true;
    [SerializeField] private string outputSubdirectory =
        "ProjectAbyss/BattleSimulation";

    public static BattleBatchSimulationRunner Instance { get; private set; }

    public bool IsRunning => batchRoutine != null;
    public string LastOutputDirectory { get; private set; }
    public BattleSimulationSummary LastSummary { get; private set; }

    private readonly BattleAverageAIPlanner playerPlanner = new();

    private BattleAnalysisDebugPanel panel;
    private Coroutine batchRoutine;
    private bool stopRequested;
    private int sourceSceneBuildIndex = -1;
    private string sourceSceneName;

    private BattleManager activeManager;
    private BattleContext activeContext;
    private BattleSimulationRunResult activeRun;
    private bool activeBattleEnded;

    private LogType savedLogFilter;

    public static BattleBatchSimulationRunner GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        BattleBatchSimulationRunner existing =
            FindFirstObjectByType<BattleBatchSimulationRunner>();

        if (existing != null)
            return existing;

        GameObject root = new GameObject(
            "BattleAnalysisRuntime");

        return root.AddComponent<
            BattleBatchSimulationRunner>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded +=
            HandleSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(BindPanelNextFrame());
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -=
            HandleSceneLoaded;

        BattleSimulationRuntime.EndBatch();
        Debug.unityLogger.filterLogType = savedLogFilter;
        Instance = null;
    }

    public void Configure(
        int winRuns,
        int damageRuns,
        int maxTurns)
    {
        winRateRunCount = Mathf.Max(1, winRuns);
        damageRunCount = Mathf.Max(1, damageRuns);
        maximumTurnsPerBattle = Mathf.Max(1, maxTurns);
    }

    public void RegisterPanel(
        BattleAnalysisDebugPanel debugPanel)
    {
        panel = debugPanel;
        panel?.SetRunning(IsRunning);

        if (LastSummary != null)
            panel?.SetStatus(LastSummary.ToDisplayString());
        else
            panel?.SetStatus(
                "동적 로그: Play Mode 전투 시 자동 기록\n" +
                "승률 또는 피해량 버튼으로 AI 배치 분석 실행");
    }

    public void RunWinRateAnalysis()
    {
        StartBatch(
            BattleSimulationMode.WinRate,
            winRateRunCount);
    }

    public void RunDamageAnalysis()
    {
        StartBatch(
            BattleSimulationMode.Damage,
            damageRunCount);
    }

    public void StopAnalysis()
    {
        if (!IsRunning)
            return;

        stopRequested = true;
        panel?.SetStatus("현재 전투가 끝나는 즉시 분석을 중단합니다.");
    }

    private void StartBatch(
        BattleSimulationMode mode,
        int runCount)
    {
        if (IsRunning)
        {
            panel?.SetStatus("이미 배치 분석이 진행 중입니다.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (scene.buildIndex < 0)
        {
            panel?.SetStatus(
                "현재 씬이 Build Settings에 없습니다.\n" +
                "Tools > Project Abyss > Diagnostics > " +
                "Setup Dynamic Analysis & AI Buttons를 다시 실행하세요.");
            return;
        }

        sourceSceneBuildIndex = scene.buildIndex;
        sourceSceneName = scene.name;
        stopRequested = false;

        batchRoutine = StartCoroutine(
            RunBatch(mode, Mathf.Max(1, runCount)));
    }

    private IEnumerator RunBatch(
        BattleSimulationMode mode,
        int runCount)
    {
        panel?.SetRunning(true);

        LastSummary = new BattleSimulationSummary
        {
            mode = mode.ToString(),
            scene = sourceSceneName,
            startedUtc = DateTime.UtcNow.ToString("O"),
            requestedRuns = runCount
        };

        BattleSimulationRuntime.BeginBatch(
            suppressDetailedTrace: true);

        savedLogFilter = Debug.unityLogger.filterLogType;

        if (suppressInfoLogs)
            Debug.unityLogger.filterLogType = LogType.Warning;

        try
        {
            for (int runIndex = 1;
                 runIndex <= runCount;
                 runIndex++)
            {
                if (stopRequested)
                    break;

                panel?.SetStatus(
                    $"{mode} 분석 중... {runIndex}/{runCount}\n" +
                    "씬을 새로 불러 실제 전투 코드를 자동 실행합니다.");

                yield return LoadSourceScene();

                BattleSimulationRunResult result =
                    new BattleSimulationRunResult
                    {
                        runIndex = runIndex
                    };

                yield return RunCurrentSceneBattle(result);

                LastSummary.runs.Add(result);
                LastSummary.Recalculate();

                panel?.SetStatus(
                    $"{mode} 분석 중... {runIndex}/{runCount}\n" +
                    $"현재 승률 {LastSummary.winRate:0.0}%\n" +
                    $"평균 가한 피해 " +
                    $"{LastSummary.averagePlayerDamageDealt:0.0}");
            }

            LastSummary.completedUtc =
                DateTime.UtcNow.ToString("O");
            LastSummary.Recalculate();
            WriteSummary(LastSummary);
        }
        finally
        {
            UnsubscribeActiveBattle();
            BattleSimulationRuntime.EndBatch();
            Debug.unityLogger.filterLogType = savedLogFilter;

            batchRoutine = null;
            panel?.SetRunning(false);

            if (LastSummary != null)
            {
                panel?.SetStatus(
                    LastSummary.ToDisplayString() +
                    "\n저장 위치:\n" +
                    LastOutputDirectory);
            }
        }

        if (reloadCleanSceneAfterBatch &&
            sourceSceneBuildIndex >= 0)
        {
            yield return LoadSourceScene();
        }
    }

    private IEnumerator LoadSourceScene()
    {
        AsyncOperation operation =
            SceneManager.LoadSceneAsync(
                sourceSceneBuildIndex,
                LoadSceneMode.Single);

        if (operation == null)
            yield break;

        while (!operation.isDone)
            yield return null;

        // Awake/Start 및 BattleManager의 한 프레임 지연 자동 시작을 기다린다.
        yield return null;
        yield return null;
    }

    private IEnumerator RunCurrentSceneBattle(
        BattleSimulationRunResult result)
    {
        activeManager =
            FindFirstObjectByType<BattleManager>();

        if (activeManager == null)
        {
            FailRun(result, "BattleManager를 찾지 못했습니다.");
            yield break;
        }

        if (!activeManager.IsInitialized &&
            !activeManager.InitializeBattle())
        {
            FailRun(result, "BattleManager.InitializeBattle 실패");
            yield break;
        }

        activeContext = activeManager.BattleContext;

        if (activeContext?.Player == null ||
            activeManager.TurnManager == null)
        {
            FailRun(result, "BattleContext 또는 Player가 없습니다.");
            yield break;
        }

        result.playerMaxHp =
            activeContext.Player.MaxCombatHP;

        activeRun = result;
        SubscribeActiveBattle();

        if (!activeManager.TurnManager.IsBattleRunning)
            activeManager.StartBattle();

        int lastSubmittedTurn = -1;
        int noProgressFrames = 0;
        activeBattleEnded = false;

        while (!activeBattleEnded &&
               activeManager != null &&
               !activeManager.IsEndingOrEnded)
        {
            if (stopRequested)
            {
                result.timedOut = true;
                result.outcome = "STOPPED";
                activeManager.EndBattle();
                break;
            }

            TurnManager turnManager =
                activeManager.TurnManager;

            if (turnManager == null)
            {
                FailRun(result, "TurnManager가 사라졌습니다.");
                break;
            }

            if (turnManager.CurrentTurn >
                maximumTurnsPerBattle)
            {
                result.timedOut = true;
                result.outcome = "TURN_LIMIT_DRAW";
                activeManager.EndBattle();
                break;
            }

            if (turnManager.IsBattleRunning &&
                !turnManager.IsResolving &&
                turnManager.CurrentTurn != lastSubmittedTurn)
            {
                int planned =
                    playerPlanner.PlanPlayer(activeManager);

                if (planned <= 0)
                {
                    FailRun(
                        result,
                        "평균 AI가 플레이어 행동을 하나도 만들지 못했습니다.");
                    activeManager.EndBattle();
                    break;
                }

                lastSubmittedTurn =
                    turnManager.CurrentTurn;

                activeManager.NextTurn();
                noProgressFrames = 0;
            }
            else
            {
                noProgressFrames++;
            }

            if (noProgressFrames > 36000)
            {
                result.timedOut = true;
                result.outcome = "FRAME_TIMEOUT_DRAW";
                activeManager.EndBattle();
                break;
            }

            yield return null;
        }

        if (string.IsNullOrWhiteSpace(result.outcome))
            result.outcome = ResolveOutcome(activeContext);

        result.turns = Mathf.Max(
            result.turns,
            (activeManager?.TurnManager?.CurrentTurn ?? 1) - 1);

        result.playerRemainingHp =
            activeContext?.Player?.CurrentHP ?? 0;

        UnsubscribeActiveBattle();
        activeManager = null;
        activeContext = null;
        activeRun = null;
    }

    private void SubscribeActiveBattle()
    {
        UnsubscribeActiveBattle();

        activeRun = activeRun ?? new BattleSimulationRunResult();
        BattleEvent battleEvent = activeContext?._battleEvent;

        if (battleEvent == null || battleEvent.IsDisposed)
            return;

        battleEvent.OnDamageEventResolved += HandleDamage;
        battleEvent.OnActionStart += HandleActionStart;
        battleEvent.OnClashResolved += HandleClash;
        battleEvent.OnTurnEnd += HandleTurnEnd;
        battleEvent.OnBattleEnded += HandleBattleEnded;
    }

    private void UnsubscribeActiveBattle()
    {
        BattleEvent battleEvent = activeContext?._battleEvent;

        if (battleEvent != null && !battleEvent.IsDisposed)
        {
            battleEvent.OnDamageEventResolved -= HandleDamage;
            battleEvent.OnActionStart -= HandleActionStart;
            battleEvent.OnClashResolved -= HandleClash;
            battleEvent.OnTurnEnd -= HandleTurnEnd;
            battleEvent.OnBattleEnded -= HandleBattleEnded;
        }
    }

    private void HandleDamage(DamageEventResult result)
    {
        if (activeRun == null || activeContext == null)
            return;

        DamageContext damage = result?.Context;

        if (damage == null)
            return;

        int amount = damage.GetDisplayDamage();
        Character player = activeContext.Player;

        if (damage.Attacker == player &&
            damage.Target != player)
        {
            activeRun.playerDamageDealt += amount;
        }

        if (damage.Target == player)
            activeRun.playerDamageTaken += amount;
    }

    private void HandleActionStart(BattleAction action)
    {
        if (activeRun == null || action?.Owner == null)
            return;

        if (action.Owner == activeContext?.Player)
            activeRun.playerActions++;
        else
            activeRun.enemyActions++;
    }

    private void HandleClash(ClashResultContext result)
    {
        if (activeRun != null && result?.IsClash == true)
            activeRun.clashes++;
    }

    private void HandleTurnEnd(int turn)
    {
        if (activeRun != null)
            activeRun.turns = Mathf.Max(activeRun.turns, turn);
    }

    private void HandleBattleEnded()
    {
        activeBattleEnded = true;
    }

    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        panel = null;
        StartCoroutine(BindPanelNextFrame());
    }

    private IEnumerator BindPanelNextFrame()
    {
        yield return null;

        BattleAnalysisDebugPanel found =
            FindFirstObjectByType<BattleAnalysisDebugPanel>();

        if (found != null)
            found.SetRunning(IsRunning);

        if (found != null)
            RegisterPanel(found);
    }

    private void WriteSummary(
        BattleSimulationSummary summary)
    {
        string timestamp =
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");

        LastOutputDirectory = Path.Combine(
            Application.persistentDataPath,
            outputSubdirectory,
            timestamp + "_" + summary.mode);

        Directory.CreateDirectory(
            LastOutputDirectory);

        File.WriteAllText(
            Path.Combine(LastOutputDirectory, "summary.json"),
            JsonUtility.ToJson(summary, true),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(LastOutputDirectory, "summary.txt"),
            summary.ToDisplayString(),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(
                Directory.GetParent(LastOutputDirectory)?.FullName ??
                LastOutputDirectory,
                "latest_simulation.txt"),
            LastOutputDirectory,
            new UTF8Encoding(false));
    }

    private static string ResolveOutcome(
        BattleContext context)
    {
        Character player = context?.Player;

        if (player == null)
            return "INVALID";

        if (player.IsDead)
            return "PLAYER_DEFEAT";

        if (context.Enemies != null)
        {
            foreach (Character enemy in context.Enemies)
            {
                if (enemy != null && !enemy.IsDead)
                    return "DRAW_OR_ABORTED";
            }
        }

        return "PLAYER_VICTORY";
    }

    private static void FailRun(
        BattleSimulationRunResult result,
        string message)
    {
        result.failed = true;
        result.failureMessage = message;
        result.outcome = "FAILED";
    }
}
