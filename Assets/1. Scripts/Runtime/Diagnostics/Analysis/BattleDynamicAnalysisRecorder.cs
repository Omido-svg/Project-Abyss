using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-500)]
public sealed class BattleDynamicAnalysisRecorder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;

    [Header("Recording")]
    [SerializeField] private bool recordAutomatically = true;
    [SerializeField] private bool captureUnityWarningsAndErrors = true;
    [SerializeField] private bool captureBattleDebugMessages = true;
    [SerializeField] private bool echoJsonToConsole;

    // Assets/BattleAnalysis 아래에 세션 폴더를 만든다.
    public string OutputRootDirectory =>
        BattleAnalysisOutputPaths.BattleAnalysisDirectory;

    public string CurrentSessionDirectory =>
        writer?.SessionDirectory;

    public string CurrentEventsPath =>
        writer?.EventsPath;

    public string LastCompletedSessionDirectory { get; private set; }

    private BattleContext context;
    private BattleEvent battleEvent;
    private BattleTraceFileWriter writer;
    private BattleTraceSessionSummary summary;

    private readonly Dictionary<Character, string>
        characterIds = new();

    private readonly HashSet<long>
        activeActionIds = new();

    private readonly HashSet<int>
        executionPlanTurns = new();

    private long nextSequence = 1;
    private int currentTurn;
    private bool bound;
    private bool sessionClosed;
    private bool isWriting;

    public void Configure(BattleManager manager)
    {
        battleManager = manager;
    }


    /// <summary>
    /// 배치 분석 버튼을 누르기 전에 일반 동적 Recorder가 만든 대기 세션을 정리한다.
    /// 실제 행동/피해가 하나라도 있었다면 세션을 보존하고, 아무 행동도 없었다면
    /// 폴더 자체를 삭제해 ABORTED_OR_DRAW 노이즈를 남기지 않는다.
    /// </summary>
    public static void PrepareForBatchSimulation()
    {
        BattleDynamicAnalysisRecorder[] recorders =
            FindObjectsByType<BattleDynamicAnalysisRecorder>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (BattleDynamicAnalysisRecorder recorder in recorders)
            recorder?.PrepareCurrentSessionForBatch();
    }

    private void PrepareCurrentSessionForBatch()
    {
        if (writer == null)
            return;

        bool meaningful = HasMeaningfulCombatData();
        Unbind();

        if (meaningful)
        {
            CloseSession("BATCH_STARTED");
            return;
        }

        DiscardCurrentSession();
    }

    private bool HasMeaningfulCombatData()
    {
        if (summary == null)
            return false;

        return summary.playerActions > 0 ||
               summary.enemyActions > 0 ||
               summary.clashes > 0 ||
               summary.playerDamageDealt > 0 ||
               summary.playerDamageTaken > 0 ||
               summary.playerKills > 0 ||
               summary.enemyKills > 0 ||
               summary.playerEnergySpent > 0 ||
               summary.enemyEnergySpent > 0;
    }

    private void DiscardCurrentSession()
    {
        BattleTraceFileWriter discardedWriter = writer;
        string discardedDirectory = discardedWriter?.SessionDirectory;

        writer = null;
        summary = null;
        sessionClosed = true;

        try
        {
            discardedWriter?.Dispose();

            if (!string.IsNullOrWhiteSpace(discardedDirectory) &&
                Directory.Exists(discardedDirectory))
            {
                Directory.Delete(discardedDirectory, true);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[BattleDynamicAnalysisRecorder] 빈 사전 세션 폴더 삭제 실패\n" +
                discardedDirectory + "\n" + exception.Message,
                this);
        }
    }

    private void Awake()
    {
        ResolveBattleManager();
    }

    private void Start()
    {
        if (BattleSimulationRuntime.ConsumeSuppressNextDynamicAnalysisSession())
        {
            return;
        }

        if (!recordAutomatically ||
            BattleSimulationRuntime.SuppressDetailedTrace)
        {
            return;
        }

        ResolveBattleManager();

        if (battleManager == null)
        {
            Debug.LogWarning(
                "[BattleDynamicAnalysisRecorder] BattleManager를 찾지 못했습니다.",
                this);
            return;
        }

        battleManager.BattlePrepared +=
            HandleBattlePrepared;

        if (battleManager.IsInitialized &&
            battleManager.BattleContext != null)
        {
            Bind(battleManager.BattleContext);
        }
    }

    private void OnDestroy()
    {
        if (battleManager != null)
        {
            battleManager.BattlePrepared -=
                HandleBattlePrepared;
        }

        Unbind();
        CloseSession("RecorderDestroyed");
    }

    private void ResolveBattleManager()
    {
        if (battleManager == null)
        {
            battleManager =
                GetComponent<BattleManager>();
        }

        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<BattleManager>();
        }
    }

    private void HandleBattlePrepared(
        BattleContext preparedContext)
    {
        Bind(preparedContext);
    }

    private void Bind(BattleContext preparedContext)
    {
        if (bound ||
            preparedContext == null ||
            BattleSimulationRuntime.SuppressDetailedTrace)
        {
            return;
        }

        context = preparedContext;
        battleEvent = context._battleEvent;

        if (battleEvent == null || battleEvent.IsDisposed)
            return;

        BuildCharacterIds();

        try
        {
            OpenSession();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[BattleDynamicAnalysisRecorder] JSONL 세션 생성 실패\n" +
                "대상 경로: " + OutputRootDirectory + "\n" +
                exception,
                this);
            return;
        }

        SubscribeEvents();
        bound = true;

        Emit(
            "SESSION_STARTED",
            "BattleDynamicAnalysisRecorder",
            snapshot: CreateStateSnapshot(
                includePlannedActions: true),
            rules: CreateRuleSnapshot(),
            message:
                "Machine-readable JSONL battle trace started.");
    }

    private void SubscribeEvents()
    {
        battleEvent.OnBattleStarted += HandleBattleStarted;
        battleEvent.OnBattleEnded += HandleBattleEnded;
        battleEvent.OnTurnStart += HandleTurnStart;
        battleEvent.OnTurnEnd += HandleTurnEnd;
        battleEvent.OnActionStart += HandleActionStart;
        battleEvent.OnActionEnd += HandleActionEnd;
        battleEvent.OnClashStart += HandleClashStart;
        battleEvent.OnClashResolved += HandleClashResolved;
        battleEvent.OnDamageEventResolved += HandleDamageResolved;
        battleEvent.OnCombatResourceChanged += HandleResourceChanged;
        battleEvent.OnStatusApplyResolved += HandleStatusApplied;
        battleEvent.OnStatusTicked += HandleStatusTicked;
        battleEvent.OnStatusRemovedDetailed += HandleStatusRemoved;
        battleEvent.OnBodyPartWeakenResolved += HandlePartWeakened;
        battleEvent.OnBodyPartBreakResolved += HandlePartBroken;
        battleEvent.OnBodyPartRecovered += HandlePartRecovered;
        battleEvent.OnCharacterDeathResolved += HandleCharacterDeath;
        battleEvent.OnKillResolved += HandleKill;

        if (captureBattleDebugMessages)
        {
            BattleDebugLog.MessageEmitted +=
                HandleBattleDebugMessage;
        }

        if (captureUnityWarningsAndErrors)
        {
            Application.logMessageReceived +=
                HandleUnityLog;
        }
    }

    private void Unbind()
    {
        if (!bound)
            return;

        if (battleEvent != null && !battleEvent.IsDisposed)
        {
            battleEvent.OnBattleStarted -= HandleBattleStarted;
            battleEvent.OnBattleEnded -= HandleBattleEnded;
            battleEvent.OnTurnStart -= HandleTurnStart;
            battleEvent.OnTurnEnd -= HandleTurnEnd;
            battleEvent.OnActionStart -= HandleActionStart;
            battleEvent.OnActionEnd -= HandleActionEnd;
            battleEvent.OnClashStart -= HandleClashStart;
            battleEvent.OnClashResolved -= HandleClashResolved;
            battleEvent.OnDamageEventResolved -= HandleDamageResolved;
            battleEvent.OnCombatResourceChanged -= HandleResourceChanged;
            battleEvent.OnStatusApplyResolved -= HandleStatusApplied;
            battleEvent.OnStatusTicked -= HandleStatusTicked;
            battleEvent.OnStatusRemovedDetailed -= HandleStatusRemoved;
            battleEvent.OnBodyPartWeakenResolved -= HandlePartWeakened;
            battleEvent.OnBodyPartBreakResolved -= HandlePartBroken;
            battleEvent.OnBodyPartRecovered -= HandlePartRecovered;
            battleEvent.OnCharacterDeathResolved -= HandleCharacterDeath;
            battleEvent.OnKillResolved -= HandleKill;
        }

        BattleDebugLog.MessageEmitted -=
            HandleBattleDebugMessage;

        Application.logMessageReceived -=
            HandleUnityLog;

        bound = false;
    }

    private void OpenSession()
    {
        string sessionId =
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") +
            "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        writer = new BattleTraceFileWriter(
            OutputRootDirectory,
            sessionId);

        summary = new BattleTraceSessionSummary
        {
            sessionId = sessionId,
            startedUtc = DateTime.UtcNow.ToString("O"),
            eventsFile = writer.EventsPath
        };

        nextSequence = 1;
        sessionClosed = false;

        Debug.Log(
            "[BattleTrace] events.jsonl created: " +
            writer.EventsPath,
            this);
    }

    private void CloseSession(string fallbackOutcome)
    {
        if (sessionClosed || writer == null)
            return;

        sessionClosed = true;
        BattleTraceFileWriter closingWriter = writer;

        try
        {
            if (summary != null)
            {
                if (string.IsNullOrWhiteSpace(summary.outcome))
                    summary.outcome = fallbackOutcome;

                summary.endedUtc = DateTime.UtcNow.ToString("O");
                summary.completedTurns = currentTurn;
                summary.totalEvents =
                    Mathf.Max(0, (int)nextSequence - 1);

                closingWriter.WriteSummary(summary);
            }

            LastCompletedSessionDirectory =
                closingWriter.SessionDirectory;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[BattleDynamicAnalysisRecorder] summary.json 저장 실패\n" +
                "세션 경로: " + closingWriter.SessionDirectory + "\n" +
                exception,
                this);
        }
        finally
        {
            closingWriter.Dispose();
            writer = null;
        }
    }

    private void HandleBattleStarted()
    {
        Emit(
            "BATTLE_STARTED",
            nameof(BattleEvent.OnBattleStarted),
            snapshot: CreateStateSnapshot(true));
    }

    private void HandleBattleEnded()
    {
        string outcome = ResolveOutcome();

        if (summary != null)
            summary.outcome = outcome;

        Emit(
            "BATTLE_ENDED",
            nameof(BattleEvent.OnBattleEnded),
            snapshot: CreateStateSnapshot(true),
            message: outcome);

        Unbind();
        CloseSession(outcome);
    }

    private void HandleTurnStart(int turn)
    {
        currentTurn = turn;
        executionPlanTurns.Remove(turn);

        Emit(
            "TURN_STARTED",
            nameof(BattleEvent.OnTurnStart),
            snapshot: CreateStateSnapshot(false));

        StartCoroutine(
            CapturePlanAtEndOfFrame(turn));
    }

    private IEnumerator CapturePlanAtEndOfFrame(int turn)
    {
        yield return new WaitForEndOfFrame();

        if (!bound || currentTurn != turn)
            yield break;

        Emit(
            "TURN_PLAN_READY",
            "TurnManager.StartTurnInternal",
            snapshot: CreateStateSnapshot(true));
    }

    private void HandleTurnEnd(int turn)
    {
        currentTurn = turn;

        Emit(
            "TURN_ENDED",
            nameof(BattleEvent.OnTurnEnd),
            snapshot: CreateStateSnapshot(true));
    }

    private void HandleActionStart(BattleAction action)
    {
        if (executionPlanTurns.Add(currentTurn))
        {
            Emit(
                "TURN_EXECUTION_PLAN",
                "ActionResolver/ClashManager",
                snapshot: CreateStateSnapshot(true));
        }

        if (action != null)
        {
            activeActionIds.Add(action.ActionId);

            if (summary != null)
            {
                if (action.Owner == context?.Player)
                    summary.playerActions++;
                else
                    summary.enemyActions++;
            }
        }

        Emit(
            "ACTION_STARTED",
            nameof(BattleEvent.OnActionStart),
            action: CreateAction(action),
            snapshot: CreateStateSnapshot(false));
    }

    private void HandleActionEnd(BattleAction action)
    {
        if (action != null)
            activeActionIds.Remove(action.ActionId);

        Emit(
            "ACTION_ENDED",
            nameof(BattleEvent.OnActionEnd),
            action: CreateAction(action),
            snapshot: CreateStateSnapshot(false));
    }

    private void HandleClashStart(
        Character first,
        Character second)
    {
        Emit(
            "CLASH_STARTED",
            nameof(BattleEvent.OnClashStart),
            message:
                $"{GetCharacterId(first)} -> {GetCharacterId(second)}");
    }

    private void HandleClashResolved(
        ClashResultContext result)
    {
        if (summary != null && result?.IsClash == true)
            summary.clashes++;

        Emit(
            "CLASH_RESOLVED",
            nameof(BattleEvent.OnClashResolved),
            action: CreateAction(result?.FirstAction),
            otherAction: CreateAction(result?.SecondAction),
            clash: CreateClash(result),
            snapshot: CreateStateSnapshot(false));
    }

    private void HandleDamageResolved(
        DamageEventResult result)
    {
        DamageContext damage = result?.Context;
        int applied = damage?.GetDisplayDamage() ?? 0;

        if (summary != null && damage != null)
        {
            if (damage.Attacker == context?.Player &&
                damage.Target != context?.Player)
            {
                summary.playerDamageDealt += applied;
            }

            if (damage.Target == context?.Player)
                summary.playerDamageTaken += applied;
        }

        Emit(
            "DAMAGE_RESOLVED",
            nameof(BattleEvent.OnDamageEventResolved),
            action: CreateAction(damage?.Action),
            damage: CreateDamage(damage),
            snapshot: CreateStateSnapshot(false));
    }

    private void HandleResourceChanged(
        CombatResourceChangeContext change)
    {
        if (change == null)
            return;

        if (summary != null &&
            change.ResourceKey == CombatResourceKeys.Energy &&
            change.Delta < 0)
        {
            int spent = -change.Delta;

            if (change.Owner == context?.Player)
                summary.playerEnergySpent += spent;
            else
                summary.enemyEnergySpent += spent;
        }

        Emit(
            "RESOURCE_CHANGED",
            nameof(BattleEvent.OnCombatResourceChanged),
            action: CreateAction(change.SourceAction),
            resource: CreateResource(change),
            snapshot: CreateStateSnapshot(false));
    }

    private void HandleStatusApplied(
        StatusEffectApplyResult result)
    {
        Emit(
            "STATUS_APPLY_RESOLVED",
            nameof(BattleEvent.OnStatusApplyResolved),
            status: CreateStatus(result));
    }

    private void HandleStatusTicked(
        StatusEffectTickContext tick)
    {
        Emit(
            "STATUS_TICKED",
            nameof(BattleEvent.OnStatusTicked),
            status: CreateStatus(tick));
    }

    private void HandleStatusRemoved(
        Character target,
        BodyPart part,
        StatusEffect effect,
        StatusEffectRemoveReason reason)
    {
        Emit(
            "STATUS_REMOVED",
            nameof(BattleEvent.OnStatusRemovedDetailed),
            status: CreateStatus(
                target,
                part,
                effect,
                reason.ToString()));
    }

    private void HandlePartWeakened(
        BodyPartWeakenEventContext result)
    {
        Emit(
            "BODY_PART_WEAKENED",
            nameof(BattleEvent.OnBodyPartWeakenResolved),
            action: CreateAction(result?.SourceAction),
            snapshot: CreateStateSnapshot(false),
            message: CreatePartTransitionMessage(
                result?.Target,
                result?.Part,
                result?.StateBefore.ToString(),
                result?.StateAfter.ToString()));
    }

    private void HandlePartBroken(
        BodyPartBreakEventContext result)
    {
        Emit(
            "BODY_PART_BROKEN",
            nameof(BattleEvent.OnBodyPartBreakResolved),
            action: CreateAction(result?.SourceAction),
            snapshot: CreateStateSnapshot(false),
            message: CreatePartTransitionMessage(
                result?.Target,
                result?.Part,
                result?.StateBefore.ToString(),
                result?.StateAfter.ToString()));
    }

    private void HandlePartRecovered(
        Character target,
        BodyPart part)
    {
        Emit(
            "BODY_PART_RECOVERED",
            nameof(BattleEvent.OnBodyPartRecovered),
            snapshot: CreateStateSnapshot(false),
            message: CreatePartTransitionMessage(
                target,
                part,
                "Broken/Weakened",
                part?.State.ToString()));
    }

    private void HandleCharacterDeath(
        KillEventContext result)
    {
        Emit(
            "CHARACTER_DEATH",
            nameof(BattleEvent.OnCharacterDeathResolved),
            action: CreateAction(result?.SourceAction),
            snapshot: CreateStateSnapshot(false),
            message: GetCharacterId(result?.Victim));
    }

    private void HandleKill(KillEventContext result)
    {
        if (summary != null && result != null)
        {
            if (result.Killer == context?.Player)
                summary.playerKills++;
            else if (result.Victim == context?.Player)
                summary.enemyKills++;
        }

        Emit(
            "KILL_RESOLVED",
            nameof(BattleEvent.OnKillResolved),
            action: CreateAction(result?.SourceAction),
            snapshot: CreateStateSnapshot(false),
            message:
                $"{GetCharacterId(result?.Killer)} -> " +
                GetCharacterId(result?.Victim));
    }

    private void HandleBattleDebugMessage(
        BattleDebugMessage message)
    {
        if (message == null)
            return;

        Emit(
            "BATTLE_DEBUG_MESSAGE",
            "BattleDebugLog",
            message:
                $"[{message.CategoryLabel}/{message.Level}] " +
                message.Message);
    }

    private void HandleUnityLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (isWriting ||
            type == LogType.Log ||
            condition.StartsWith(
                BattleTraceSchema.ConsolePrefix,
                StringComparison.Ordinal))
        {
            return;
        }

        Emit(
            type == LogType.Warning
                ? "UNITY_WARNING"
                : "UNITY_ERROR",
            "Application.logMessageReceived",
            message:
                condition +
                (string.IsNullOrWhiteSpace(stackTrace)
                    ? string.Empty
                    : "\n" + stackTrace));
    }

    private void Emit(
        string eventType,
        string source,
        BattleTraceAction action = null,
        BattleTraceAction otherAction = null,
        BattleTraceClash clash = null,
        BattleTraceDamage damage = null,
        BattleTraceStatus status = null,
        BattleTraceResource resource = null,
        BattleTraceStateSnapshot snapshot = null,
        BattleTraceRuleSnapshot rules = null,
        string message = null)
    {
        if (writer == null || sessionClosed)
            return;

        BattleTraceRecord record =
            new BattleTraceRecord
            {
                sessionId = summary?.sessionId,
                sequence = nextSequence++,
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                frame = Time.frameCount,
                realtime = Time.realtimeSinceStartup,
                eventType = eventType,
                source = source,
                turn = currentTurn,
                phase = ResolvePhase(action),
                momentum = context?.battleManager
                    ?.MomentumManager?.CurrentMomentum ?? 0,
                message = message,
                rules = rules,
                action = action,
                otherAction = otherAction,
                clash = clash,
                damage = damage,
                status = status,
                resource = resource,
                snapshot = snapshot
            };

        try
        {
            isWriting = true;
            writer.Write(record);

            if (echoJsonToConsole)
            {
                Debug.Log(
                    BattleTraceSchema.ConsolePrefix + " " +
                    JsonUtility.ToJson(record));
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            isWriting = false;
        }
    }

    private void BuildCharacterIds()
    {
        characterIds.Clear();

        int index = 1;

        if (context?.Player != null)
        {
            characterIds[context.Player] =
                $"P{index:00}";
            index++;
        }

        if (context?.Enemies == null)
            return;

        foreach (Character enemy in context.Enemies)
        {
            if (enemy == null || characterIds.ContainsKey(enemy))
                continue;

            characterIds[enemy] =
                $"E{index:00}";
            index++;
        }
    }

    private string GetCharacterId(Character character)
    {
        if (character == null)
            return "NONE";

        if (characterIds.TryGetValue(
                character,
                out string id))
        {
            return id;
        }

        id = "X" + Mathf.Abs(
            character.GetInstanceID());
        characterIds[character] = id;
        return id;
    }

    private string GetSide(Character character)
    {
        if (character == null)
            return "NONE";

        return character == context?.Player
            ? "PLAYER"
            : "ENEMY";
    }

    private BattleTraceResource CreateResource(
        CombatResourceChangeContext change)
    {
        if (change == null)
            return null;

        return new BattleTraceResource
        {
            ownerId = GetCharacterId(change.Owner),
            ownerName = GetCharacterName(change.Owner),
            ownerSide = GetSide(change.Owner),
            key = change.ResourceKey,
            before = change.Before,
            after = change.After,
            maximum = change.Maximum,
            delta = change.Delta,
            reason = change.Reason.ToString(),
            sourceActionId = change.SourceAction?.ActionId ?? 0,
            sourceSkill = change.SourceSkill?.SkillName ?? "NONE"
        };
    }

    private BattleTraceAction CreateAction(
        BattleAction action)
    {
        return action == null
            ? null
            : CreateAction(action.Slot, action);
    }

    private BattleTraceAction CreateAction(
        ActionSlot slot,
        BattleAction resolvedAction = null)
    {
        if (slot == null)
            return null;

        return new BattleTraceAction
        {
            actionId = slot.ActionId,
            targetActionId = slot.TargetSlot?.ActionId ?? 0,
            actionIndex = slot.ActionIndex,
            ownerId = GetCharacterId(slot.Owner),
            ownerName = GetCharacterName(slot.Owner),
            ownerSide = GetSide(slot.Owner),
            ownerPart = GetPartName(slot.Part),
            targetId = GetCharacterId(slot.TargetCharacter),
            targetName = GetCharacterName(slot.TargetCharacter),
            targetSide = GetSide(slot.TargetCharacter),
            targetPart = GetPartName(slot.TargetPart),
            skill = slot.Skill?.SkillName ?? "NONE",
            actionType = slot.Skill?.ActionType.ToString() ?? "NONE",
            phase = slot.Phase.ToString(),
            speed = slot.Speed,
            purePower = resolvedAction?.RolledPower ?? 0,
            clashPower = resolvedAction?.ClashPower ?? 0,
            speedModifier = resolvedAction?.SpeedModifier ?? 0,
            momentumModifier = resolvedAction?.MomentumModifier ?? 0,
            preparationModifier =
                resolvedAction?.PreparationModifier ?? 0,
            critical = resolvedAction?.Critical ?? false,
            exchangeRollCount =
                slot.Skill?.ExchangeRollCount ?? 0,
            energyCost = slot.Skill?.EnergyCost ?? 0
        };
    }

    private BattleTraceStateSnapshot CreateStateSnapshot(
        bool includePlannedActions)
    {
        BattleTraceStateSnapshot snapshot =
            new BattleTraceStateSnapshot
            {
                momentum = context?.battleManager
                    ?.MomentumManager?.CurrentMomentum ?? 0
            };

        if (context?.AllCharacters != null)
        {
            foreach (Character character in context.AllCharacters)
            {
                if (character != null)
                {
                    snapshot.characters.Add(
                        CreateCharacterState(character));
                }
            }
        }

        if (includePlannedActions &&
            context?.battleManager?.ActionManager?.Slots != null)
        {
            foreach (ActionSlot slot in
                     context.battleManager.ActionManager.Slots)
            {
                BattleTraceAction action =
                    CreateAction(slot);

                if (action != null)
                    snapshot.plannedActions.Add(action);
            }
        }

        return snapshot;
    }

    private BattleTraceCharacterState CreateCharacterState(
        Character character)
    {
        BattleTraceCharacterState state =
            new BattleTraceCharacterState
            {
                characterId = GetCharacterId(character),
                name = GetCharacterName(character),
                side = GetSide(character),
                initialized = character.IsInitialized,
                dead = character.IsDead,
                usesBodyParts = character.UsesBodyParts,
                hp = character.CurrentHP,
                maxHp = character.MaxCombatHP,
                prestige =
                    character.RuntimeStatus?.currentPrestige ?? 0,
                maxPrestige =
                    character.CurrentStatus?.maxPrestige ?? 0,
                energy = character.CurrentEnergy,
                maxEnergy = character.MaxEnergy
            };

        if (character.StatusEffects != null)
        {
            foreach (StatusEffect effect in character.StatusEffects)
            {
                if (effect != null)
                    state.statuses.Add(CreateStatusState(effect));
            }
        }

        if (character.BodyParts != null)
        {
            foreach (BodyPart part in character.BodyParts)
            {
                if (part == null)
                    continue;

                BattleTraceBodyPartState partState =
                    new BattleTraceBodyPartState
                    {
                        part = part.Type.ToString(),
                        state = part.State.ToString(),
                        hp = Mathf.Max(
                            0,
                            Mathf.RoundToInt(part.PartHP)),
                        maxHp = Mathf.Max(
                            0,
                            Mathf.RoundToInt(part.MaxPartHP)),
                        revision = part.Revision
                    };

                foreach (StatusEffect effect in part.StatusEffects)
                {
                    if (effect != null)
                    {
                        partState.statuses.Add(
                            CreateStatusState(effect));
                    }
                }

                state.parts.Add(partState);
            }
        }

        return state;
    }

    private static BattleTraceStatusState CreateStatusState(
        StatusEffect effect)
    {
        return new BattleTraceStatusState
        {
            name = effect?.Name ?? "NONE",
            type = effect?.GetType().Name ?? "NONE",
            stack = effect?.Stack ?? 0,
            duration = effect?.Duration ?? 0,
            ownerPart = GetPartName(effect?.OwnerPart)
        };
    }

    private BattleTraceClash CreateClash(
        ClashResultContext result)
    {
        if (result == null)
            return null;

        BattleTraceClash trace =
            new BattleTraceClash
            {
                isClash = result.IsClash,
                draw = result.IsDraw,
                winnerOwnerId =
                    GetCharacterId(result.WinnerAction?.Owner),
                loserOwnerId =
                    GetCharacterId(result.LoserAction?.Owner),
                firstExchangeWins = result.FirstExchangeWins,
                secondExchangeWins = result.SecondExchangeWins,
                pairedExchangeCount = result.PairedExchangeCount,
                oneSidedHitCount = result.OneSidedHitCount,
                totalDamage = result.TotalDamage,
                momentumShift = result.MomentumShift,
                prestigeGain = result.PrestigeGain
            };

        if (result.Exchanges != null)
        {
            foreach (ClashExchangeResult exchange
                     in result.Exchanges)
            {
                if (exchange == null)
                    continue;

                trace.exchanges.Add(
                    new BattleTraceExchange
                    {
                        index = exchange.ExchangeIndex,
                        oneSided = exchange.IsOneSided,
                        tie = exchange.IsTie,
                        cancelled = exchange.WasCancelled,
                        firstClashPower = exchange.FirstClashPower,
                        secondClashPower = exchange.SecondClashPower,
                        winnerOwnerId = GetCharacterId(
                            exchange.WinnerAction?.Owner),
                        loserOwnerId = GetCharacterId(
                            exchange.LoserAction?.Owner),
                        damage = exchange.Damage,
                        momentumShift = exchange.MomentumShift,
                        prestigeDealtGain = exchange.PrestigeDealtGain,
                        prestigeTakenGain = exchange.PrestigeTakenGain,
                        firstRoll = CreateRoll(exchange.FirstRollResult),
                        secondRoll = CreateRoll(exchange.SecondRollResult)
                    });
            }
        }

        return trace;
    }

    private static BattleTraceRoll CreateRoll(
        RollResult result)
    {
        if (result == null)
            return null;

        return new BattleTraceRoll
        {
            resolver = result.ResolverType.ToString(),
            basePower = result.BasePower,
            rawValue = result.RawValue,
            modifiedValue = result.ModifiedValue,
            externalModifier = result.ExternalModifier,
            finalPower = result.FinalPower,
            speedModifier = result.SpeedModifier,
            momentumModifier = result.MomentumModifier,
            preparationModifier = result.PreparationModifier,
            clashPower = result.ClashPower,
            isMax = result.IsMax,
            critical = result.IsCritical,
            reused = result.WasReused,
            display = result.GetShortDisplayText(),
            chinchiroCombination =
                result.ChinchiroCombination.ToString(),
            chinchiroBonus = result.ChinchiroBonus,
            chinchiroSelfDamage = result.ChinchiroSelfDamage,
            diceValues = result.DiceValues == null
                ? new List<int>()
                : new List<int>(result.DiceValues),
            coinFaces = result.CoinFaces == null
                ? new List<bool>()
                : new List<bool>(result.CoinFaces),
            coinValues = result.CoinValues == null
                ? new List<int>()
                : new List<int>(result.CoinValues)
        };
    }

    private static BattleTraceDamage CreateDamage(
        DamageContext context)
    {
        if (context == null)
            return null;

        BattleTraceDamage trace =
            new BattleTraceDamage
            {
                damageType = context.DamageType.ToString(),
                rawPower = context.RawPower,
                skillMultiplier = context.SkillMultiplier,
                flatDamageBonus = context.FlatDamageBonus,
                ownerDamageMultiplier = context.OwnerDamageMultiplier,
                momentumMultiplier = context.MomentumMultiplier,
                baseDamage = context.BaseDamage,
                rawDamage = context.RawDamage,
                attackerModifiedDamage = context.AttackerModifiedDamage,
                critical = context.WasCritical,
                damageAfterCritical = context.DamageAfterCritical,
                defenseValue = context.DefenseValue,
                damageAfterArmor = context.DamageAfterArmor,
                guardBefore = context.GuardBefore,
                guardAbsorbed = context.GuardAbsorbed,
                guardAfter = context.GuardAfter,
                targetModifiedDamage = context.TargetModifiedDamage,
                protectionValue = context.ProtectionValue,
                protectionAbsorbed = context.ProtectionAbsorbed,
                finalDamage = context.FinalDamage,
                appliedDamage = context.AppliedDamage,
                hpDamage = context.FinalHpDamage,
                partDamage = context.PartHpDamage,
                directHpDamage = context.DirectHpDamage,
                targetHpBefore = context.TargetHpBefore,
                targetHpAfter = context.TargetHpAfter,
                hasPartSnapshot = context.HasTargetPartSnapshot,
                targetPartHpBefore = context.TargetPartHpBefore,
                targetPartHpAfter = context.TargetPartHpAfter,
                targetPartStateBefore =
                    context.TargetPartStateBefore.ToString(),
                targetPartStateAfter =
                    context.TargetPartStateAfter.ToString(),
                weakened = context.WasWeakened,
                broken = context.WasBroken,
                killed = context.WasKilled,
                directHpRoute = context.WasDirectHPDamage
            };

        foreach (DamageSnapshot snapshot in context.StageSnapshots)
        {
            if (snapshot == null)
                continue;

            trace.stages.Add(
                new BattleTraceDamageStage
                {
                    stage = snapshot.Stage.ToString(),
                    damage = snapshot.Damage,
                    targetHp = snapshot.TargetHp,
                    targetPartHp = snapshot.TargetPartHp,
                    guardValue = snapshot.GuardValue,
                    protectionValue = snapshot.ProtectionValue,
                    hasTargetPart = snapshot.HasTargetPart,
                    targetPartState =
                        snapshot.TargetPartState.ToString()
                });
        }

        return trace;
    }

    private BattleTraceStatus CreateStatus(
        StatusEffectApplyResult result)
    {
        if (result == null)
            return null;

        return new BattleTraceStatus
        {
            targetId = GetCharacterId(result.TargetCharacter),
            targetName = GetCharacterName(result.TargetCharacter),
            targetPart = GetPartName(result.TargetPart),
            effect = result.Effect?.Name ?? "NONE",
            effectType = result.Effect?.GetType().Name ?? "NONE",
            applyKind = result.Kind.ToString(),
            stackBefore = result.StackBefore,
            stackAfter = result.StackAfter,
            durationBefore = result.DurationBefore,
            durationAfter = result.DurationAfter,
            transferred = result.WasTransferred
        };
    }

    private BattleTraceStatus CreateStatus(
        StatusEffectTickContext tick)
    {
        if (tick == null)
            return null;

        return new BattleTraceStatus
        {
            targetId = GetCharacterId(tick.TargetCharacter),
            targetName = GetCharacterName(tick.TargetCharacter),
            targetPart = GetPartName(tick.TargetPart),
            effect = tick.SourceEffect?.Name ?? "NONE",
            effectType = tick.SourceEffect?.GetType().Name ?? "NONE",
            timing = tick.Timing.ToString(),
            stackBefore = tick.StackBefore,
            stackAfter = tick.StackAfter,
            durationBefore = tick.DurationBefore,
            durationAfter = tick.DurationAfter
        };
    }

    private BattleTraceStatus CreateStatus(
        Character target,
        BodyPart part,
        StatusEffect effect,
        string removeReason)
    {
        return new BattleTraceStatus
        {
            targetId = GetCharacterId(target),
            targetName = GetCharacterName(target),
            targetPart = GetPartName(part),
            effect = effect?.Name ?? "NONE",
            effectType = effect?.GetType().Name ?? "NONE",
            removeReason = removeReason,
            stackAfter = effect?.Stack ?? 0,
            durationAfter = effect?.Duration ?? 0
        };
    }

    private BattleTraceRuleSnapshot CreateRuleSnapshot()
    {
        BattleRuleSettings settings = context?.Rules;
        MomentumRuleSettings momentum = settings?.Momentum;
        ClashRuleSettings clash = settings?.Clash;
        PrestigeRuleSettings prestige = settings?.Prestige;

        return new BattleTraceRuleSnapshot
        {
            momentumMinimum = momentum?.Minimum ?? 0,
            momentumMaximum = momentum?.Maximum ?? 0,
            lastStandThreshold = momentum?.LastStandThreshold ?? 0,
            disadvantageThreshold = momentum?.DisadvantageThreshold ?? 0,
            advantageThreshold = momentum?.AdvantageThreshold ?? 0,
            overwhelmThreshold = momentum?.OverwhelmThreshold ?? 0,
            lastStandMultiplier = momentum?.LastStandMultiplier ?? 1f,
            disadvantageMultiplier = momentum?.DisadvantageMultiplier ?? 1f,
            balanceMultiplier = momentum?.BalanceMultiplier ?? 1f,
            advantageMultiplier = momentum?.AdvantageMultiplier ?? 1f,
            overwhelmMultiplier = momentum?.OverwhelmMultiplier ?? 1f,
            maximumOverwhelmMultiplier =
                momentum?.MaximumOverwhelmMultiplier ?? 1f,
            hitShift = momentum?.HitShift ?? 0,
            duelVictoryShift = momentum?.DuelVictoryShift ?? 0,
            lastStandHitShiftMultiplier =
                momentum?.LastStandHitShiftMultiplier ?? 0,
            defaultExchangeRollCount =
                clash?.DefaultExchangeRollCount ?? 0,
            speedWeight = clash?.SpeedWeight ?? 0,
            prestigeClashStart = prestige?.ClashStartCharge ?? 0,
            prestigeHitDealt = prestige?.HitDealtCharge ?? 0,
            prestigeHitTaken = prestige?.HitTakenCharge ?? 0,
            prestigeClashVictory = prestige?.ClashVictoryCharge ?? 0
        };
    }

    private string ResolveOutcome()
    {
        Character player = context?.Player;

        if (player == null)
            return "INVALID";

        if (player.IsDead)
            return "PLAYER_DEFEAT";

        bool anyLivingEnemy = false;

        if (context.Enemies != null)
        {
            foreach (Character enemy in context.Enemies)
            {
                if (enemy != null && !enemy.IsDead)
                {
                    anyLivingEnemy = true;
                    break;
                }
            }
        }

        return anyLivingEnemy
            ? "ABORTED_OR_DRAW"
            : "PLAYER_VICTORY";
    }

    private static string ResolvePhase(
        BattleTraceAction action)
    {
        return action?.phase ?? "NONE";
    }

    private static string CreatePartTransitionMessage(
        Character target,
        BodyPart part,
        string before,
        string after)
    {
        return
            $"Target={GetCharacterName(target)}, " +
            $"Part={GetPartName(part)}, " +
            $"State={before}->{after}";
    }

    private static string GetCharacterName(
        Character character)
    {
        if (character == null)
            return "NONE";

        return character.Data?.CharacterName ??
               character.name ??
               "NONE";
    }

    private static string GetPartName(BodyPart part)
    {
        return part == null
            ? "NONE"
            : part.Type.ToString();
    }
}