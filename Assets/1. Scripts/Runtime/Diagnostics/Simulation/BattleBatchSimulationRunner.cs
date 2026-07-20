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
    [SerializeField] private bool recordDetailedTraceDuringBatch;
    [SerializeField] private bool writeProgressAfterEveryRun = true;
    [SerializeField] private bool openOutputFolderWhenComplete;
    [SerializeField, Min(2f)] private float sceneLoadTimeoutSeconds = 20f;
    [SerializeField, Min(2f)] private float battleReadyTimeoutSeconds = 10f;
    [SerializeField, Min(2f)] private float noProgressTimeoutSeconds = 15f;

    public static BattleBatchSimulationRunner Instance { get; private set; }

    public bool IsRunning => batchRoutine != null;

    // persistentDataPath/ProjectAbyssDiagnostics/BattleSimulation 아래에 저장한다.
    public string OutputRootDirectory =>
        BattleAnalysisOutputPaths.BattleSimulationDirectory;
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
    private bool logFilterCaptured;

    private float savedTimeScale = 1f;
    private bool timeScaleCaptured;

    private string runsJsonlPath;
    private string batchEventsJsonlPath;
    private long nextBatchEventSequence = 1;
    private bool lastSceneLoadSucceeded;
    private string lastSceneLoadError;

    public static BattleBatchSimulationRunner GetOrCreate()
    {
        if (Instance != null)
        {
            Instance.EnsurePersistentRoot();
            return Instance;
        }

        BattleBatchSimulationRunner existing =
            FindFirstObjectByType<BattleBatchSimulationRunner>(
                FindObjectsInactive.Include);

        if (existing != null)
        {
            // 비활성 부모 아래에 있어도 실행할 수 있도록 먼저 씬 루트로 분리한다.
            if (existing.transform.parent != null)
                existing.transform.SetParent(null, true);

            if (!existing.gameObject.activeSelf)
                existing.gameObject.SetActive(true);

            if (!existing.enabled)
                existing.enabled = true;

            existing.EnsurePersistentRoot();

            return Instance != null
                ? Instance
                : existing;
        }

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

        // DontDestroyOnLoad는 루트 GameObject에서만 정상 동작한다.
        // Hierarchy 정리 그룹 아래에 배치되어 있어도 런타임에는 자동으로 루트로 분리한다.
        EnsurePersistentRoot();

        Instance = this;

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

        if (IsRunning && LastSummary != null)
        {
            LastSummary.completedUtc = DateTime.UtcNow.ToString("O");
            LastSummary.Recalculate();
            PersistSummary("PLAY_MODE_STOPPED");
            AppendBatchEvent(
                "BATCH_ABORTED",
                "Play Mode stopped before the batch completed.");
        }

        BattleSimulationRuntime.EndBatch();

        RestoreRuntimeState();

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
        {
            panel?.SetStatus(
                LastSummary.ToDisplayString() +
                "\nJSON 저장 위치:\n" +
                LastOutputDirectory);
        }
        else
            panel?.SetStatus(
                "동적 로그: Play Mode 전투 시 자동 기록\n" +
                "승률 또는 피해량 버튼으로 AI 배치 분석 실행\n" +
                "JSON 저장 루트:\n" +
                OutputRootDirectory);
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
        EnsurePersistentRoot();

        if (!isActiveAndEnabled)
        {
            panel?.SetStatus(
                "배치 분석 Runner가 비활성 상태입니다.\n" +
                "BattleAnalysisRuntime 오브젝트를 활성화하세요.");
            return;
        }

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
                "File > Build Profiles에서 현재 전투 씬을 Scene List에 추가하세요.");
            return;
        }

        sourceSceneBuildIndex = scene.buildIndex;
        sourceSceneName = scene.name;
        stopRequested = false;

        // Play Mode 진입 후 아무 행동도 하지 않은 일반 BattleAnalysis 세션은
        // 배치 시작 전에 제거한다. 실제 전투 데이터가 있으면 보존한다.
        BattleDynamicAnalysisRecorder.PrepareForBatchSimulation();

        LastSummary = new BattleSimulationSummary
        {
            mode = mode.ToString(),
            scene = sourceSceneName,
            startedUtc = DateTime.UtcNow.ToString("O"),
            requestedRuns = Mathf.Max(1, runCount),
            detailedTraceDuringBatch = recordDetailedTraceDuringBatch
        };

        try
        {
            PrepareOutputDirectory(LastSummary);
            PersistSummary("RUNNING");
            AppendBatchEvent(
                "BATCH_STARTED",
                $"Mode={mode}, RequestedRuns={LastSummary.requestedRuns}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            panel?.SetStatus(
                "JSON 출력 폴더를 만들지 못했습니다.\n" +
                exception.Message + "\n" +
                "대상 경로:\n" + OutputRootDirectory);
            return;
        }

        panel?.SetStatus(
            $"{mode} 분석 준비 완료\n" +
            "JSON은 분석 시작 즉시 생성됩니다.\n" +
            "저장 위치:\n" + LastOutputDirectory);

        try
        {
            batchRoutine = StartCoroutine(
                RunBatch(mode, Mathf.Max(1, runCount)));

            if (batchRoutine == null)
            {
                panel?.SetStatus(
                    "배치 분석 Coroutine을 시작하지 못했습니다.");
            }
        }
        catch (Exception exception)
        {
            batchRoutine = null;
            Debug.LogException(exception, this);
            panel?.SetStatus(
                "배치 분석 시작 중 예외가 발생했습니다.\n" +
                exception.Message);
        }
    }

    private IEnumerator RunBatch(
        BattleSimulationMode mode,
        int runCount)
    {
        panel?.SetRunning(true);
        bool batchCompletedNormally = false;

        BattleSimulationRuntime.BeginBatch(
            suppressDetailedTrace: !recordDetailedTraceDuringBatch);

        savedLogFilter = Debug.unityLogger.filterLogType;
        logFilterCaptured = true;

        savedTimeScale = Time.timeScale;
        timeScaleCaptured = true;

        if (Time.timeScale <= 0f)
            Time.timeScale = 1f;

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

                AppendBatchEvent(
                    "RUN_STARTED",
                    $"Run={runIndex}",
                    runIndex);

                yield return LoadSourceScene();

                BattleSimulationRunResult result =
                    new BattleSimulationRunResult
                    {
                        runIndex = runIndex
                    };

                if (!lastSceneLoadSucceeded)
                {
                    FailRun(
                        result,
                        string.IsNullOrWhiteSpace(lastSceneLoadError)
                            ? "전투 씬 로드 실패"
                            : lastSceneLoadError);

                    LastSummary.runs.Add(result);
                    LastSummary.Recalculate();
                    AppendRunResult(result);
                    AppendBatchEvent(
                        "RUN_FAILED",
                        $"Run={runIndex}, Reason={result.failureMessage}",
                        runIndex);
                    PersistSummary("RUNNING");
                    break;
                }

                yield return RunCurrentSceneBattle(result);

                LastSummary.runs.Add(result);
                LastSummary.Recalculate();
                AppendRunResult(result);
                AppendBatchEvent(
                    "RUN_COMPLETED",
                    $"Run={runIndex}, Outcome={result.outcome}, " +
                    $"Turns={result.turns}, Dealt={result.playerDamageDealt}, " +
                    $"Taken={result.playerDamageTaken}",
                    runIndex);

                if (writeProgressAfterEveryRun)
                    PersistSummary("RUNNING");

                panel?.SetStatus(
                    $"{mode} 분석 중... {runIndex}/{runCount}\n" +
                    $"현재 승률 {LastSummary.winRate:0.0}%\n" +
                    $"평균 가한 피해 " +
                    $"{LastSummary.averagePlayerDamageDealt:0.0}");
            }

            batchCompletedNormally = true;
        }
        finally
        {
            UnsubscribeActiveBattle();
            BattleSimulationRuntime.EndBatch();
            RestoreRuntimeState();

            if (LastSummary != null)
            {
                LastSummary.completedUtc =
                    DateTime.UtcNow.ToString("O");
                LastSummary.Recalculate();

                string finalStatus = stopRequested
                    ? "STOPPED"
                    : batchCompletedNormally
                        ? "COMPLETED"
                        : "ABORTED";

                AppendBatchEvent(
                    finalStatus == "COMPLETED"
                        ? "BATCH_COMPLETED"
                        : "BATCH_ABORTED",
                    $"Status={finalStatus}, CompletedRuns={LastSummary.completedRuns}");
                PersistSummary(finalStatus);
            }

            batchRoutine = null;
            panel?.SetRunning(false);

            if (LastSummary != null)
            {
                panel?.SetStatus(
                    LastSummary.ToDisplayString() +
                    "\nJSON 저장 위치:\n" +
                    LastOutputDirectory);
            }

            if (openOutputFolderWhenComplete)
                OpenOutputFolder();
        }

        if (reloadCleanSceneAfterBatch &&
            sourceSceneBuildIndex >= 0)
        {
            // 이 최종 재로드는 분석용 전투가 아니라 사용자에게 깨끗한 씬을 돌려주기 위한 것이다.
            // 자동 BattleDynamicAnalysisRecorder가 이 짧은 전투를 INVALID 세션으로 남기지 않게 한 번만 억제한다.
            BattleSimulationRuntime.SuppressNextDynamicAnalysisSession();
            yield return LoadSourceScene();
        }
    }

    private IEnumerator LoadSourceScene()
    {
        EnsurePersistentRoot();

        lastSceneLoadSucceeded = false;
        lastSceneLoadError = string.Empty;

        AppendBatchEvent(
            "SCENE_LOAD_STARTED",
            $"BuildIndex={sourceSceneBuildIndex}");

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(
                sourceSceneBuildIndex,
                LoadSceneMode.Single);

        if (operation == null)
        {
            lastSceneLoadError =
                "SceneManager.LoadSceneAsync가 null을 반환했습니다.";
            yield break;
        }

        float startedAt = Time.realtimeSinceStartup;

        while (!operation.isDone)
        {
            if (Time.realtimeSinceStartup - startedAt >
                sceneLoadTimeoutSeconds)
            {
                lastSceneLoadError =
                    $"씬 로드가 {sceneLoadTimeoutSeconds:0.0}초를 초과했습니다.";
                AppendBatchEvent(
                    "SCENE_LOAD_TIMEOUT",
                    lastSceneLoadError);
                yield break;
            }

            yield return null;
        }

        // Awake/Start 및 한 프레임 지연 자동 시작을 기다린다.
        yield return null;
        yield return null;

        lastSceneLoadSucceeded = true;
        AppendBatchEvent(
            "SCENE_LOAD_COMPLETED",
            $"Scene={SceneManager.GetActiveScene().name}");
    }

    private IEnumerator RunCurrentSceneBattle(
        BattleSimulationRunResult result)
    {
        float readyStartedAt = Time.realtimeSinceStartup;

        while (true)
        {
            activeManager =
                FindFirstObjectByType<BattleManager>();

            if (activeManager != null)
            {
                if (!activeManager.IsInitialized)
                    activeManager.InitializeBattle();

                activeContext = activeManager.BattleContext;

                if (activeManager.IsInitialized &&
                    activeContext?.Player != null &&
                    activeManager.TurnManager != null)
                {
                    break;
                }
            }

            if (Time.realtimeSinceStartup - readyStartedAt >
                battleReadyTimeoutSeconds)
            {
                FailRun(
                    result,
                    $"BattleManager 준비가 {battleReadyTimeoutSeconds:0.0}초 안에 완료되지 않았습니다.");
                yield break;
            }

            yield return null;
        }

        result.playerMaxHp =
            activeContext.Player.MaxCombatHP;

        activeRun = result;
        SubscribeActiveBattle();

        if (!activeManager.TurnManager.IsBattleRunning)
            activeManager.StartBattle();

        int lastSubmittedTurn = -1;
        int lastObservedTurn = activeManager.TurnManager.CurrentTurn;
        bool lastObservedResolving = activeManager.TurnManager.IsResolving;
        float lastProgressAt = Time.realtimeSinceStartup;
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

            if (turnManager.CurrentTurn != lastObservedTurn ||
                turnManager.IsResolving != lastObservedResolving)
            {
                lastObservedTurn = turnManager.CurrentTurn;
                lastObservedResolving = turnManager.IsResolving;
                lastProgressAt = Time.realtimeSinceStartup;
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
                lastProgressAt = Time.realtimeSinceStartup;
            }

            if (Time.realtimeSinceStartup - lastProgressAt >
                noProgressTimeoutSeconds)
            {
                result.timedOut = true;
                result.outcome = "NO_PROGRESS_TIMEOUT";
                result.failureMessage =
                    $"전투 상태가 {noProgressTimeoutSeconds:0.0}초 동안 진행되지 않았습니다.";
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

        result.playerNetHpLoss = Mathf.Max(
            0,
            result.playerMaxHp - result.playerRemainingHp);

        result.playerEstimatedHealingReceived = Mathf.Max(
            0,
            result.playerCharacterHpDamageTaken -
            result.playerNetHpLoss);

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
        battleEvent.OnCombatResourceChanged += HandleResourceChanged;
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
            battleEvent.OnCombatResourceChanged -= HandleResourceChanged;
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

        int displayDamage = damage.GetDisplayDamage();
        int characterHpDamage = Mathf.Max(0, damage.FinalHpDamage);
        int partDamage = Mathf.Max(0, damage.PartHpDamage);
        int directHpDamage = Mathf.Max(0, damage.DirectHpDamage);
        Character player = activeContext.Player;

        if (damage.Attacker == player &&
            damage.Target != player)
        {
            activeRun.playerDamageDealt += displayDamage;
            activeRun.playerCharacterHpDamageDealt += characterHpDamage;
            activeRun.playerPartDamageDealt += partDamage;
            activeRun.playerDirectHpDamageDealt += directHpDamage;
        }

        if (damage.Target == player)
        {
            activeRun.playerDamageTaken += displayDamage;
            activeRun.playerCharacterHpDamageTaken += characterHpDamage;
            activeRun.playerPartDamageTaken += partDamage;
            activeRun.playerDirectHpDamageTaken += directHpDamage;
        }
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

    private void HandleResourceChanged(
        CombatResourceChangeContext change)
    {
        if (activeRun == null ||
            activeContext == null ||
            change == null ||
            change.ResourceKey != CombatResourceKeys.Energy ||
            change.Delta >= 0)
        {
            return;
        }

        int spent = -change.Delta;

        if (change.Owner == activeContext.Player)
            activeRun.playerEnergySpent += spent;
        else
            activeRun.enemyEnergySpent += spent;
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
            FindFirstObjectByType<BattleAnalysisDebugPanel>(
                FindObjectsInactive.Include);

        if (found != null)
            found.SetRunning(IsRunning);

        if (found != null)
            RegisterPanel(found);
    }

    private void EnsurePersistentRoot()
    {
        if (transform.parent != null)
            transform.SetParent(null, true);

        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
    }

    private void RestoreRuntimeState()
    {
        if (logFilterCaptured)
        {
            Debug.unityLogger.filterLogType =
                savedLogFilter;

            logFilterCaptured = false;
        }

        if (timeScaleCaptured)
        {
            Time.timeScale = savedTimeScale;
            timeScaleCaptured = false;
        }
    }

    public void OpenOutputFolder()
    {
        string directory =
            !string.IsNullOrWhiteSpace(LastOutputDirectory) &&
            Directory.Exists(LastOutputDirectory)
                ? LastOutputDirectory
                : OutputRootDirectory;

        try
        {
            Directory.CreateDirectory(directory);
            Application.OpenURL(
                new Uri(directory).AbsoluteUri);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            panel?.SetStatus(
                "출력 폴더를 열지 못했습니다.\n" +
                directory + "\n" + exception.Message);
        }
    }

    private void PrepareOutputDirectory(
        BattleSimulationSummary summary)
    {
        string timestamp =
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");

        string sessionName =
            timestamp + "_" + summary.mode + "_" +
            Guid.NewGuid().ToString("N").Substring(0, 8);

        LastOutputDirectory = Path.Combine(
            OutputRootDirectory,
            sessionName);

        Directory.CreateDirectory(LastOutputDirectory);

        runsJsonlPath = Path.Combine(
            LastOutputDirectory,
            "runs.jsonl");

        batchEventsJsonlPath = Path.Combine(
            LastOutputDirectory,
            "batch_events.jsonl");

        File.WriteAllText(
            runsJsonlPath,
            string.Empty,
            new UTF8Encoding(false));

        File.WriteAllText(
            batchEventsJsonlPath,
            string.Empty,
            new UTF8Encoding(false));

        summary.outputDirectory = LastOutputDirectory;
        nextBatchEventSequence = 1;

        File.WriteAllText(
            Path.Combine(LastOutputDirectory, "output_location.txt"),
            LastOutputDirectory,
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(
                OutputRootDirectory,
                "latest_simulation.txt"),
            LastOutputDirectory,
            new UTF8Encoding(false));

        Debug.Log(
            "[BattleSimulation] JSON output created: " +
            LastOutputDirectory,
            this);
    }

    private void AppendRunResult(
        BattleSimulationRunResult result)
    {
        if (result == null ||
            string.IsNullOrWhiteSpace(runsJsonlPath))
        {
            return;
        }

        try
        {
            File.AppendAllText(
                runsJsonlPath,
                JsonUtility.ToJson(result) + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void AppendBatchEvent(
        string eventType,
        string message,
        int runIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(batchEventsJsonlPath))
            return;

        BattleSimulationBatchEvent record =
            new BattleSimulationBatchEvent
            {
                sequence = nextBatchEventSequence++,
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                eventType = eventType,
                mode = LastSummary?.mode,
                runIndex = runIndex,
                completedRuns = LastSummary?.completedRuns ?? 0,
                requestedRuns = LastSummary?.requestedRuns ?? 0,
                message = message
            };

        try
        {
            File.AppendAllText(
                batchEventsJsonlPath,
                JsonUtility.ToJson(record) + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void PersistSummary(string status)
    {
        if (LastSummary == null ||
            string.IsNullOrWhiteSpace(LastOutputDirectory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(LastOutputDirectory);

            LastSummary.status = status;
            LastSummary.outputDirectory = LastOutputDirectory;
            LastSummary.Recalculate();

            File.WriteAllText(
                Path.Combine(LastOutputDirectory, "summary.json"),
                JsonUtility.ToJson(LastSummary, true),
                new UTF8Encoding(false));

            File.WriteAllText(
                Path.Combine(LastOutputDirectory, "summary.txt"),
                LastSummary.ToDisplayString() +
                Environment.NewLine +
                "상태: " + status +
                Environment.NewLine +
                "출력 위치: " + LastOutputDirectory,
                new UTF8Encoding(false));

            File.WriteAllText(
                Path.Combine(
                    OutputRootDirectory,
                    "latest_simulation.txt"),
                LastOutputDirectory,
                new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            panel?.SetStatus(
                "JSON 저장 실패\n" +
                exception.Message + "\n" +
                "대상 경로:\n" + LastOutputDirectory);
        }
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