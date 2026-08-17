using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class CharacterVerificationAutoBattleResult
{
    public bool CompletedNormally;
    public bool PlayerWon;
    public bool PlayerLost;
    public bool TimedOut;
    public bool TurnLimitReached;
    public bool AnalysisTurnBudgetReached;
    public bool UsedFallbackPlanner;

    public int Turns;
    public int WinRatePlanAttempts;
    public int SuccessfulWinRatePlans;
    public int FallbackPlanCount;
    public int PlannedActionCount;
    public int PlayerRuntimeSkillCount;
    public int RuntimeErrorCount;
    public int RuntimeWarningCount;
    public int CriticalInvariantCount;
    public int BlockedDamageAttemptCount;
    public int MaxConsecutiveNoStateProgressTurns;

    public float RequestedTimeScale;
    public long ElapsedMilliseconds;

    public string Outcome;
    public string FailureMessage;
    public string PlayerName;

    public readonly List<string> ParticipantNames =
        new List<string>();

    public readonly List<string> ParticipantIdentities =
        new List<string>();

    public readonly List<string> UsedPlayerSkills =
        new List<string>();

    public readonly List<string> RuntimeProblemSamples =
        new List<string>();

    public readonly List<string> WarningSamples =
        new List<string>();

    public string BuildDetails()
    {
        StringBuilder builder =
            new StringBuilder();

        string outcome =
            string.IsNullOrWhiteSpace(Outcome)
                ? "UNKNOWN"
                : Outcome;

        builder.AppendLine(
            $"Outcome={outcome}");

        builder.AppendLine(
            $"Turns={Turns}");

        builder.AppendLine(
            $"AnalysisTurnBudgetReached={AnalysisTurnBudgetReached}");

        builder.AppendLine(
            $"BlockedDamageAttempts={BlockedDamageAttemptCount}");

        builder.AppendLine(
            $"MaxConsecutiveNoStateProgressTurns={MaxConsecutiveNoStateProgressTurns}");

        builder.AppendLine(
            $"WinRatePlanAttempts={WinRatePlanAttempts}");

        builder.AppendLine(
            $"SuccessfulWinRatePlans={SuccessfulWinRatePlans}");

        builder.AppendLine(
            $"FallbackPlanCount={FallbackPlanCount}");

        builder.AppendLine(
            $"PlannedActionCount={PlannedActionCount}");

        builder.AppendLine(
            $"PlayerSkillUsage={UsedPlayerSkills.Count}/{PlayerRuntimeSkillCount}");

        builder.AppendLine(
            "UsedPlayerSkills=" +
            (UsedPlayerSkills.Count == 0
                ? "NONE"
                : string.Join(", ", UsedPlayerSkills)));

        builder.AppendLine(
            $"RuntimeErrors={RuntimeErrorCount}");

        builder.AppendLine(
            $"RuntimeWarnings={RuntimeWarningCount}");

        builder.AppendLine(
            $"CriticalInvariants={CriticalInvariantCount}");

        builder.AppendLine(
            $"RequestedTimeScale={RequestedTimeScale:0.##}");

        builder.AppendLine(
            $"ElapsedMilliseconds={ElapsedMilliseconds}");

        builder.AppendLine(
            "Participants=" +
            (ParticipantNames.Count == 0
                ? "NONE"
                : string.Join(", ", ParticipantNames)));

        if (RuntimeProblemSamples.Count > 0)
        {
            builder.AppendLine(
                "RuntimeProblemSamples=" +
                string.Join(" || ", RuntimeProblemSamples));
        }

        if (WarningSamples.Count > 0)
        {
            builder.AppendLine(
                "WarningSamples=" +
                string.Join(" || ", WarningSamples));
        }

        if (!string.IsNullOrWhiteSpace(FailureMessage))
        {
            builder.AppendLine(
                $"Failure={FailureMessage}");
        }

        return builder.ToString().TrimEnd();
    }
}

/// <summary>
/// Character Verification의 Live Scene 분석을 실제 한 판 전투로 확장한다.
/// 매 턴 BattleAutoPlanButtonPanel의 승률 자동 지정과 같은 경로를 호출하고,
/// 계획이 완성되면 BattleManager.NextTurn()을 호출해 전투 종료까지 반복한다.
///
/// 화면 위치·카메라·Timeline을 직접 조작하지 않으며,
/// 분석 중에만 Time.timeScale을 높였다가 반드시 원래 값으로 복구한다.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class CharacterVerificationAutoBattleDriver :
    MonoBehaviour
{
    private const float DefaultAnalysisTimeScale = 30f;

    // Character Verification은 밸런스 시뮬레이션이 아니라 실전 파이프라인
    // 스모크 테스트다. 승패가 늦게 나는 조합에서도 결과 리포트를 항상
    // 생성하도록 실제 전투 구간은 5턴으로 제한한다.
    private const int DefaultAnalysisTurnBudget = 5;

    // TurnBudget 완료 이벤트가 유실되거나 단일 턴이 비정상적으로 반복되는
    // 경우를 막는 별도의 하드 세이프티 상한이다.
    private const int DefaultMaximumTurns = 12;

    private const float DefaultReadyTimeoutSeconds = 12f;
    private const float DefaultNoProgressTimeoutSeconds = 30f;
    private const float DefaultPresentationNoProgressTimeoutSeconds = 90f;
    private const float DefaultOverallTimeoutSeconds = 180f;

    public static CharacterVerificationAutoBattleDriver Active
    {
        get;
        private set;
    }

    public static bool TryStart(
        BattleManager manager,
        Action<CharacterVerificationAutoBattleResult> completed,
        out string failure,
        float analysisTimeScale = DefaultAnalysisTimeScale,
        int maximumTurns = DefaultMaximumTurns)
    {
        failure = string.Empty;

        if (Active != null)
        {
            failure =
                "Character Verification 자동 전투가 이미 실행 중입니다.";

            return false;
        }

        if (manager == null)
        {
            failure =
                "BattleManager를 찾지 못했습니다.";

            return false;
        }

        if (BattleBatchSimulationRunner.Instance != null &&
            BattleBatchSimulationRunner.Instance.IsRunning)
        {
            failure =
                "BattleBatchSimulationRunner가 실행 중이어서 " +
                "Character Verification 자동 전투를 동시에 시작할 수 없습니다.";

            return false;
        }

        GameObject root =
            new GameObject(
                "CharacterVerificationAutoBattle");

        CharacterVerificationAutoBattleDriver driver =
            root.AddComponent<
                CharacterVerificationAutoBattleDriver>();

        driver.Configure(
            manager,
            completed,
            Mathf.Max(1f, analysisTimeScale),
            Mathf.Max(1, maximumTurns));

        return true;
    }

    private readonly PlayerAutoPlanService winRatePlanner =
        new PlayerAutoPlanService();

    private readonly BattleAverageAIPlanner fallbackPlanner =
        new BattleAverageAIPlanner();

    private BattleManager manager;
    private BattleContext context;
    private Character player;
    private BattleAutoPlanButtonPanel autoPlanPanel;
    private BattleAnimationDirector animationDirector;
    private BattleClashRollPresentationUI clashRollPresentation;

    private Action<CharacterVerificationAutoBattleResult>
        completionCallback;

    private CharacterVerificationAutoBattleResult result;

    private float requestedTimeScale;
    private int maximumTurns;
    private int analysisTurnBudget;
    private int lastSubmittedTurn = -1;
    private int lastCompletedTurn;
    private int lastObservedTurn = -1;
    private bool lastObservedResolving;
    private bool battleObservedRunning;
    private bool configured;
    private bool completed;
    private bool isFinishing;

    private float savedTimeScale = 1f;
    private float startedAt;
    private float lastProgressAt;
    private bool logCaptureBound;
    private bool battleEventCaptureBound;
    private BattleEvent observedBattleEvent;

    private bool lastObservedAnimationPlaying;
    private bool lastObservedResolutionPresentation;
    private bool lastObservedRollVisible;
    private string lastProgressStage = "NotStarted";

    private string lastCombatFingerprint = string.Empty;
    private bool hasCombatFingerprint;
    private int consecutiveNoStateProgressTurns;

    private void Configure(
        BattleManager battleManager,
        Action<CharacterVerificationAutoBattleResult> callback,
        float analysisTimeScale,
        int maxTurns)
    {
        manager = battleManager;
        context = battleManager?.BattleContext;
        player = context?.Player;
        completionCallback = callback;
        requestedTimeScale = analysisTimeScale;
        maximumTurns = maxTurns;
        analysisTurnBudget =
            Mathf.Clamp(
                DefaultAnalysisTurnBudget,
                1,
                maximumTurns);

        result =
            new CharacterVerificationAutoBattleResult
            {
                RequestedTimeScale = analysisTimeScale,
                PlayerName = DescribeCharacter(player),
                PlayerRuntimeSkillCount =
                    CountUniqueRuntimeSkills(
                        player?.RuntimeSkills)
            };

        CaptureParticipants();
        BindRuntimeObservation();

        autoPlanPanel =
            FindFirstObjectByType<
                BattleAutoPlanButtonPanel>(
                    FindObjectsInactive.Include);

        ResolvePresentationReferences();

        savedTimeScale = Time.timeScale;
        Time.timeScale =
            Mathf.Max(
                1f,
                analysisTimeScale);

        startedAt =
            Time.realtimeSinceStartup;

        lastProgressAt = startedAt;
        lastProgressStage = "Configured";
        CaptureCombatFingerprint(
            resetStallCounter: true);
        configured = true;
        Active = this;

        Debug.Log(
            "[CharacterVerification][AutoBattle] 시작 / " +
            $"Player={result.PlayerName}, " +
            $"TimeScale={Time.timeScale:0.##}, " +
            $"TurnBudget={analysisTurnBudget}, " +
            $"HardMaxTurns={maximumTurns}",
            this);
    }

    private void Update()
    {
        if (!configured ||
            completed ||
            isFinishing)
        {
            return;
        }

        if (manager == null)
        {
            FinishAsError(
                "자동 전투 도중 BattleManager가 사라졌습니다.");

            return;
        }

        if (BattleBatchSimulationRunner.Instance != null &&
            BattleBatchSimulationRunner.Instance.IsRunning)
        {
            FinishAsError(
                "실행 도중 BattleBatchSimulationRunner가 시작되어 " +
                "자동 전투를 중단했습니다.");

            return;
        }

        context ??= manager.BattleContext;
        player ??= context?.Player;

        if (!battleEventCaptureBound ||
            !ReferenceEquals(
                observedBattleEvent,
                context?._battleEvent))
        {
            BindRuntimeObservation();
        }

        ResolvePresentationReferences();
        ObservePresentationState();

        if (result.PlayerRuntimeSkillCount == 0)
        {
            result.PlayerRuntimeSkillCount =
                CountUniqueRuntimeSkills(
                    player?.RuntimeSkills);
        }

        TurnManager turnManager =
            manager.TurnManager;

        if (!manager.IsInitialized ||
            turnManager == null ||
            context == null ||
            player == null)
        {
            if (Time.realtimeSinceStartup - startedAt >
                DefaultReadyTimeoutSeconds)
            {
                FinishAsError(
                    "전투 준비가 제한 시간 안에 완료되지 않았습니다.");
            }

            return;
        }

        if (manager.IsEndingOrEnded)
        {
            FinishFromBattleState();
            return;
        }

        if (!turnManager.IsBattleRunning)
        {
            if (battleObservedRunning)
            {
                FinishFromBattleState();
            }
            else if (Time.realtimeSinceStartup - startedAt >
                     DefaultReadyTimeoutSeconds)
            {
                FinishAsError(
                    "TurnManager가 전투 실행 상태로 진입하지 않았습니다.");
            }

            return;
        }

        battleObservedRunning = true;

        if (turnManager.CurrentTurn != lastObservedTurn ||
            turnManager.IsResolving != lastObservedResolving)
        {
            lastObservedTurn =
                turnManager.CurrentTurn;

            lastObservedResolving =
                turnManager.IsResolving;

            MarkProgress(
                $"TurnState:{turnManager.CurrentTurn}/" +
                $"Resolving={turnManager.IsResolving}");
        }

        if (Time.realtimeSinceStartup - startedAt >
            DefaultOverallTimeoutSeconds)
        {
            result.TimedOut = true;
            result.Outcome = "OVERALL_TIMEOUT";
            result.FailureMessage =
                "Character Verification 실전 분석이 전체 제한 시간 " +
                $"{DefaultOverallTimeoutSeconds:0}초를 초과했습니다. " +
                $"Stage={lastProgressStage}, " +
                $"Turn={turnManager.CurrentTurn}, " +
                $"CompletedTurns={lastCompletedTurn}";

            CancelPresentationAndEndBattle();
            Finish(forceNormalCompletion: false);
            return;
        }

        if (lastCompletedTurn >=
            analysisTurnBudget)
        {
            CompleteTurnBudget();
            return;
        }

        if (turnManager.CurrentTurn > maximumTurns)
        {
            result.TurnLimitReached = true;
            result.Outcome = "HARD_TURN_LIMIT";
            result.FailureMessage =
                "검증 전투가 하드 세이프티 상한 " +
                $"{maximumTurns}턴을 초과했습니다.";

            CancelPresentationAndEndBattle();
            Finish(forceNormalCompletion: false);
            return;
        }

        if (!turnManager.IsResolving &&
            turnManager.CurrentTurn != lastSubmittedTurn)
        {
            SubmitWinRateTurn(turnManager);
        }

        bool presentationActive =
            IsPresentationActive();

        float noProgressTimeout =
            presentationActive
                ? DefaultPresentationNoProgressTimeoutSeconds
                : DefaultNoProgressTimeoutSeconds;

        if (!completed &&
            Time.realtimeSinceStartup - lastProgressAt >
            noProgressTimeout)
        {
            result.TimedOut = true;
            result.Outcome = "NO_PROGRESS_TIMEOUT";
            result.FailureMessage =
                "전투 진행 신호가 " +
                $"{noProgressTimeout:0}초 동안 갱신되지 않았습니다. " +
                $"Stage={lastProgressStage}, " +
                $"Turn={turnManager.CurrentTurn}, " +
                $"Resolving={turnManager.IsResolving}, " +
                $"PresentationActive={presentationActive}";

            CancelPresentationAndEndBattle();
            Finish(forceNormalCompletion: false);
        }
    }

    private void SubmitWinRateTurn(
        TurnManager turnManager)
    {
        result.WinRatePlanAttempts++;

        PlayerAutoPlanResult planResult =
            autoPlanPanel != null
                ? autoPlanPanel
                    .ApplyWinRatePlanForAutomation()
                : winRatePlanner.BuildAndApply(
                    manager,
                    PlayerAutoPlanMode.WinRate);

        int planned =
            planResult?.Success == true
                ? planResult.PlannedSlotCount
                : 0;

        if (planned > 0)
        {
            result.SuccessfulWinRatePlans++;
        }
        else
        {
            planned =
                fallbackPlanner.PlanPlayer(
                    manager);

            if (planned <= 0)
            {
                result.Outcome =
                    "PLAYER_PLAN_FAILED";

                result.FailureMessage =
                    "승률 자동 지정과 평균 AI 폴백이 모두 " +
                    "플레이어 행동을 만들지 못했습니다. " +
                    (planResult?.Message ?? "원인 미상");

                CancelPresentationAndEndBattle();
                Finish(forceNormalCompletion: false);
                return;
            }

            result.UsedFallbackPlanner = true;
            result.FallbackPlanCount++;

            string primaryFailure =
                string.IsNullOrWhiteSpace(planResult?.Message)
                    ? "NONE"
                    : planResult.Message;

            Debug.LogWarning(
                "[CharacterVerification][AutoBattle] " +
                "승률 계획 실패로 현재 턴만 평균 AI 폴백 / " +
                $"Turn={turnManager.CurrentTurn}, " +
                $"Slots={planned}, " +
                $"Primary={primaryFailure}",
                this);
        }

        result.PlannedActionCount += planned;
        lastSubmittedTurn = turnManager.CurrentTurn;
        MarkProgress(
            $"TurnPlanSubmitted:{turnManager.CurrentTurn}");

        Debug.Log(
            "[CharacterVerification][AutoBattle] " +
            "승률 자동 지정 후 턴 진행 / " +
            $"Turn={turnManager.CurrentTurn}, " +
            $"Slots={planned}, " +
            $"Fallback={result.FallbackPlanCount}",
            this);

        manager.NextTurn();
    }

    private void CompleteTurnBudget()
    {
        if (completed || isFinishing)
            return;

        result.AnalysisTurnBudgetReached = true;
        result.Outcome = "TURN_BUDGET_COMPLETE";
        result.FailureMessage = string.Empty;

        Debug.Log(
            "[CharacterVerification][AutoBattle] " +
            "실전 스모크 전투 예산 완료 / " +
            $"Turns={lastCompletedTurn}/{analysisTurnBudget}, " +
            $"BlockedDamage={result.BlockedDamageAttemptCount}, " +
            "MaxNoStateProgressTurns=" +
            $"{result.MaxConsecutiveNoStateProgressTurns}",
            this);

        CancelPresentationAndEndBattle();
        Finish(forceNormalCompletion: true);
    }

    private void CaptureCombatFingerprint(
        bool resetStallCounter)
    {
        string fingerprint =
            BuildCombatFingerprint();

        if (resetStallCounter ||
            !hasCombatFingerprint)
        {
            lastCombatFingerprint =
                fingerprint;
            hasCombatFingerprint = true;
            consecutiveNoStateProgressTurns = 0;
            return;
        }

        if (string.Equals(
                lastCombatFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            consecutiveNoStateProgressTurns++;

            result.MaxConsecutiveNoStateProgressTurns =
                Mathf.Max(
                    result.MaxConsecutiveNoStateProgressTurns,
                    consecutiveNoStateProgressTurns);
        }
        else
        {
            consecutiveNoStateProgressTurns = 0;
        }

        lastCombatFingerprint = fingerprint;
    }

    private string BuildCombatFingerprint()
    {
        StringBuilder builder =
            new StringBuilder();

        AppendCharacterFingerprint(
            builder,
            player);

        if (context?.Enemies != null)
        {
            for (int i = 0;
                 i < context.Enemies.Count;
                 i++)
            {
                AppendCharacterFingerprint(
                    builder,
                    context.Enemies[i]);
            }
        }

        return builder.ToString();
    }

    private static void AppendCharacterFingerprint(
        StringBuilder builder,
        Character character)
    {
        if (builder == null)
            return;

        if (character == null)
        {
            builder.Append("NULL;");
            return;
        }

        builder
            .Append(character.GetInstanceID())
            .Append(':')
            .Append(character.IsDead ? 1 : 0)
            .Append(':')
            .Append(character.CurrentHP)
            .Append('|');

        IReadOnlyList<BodyPart> parts =
            character.BodyParts;

        if (parts != null)
        {
            for (int i = 0;
                 i < parts.Count;
                 i++)
            {
                BodyPart part =
                    parts[i];

                if (part == null)
                {
                    builder.Append("PNULL,");
                    continue;
                }

                builder
                    .Append((int)part.Type)
                    .Append(':')
                    .Append((int)part.State)
                    .Append(':')
                    .Append(
                        Mathf.CeilToInt(
                            part.PartHP))
                    .Append(',');
            }
        }

        builder.Append(';');
    }

    private void FinishFromBattleState()
    {
        bool playerDead =
            player == null ||
            player.IsDead;

        bool anyEnemyAlive = false;

        if (context?.Enemies != null)
        {
            foreach (Character enemy in context.Enemies)
            {
                if (enemy != null && !enemy.IsDead)
                {
                    anyEnemyAlive = true;
                    break;
                }
            }
        }

        result.PlayerLost = playerDead;
        result.PlayerWon =
            !playerDead &&
            !anyEnemyAlive;

        if (result.PlayerWon)
        {
            result.Outcome = "VICTORY";
            Finish(forceNormalCompletion: true);
            return;
        }

        if (result.PlayerLost)
        {
            result.Outcome = "DEFEAT";
            Finish(forceNormalCompletion: true);
            return;
        }

        result.Outcome = "ABORTED";
        result.FailureMessage =
            "승패 조건이 충족되지 않은 상태에서 전투가 종료되었습니다.";

        Finish(forceNormalCompletion: false);
    }

    private void FinishAsError(
        string message)
    {
        result.Outcome = "ERROR";
        result.FailureMessage = message;
        CancelPresentationAndEndBattle();
        Finish(forceNormalCompletion: false);
    }

    private void Finish(
        bool forceNormalCompletion)
    {
        if (completed)
            return;

        completed = true;

        TurnManager turnManager =
            manager?.TurnManager;

        result.Turns =
            Mathf.Max(
                0,
                Mathf.Max(
                    lastCompletedTurn,
                    Mathf.Max(
                        lastSubmittedTurn,
                        (turnManager?.CurrentTurn ?? 1) - 1)));

        bool hasCriticalRuntimeProblem =
            result.RuntimeErrorCount > 0 ||
            result.CriticalInvariantCount > 0;

        if (forceNormalCompletion &&
            hasCriticalRuntimeProblem)
        {
            string terminalOutcome =
                string.IsNullOrWhiteSpace(result.Outcome)
                    ? "COMPLETED"
                    : result.Outcome;

            result.Outcome =
                terminalOutcome +
                "_WITH_RUNTIME_ERRORS";

            result.FailureMessage =
                "전투는 승패까지 진행됐지만 실행 중 오류 또는 " +
                "전투 타깃 불변식 위반이 감지되었습니다.";
        }

        result.CompletedNormally =
            forceNormalCompletion &&
            !result.TimedOut &&
            !result.TurnLimitReached &&
            !hasCriticalRuntimeProblem &&
            string.IsNullOrWhiteSpace(
                result.FailureMessage);

        result.ElapsedMilliseconds =
            (long)Math.Round(
                (Time.realtimeSinceStartup - startedAt) *
                1000d);

        RestoreTimeScale();
        UnbindRuntimeObservation();

        Debug.Log(
            "[CharacterVerification][AutoBattle] 완료 / " +
            $"Outcome={result.Outcome}, " +
            $"Turns={result.Turns}, " +
            $"Plans={result.SuccessfulWinRatePlans}/" +
            $"{result.WinRatePlanAttempts}, " +
            $"Fallback={result.FallbackPlanCount}, " +
            $"Elapsed={result.ElapsedMilliseconds}ms",
            this);

        Action<CharacterVerificationAutoBattleResult>
            callback = completionCallback;

        completionCallback = null;

        try
        {
            callback?.Invoke(result);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (Active == this)
            Active = null;

        Destroy(gameObject);
    }

    private void CaptureParticipants()
    {
        result.ParticipantNames.Clear();
        result.ParticipantIdentities.Clear();

        AddParticipant(player);

        if (context?.Enemies == null)
            return;

        foreach (Character enemy in context.Enemies)
            AddParticipant(enemy);
    }

    private void AddParticipant(
        Character character)
    {
        if (character == null)
            return;

        AddUnique(
            result.ParticipantNames,
            DescribeCharacter(character));

        AddUnique(
            result.ParticipantIdentities,
            DescribeCharacter(character));

        AddUnique(
            result.ParticipantIdentities,
            character.Data?.name);

        AddUnique(
            result.ParticipantIdentities,
            character.name);
    }

    private static void AddUnique(
        IList<string> destination,
        string value)
    {
        if (destination == null ||
            string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string normalized = value.Trim();

        for (int i = 0; i < destination.Count; i++)
        {
            if (string.Equals(
                    destination[i],
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        destination.Add(normalized);
    }

    private static string DescribeCharacter(
        Character character)
    {
        return character?.Data?.CharacterName ??
               character?.name ??
               "Unknown Character";
    }

    private void BindRuntimeObservation()
    {
        if (!logCaptureBound)
        {
            Application.logMessageReceived +=
                HandleRuntimeLog;

            logCaptureBound = true;
        }

        BattleEvent currentEvent =
            context?._battleEvent;

        if (ReferenceEquals(
                observedBattleEvent,
                currentEvent) &&
            battleEventCaptureBound)
        {
            return;
        }

        UnbindBattleProgressEvents();

        observedBattleEvent = currentEvent;

        if (observedBattleEvent == null)
            return;

        observedBattleEvent.OnBattleStarted +=
            HandleBattleStarted;
        observedBattleEvent.OnBattleEnded +=
            HandleBattleEnded;
        observedBattleEvent.OnTurnStart +=
            HandleTurnStart;
        observedBattleEvent.OnTurnEnd +=
            HandleTurnEnd;
        observedBattleEvent.OnActionStart +=
            HandleActionStart;
        observedBattleEvent.OnActionEnd +=
            HandleActionEnd;
        observedBattleEvent.OnClashStart +=
            HandleClashStart;
        observedBattleEvent.OnExchangeResolved +=
            HandleExchangeResolved;
        observedBattleEvent.OnClashResolved +=
            HandleClashResolved;
        observedBattleEvent.OnDamageResolved +=
            HandleDamageResolved;
        observedBattleEvent.OnDamageEventResolved +=
            HandleDamageEventResolved;
        observedBattleEvent.OnStatusTicked +=
            HandleStatusTicked;
        observedBattleEvent.OnBodyPartWeakenResolved +=
            HandleBodyPartWeakened;
        observedBattleEvent.OnBodyPartBreakResolved +=
            HandleBodyPartBroken;
        observedBattleEvent.OnCharacterDeathResolved +=
            HandleCharacterDeath;
        observedBattleEvent.OnKillResolved +=
            HandleKill;

        battleEventCaptureBound = true;
        MarkProgress("BattleEventBound");
    }

    private void UnbindRuntimeObservation()
    {
        if (logCaptureBound)
        {
            Application.logMessageReceived -=
                HandleRuntimeLog;

            logCaptureBound = false;
        }

        UnbindBattleProgressEvents();
    }

    private void UnbindBattleProgressEvents()
    {
        if (battleEventCaptureBound &&
            observedBattleEvent != null)
        {
            observedBattleEvent.OnBattleStarted -=
                HandleBattleStarted;
            observedBattleEvent.OnBattleEnded -=
                HandleBattleEnded;
            observedBattleEvent.OnTurnStart -=
                HandleTurnStart;
            observedBattleEvent.OnTurnEnd -=
                HandleTurnEnd;
            observedBattleEvent.OnActionStart -=
                HandleActionStart;
            observedBattleEvent.OnActionEnd -=
                HandleActionEnd;
            observedBattleEvent.OnClashStart -=
                HandleClashStart;
            observedBattleEvent.OnExchangeResolved -=
                HandleExchangeResolved;
            observedBattleEvent.OnClashResolved -=
                HandleClashResolved;
            observedBattleEvent.OnDamageResolved -=
                HandleDamageResolved;
            observedBattleEvent.OnDamageEventResolved -=
                HandleDamageEventResolved;
            observedBattleEvent.OnStatusTicked -=
                HandleStatusTicked;
            observedBattleEvent.OnBodyPartWeakenResolved -=
                HandleBodyPartWeakened;
            observedBattleEvent.OnBodyPartBreakResolved -=
                HandleBodyPartBroken;
            observedBattleEvent.OnCharacterDeathResolved -=
                HandleCharacterDeath;
            observedBattleEvent.OnKillResolved -=
                HandleKill;
        }

        battleEventCaptureBound = false;
        observedBattleEvent = null;
    }

    private void HandleBattleStarted()
    {
        CaptureCombatFingerprint(
            resetStallCounter: true);

        MarkProgress("BattleStarted");
    }

    private void HandleBattleEnded()
    {
        MarkProgress("BattleEnded");
    }

    private void HandleTurnStart(
        int turn)
    {
        MarkProgress($"TurnStart:{turn}");
    }

    private void HandleTurnEnd(
        int turn)
    {
        lastCompletedTurn =
            Mathf.Max(
                lastCompletedTurn,
                turn);

        CaptureCombatFingerprint(
            resetStallCounter: false);

        MarkProgress($"TurnEnd:{turn}");
    }

    private void HandleActionStart(
        BattleAction action)
    {
        MarkProgress(
            $"ActionStart:{action?.ActionId ?? 0}");

        if (completed ||
            action?.Owner != player ||
            action.Skill == null)
        {
            return;
        }

        string skillId =
            action.Skill.Definition?.SkillId;

        string label =
            string.IsNullOrWhiteSpace(skillId)
                ? action.Skill.SkillName ??
                  action.Skill.GetType().Name
                : $"{action.Skill.SkillName ?? skillId}" +
                  $"<{skillId}>";

        AddUnique(
            result.UsedPlayerSkills,
            label);
    }

    private void HandleActionEnd(
        BattleAction action)
    {
        MarkProgress(
            $"ActionEnd:{action?.ActionId ?? 0}");
    }

    private void HandleClashStart(
        Character attacker,
        Character defender)
    {
        MarkProgress(
            $"ClashStart:{DescribeCharacter(attacker)}->" +
            DescribeCharacter(defender));
    }

    private void HandleExchangeResolved(
        ClashExchangeResult exchange)
    {
        MarkProgress(
            $"ExchangeResolved:{exchange?.ExchangeIndex ?? -1}");
    }

    private void HandleClashResolved(
        ClashResultContext clash)
    {
        MarkProgress("ClashResolved");
    }

    private void HandleDamageResolved(
        DamageContext damage)
    {
        MarkProgress("DamageResolved");
    }

    private void HandleDamageEventResolved(
        DamageEventResult damage)
    {
        MarkProgress("DamageEventResolved");
    }

    private void HandleStatusTicked(
        StatusEffectTickContext status)
    {
        MarkProgress("StatusTicked");
    }

    private void HandleBodyPartWeakened(
        BodyPartWeakenEventContext bodyPart)
    {
        MarkProgress("BodyPartWeakened");
    }

    private void HandleBodyPartBroken(
        BodyPartBreakEventContext bodyPart)
    {
        MarkProgress("BodyPartBroken");
    }

    private void HandleCharacterDeath(
        KillEventContext death)
    {
        MarkProgress("CharacterDeath");
    }

    private void HandleKill(
        KillEventContext kill)
    {
        MarkProgress("KillResolved");
    }

    private void MarkProgress(
        string stage)
    {
        if (completed)
            return;

        lastProgressAt =
            Time.realtimeSinceStartup;

        if (!string.IsNullOrWhiteSpace(stage))
            lastProgressStage = stage;
    }

    private void ResolvePresentationReferences()
    {
        if (animationDirector == null)
        {
            animationDirector =
                FindFirstObjectByType<BattleAnimationDirector>(
                    FindObjectsInactive.Include);
        }

        if (clashRollPresentation == null)
        {
            clashRollPresentation =
                FindFirstObjectByType<BattleClashRollPresentationUI>(
                    FindObjectsInactive.Include);
        }
    }

    private void ObservePresentationState()
    {
        bool animationPlaying =
            animationDirector?.IsPlaying == true;

        bool resolutionPresentation =
            BattleResolutionUiController.Instance?.IsResolutionPresentationActive == true;

        bool rollVisible =
            clashRollPresentation?.IsVisible == true;

        if (animationPlaying !=
                lastObservedAnimationPlaying ||
            resolutionPresentation !=
                lastObservedResolutionPresentation ||
            rollVisible !=
                lastObservedRollVisible)
        {
            lastObservedAnimationPlaying =
                animationPlaying;
            lastObservedResolutionPresentation =
                resolutionPresentation;
            lastObservedRollVisible =
                rollVisible;

            MarkProgress(
                "PresentationState:" +
                $"Animation={animationPlaying}," +
                $"ResolutionUI={resolutionPresentation}," +
                $"RollUI={rollVisible}");
        }
    }

    private bool IsPresentationActive()
    {
        return
            animationDirector?.IsPlaying == true ||
            BattleResolutionUiController.Instance?.IsResolutionPresentationActive == true ||
            clashRollPresentation?.IsVisible == true;
    }

    private void CancelPresentationAndEndBattle()
    {
        isFinishing = true;
        ResolvePresentationReferences();

        try
        {
            animationDirector?.CancelActivePlayback();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        try
        {
            clashRollPresentation?
                .CancelActivePresentation();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        try
        {
            BattleResolutionUiController
                .EndCurrentResolution();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (manager != null &&
            !manager.IsEndingOrEnded)
        {
            manager.EndBattle();
        }
    }

    private void HandleRuntimeLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (completed ||
            isFinishing ||
            result == null)
        {
            return;
        }

        if (type == LogType.Log &&
            IsBattleProgressLog(
                condition,
                stackTrace))
        {
            MarkProgress("RuntimePresentationLog");
        }

        string message =
            string.IsNullOrWhiteSpace(condition)
                ? type.ToString()
                : condition.Trim();

        if (message.IndexOf(
                "파괴 권한이 없는 공격은 피해를 주지 못합니다.",
                StringComparison.Ordinal) >= 0)
        {
            result.BlockedDamageAttemptCount++;
        }

        bool isError =
            type == LogType.Error ||
            type == LogType.Exception ||
            type == LogType.Assert;

        bool isWarning =
            type == LogType.Warning;

        bool invariant =
            IsCriticalCombatInvariant(message);

        if (isError)
            result.RuntimeErrorCount++;

        if (isWarning)
            result.RuntimeWarningCount++;

        if (invariant)
            result.CriticalInvariantCount++;

        if (isError || invariant)
        {
            AddSample(
                result.RuntimeProblemSamples,
                BuildLogSample(
                    type,
                    message,
                    stackTrace));
        }
        else if (isWarning)
        {
            AddSample(
                result.WarningSamples,
                BuildLogSample(
                    type,
                    message,
                    stackTrace));
        }
    }

    private static bool IsBattleProgressLog(
        string condition,
        string stackTrace)
    {
        string message =
            condition ?? string.Empty;

        if (message.StartsWith(
                "===== TURN ",
                StringComparison.Ordinal) ||
            message.StartsWith(
                "[Combat",
                StringComparison.Ordinal) ||
            message.StartsWith(
                "[BattleAnimationDirector]",
                StringComparison.Ordinal) ||
            message.StartsWith(
                "[BattleResolutionUI]",
                StringComparison.Ordinal) ||
            message.StartsWith(
                "[MomentumScrollbarUI]",
                StringComparison.Ordinal) ||
            message.StartsWith(
                "[Nakil]",
                StringComparison.Ordinal))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(stackTrace))
            return false;

        return
            stackTrace.IndexOf(
                "ActionResolver",
                StringComparison.Ordinal) >= 0 ||
            stackTrace.IndexOf(
                "BattleAnimationDirector",
                StringComparison.Ordinal) >= 0 ||
            stackTrace.IndexOf(
                "BattleClashRollPresentationUI",
                StringComparison.Ordinal) >= 0 ||
            stackTrace.IndexOf(
                "SkillCutsceneDirector",
                StringComparison.Ordinal) >= 0 ||
            stackTrace.IndexOf(
                "BattleStatusVisualDirector",
                StringComparison.Ordinal) >= 0;
    }

    private static bool IsCriticalCombatInvariant(
        string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (message.IndexOf(
                "NullReferenceException",
                StringComparison.Ordinal) >= 0 ||
            message.IndexOf(
                "MissingReferenceException",
                StringComparison.Ordinal) >= 0 ||
            message.IndexOf(
                "InvalidOperationException",
                StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        bool targetManager =
            message.IndexOf(
                "[DamageManager]",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf(
                "[ClashManager]",
                StringComparison.OrdinalIgnoreCase) >= 0;

        bool targetViolation =
            message.IndexOf(
                "불일치",
                StringComparison.Ordinal) >= 0 ||
            message.IndexOf(
                "타깃",
                StringComparison.Ordinal) >= 0 ||
            message.IndexOf(
                "TargetPart.Owner",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf(
                "차단",
                StringComparison.Ordinal) >= 0;

        return targetManager && targetViolation;
    }

    private static string BuildLogSample(
        LogType type,
        string message,
        string stackTrace)
    {
        string firstStackLine = string.Empty;

        if (!string.IsNullOrWhiteSpace(stackTrace))
        {
            string[] lines = stackTrace.Split(
                new[]
                {
                    '\r',
                    '\n'
                },
                StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length > 0)
                firstStackLine = lines[0].Trim();
        }

        string sample =
            $"{type}: {message}";

        if (!string.IsNullOrWhiteSpace(firstStackLine))
            sample += $" @ {firstStackLine}";

        return sample;
    }

    private static void AddSample(
        IList<string> destination,
        string sample)
    {
        const int MaximumSamples = 8;

        if (destination == null ||
            string.IsNullOrWhiteSpace(sample) ||
            destination.Count >= MaximumSamples)
        {
            return;
        }

        for (int i = 0; i < destination.Count; i++)
        {
            if (string.Equals(
                    destination[i],
                    sample,
                    StringComparison.Ordinal))
            {
                return;
            }
        }

        destination.Add(sample);
    }

    private static int CountUniqueRuntimeSkills(
        IReadOnlyList<Skill> skills)
    {
        if (skills == null)
            return 0;

        HashSet<string> identities =
            new HashSet<string>(
                StringComparer.Ordinal);

        for (int i = 0; i < skills.Count; i++)
        {
            Skill skill = skills[i];

            if (skill == null)
                continue;

            string identity =
                skill.Definition?.SkillId;

            if (string.IsNullOrWhiteSpace(identity))
            {
                identity =
                    skill.SkillName ??
                    skill.GetType().FullName;
            }

            if (!string.IsNullOrWhiteSpace(identity))
                identities.Add(identity);
        }

        return identities.Count;
    }

    private void RestoreTimeScale()
    {
        Time.timeScale =
            Mathf.Max(
                0f,
                savedTimeScale);
    }

    private void OnDestroy()
    {
        UnbindRuntimeObservation();

        if (!completed)
            RestoreTimeScale();

        if (Active == this)
            Active = null;
    }
}