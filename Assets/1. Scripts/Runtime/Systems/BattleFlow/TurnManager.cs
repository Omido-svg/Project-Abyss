using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager
{
    private readonly BattleContext battleContext;
    private readonly ActionManager actionManager;
    private readonly AIManager aiManager;
    private readonly SpeedManager speedManager;
    private readonly ActionResolver actionResolver;
    private readonly MomentumManager momentumManager;
    private readonly ClashBuilder clashBuilder;
    private readonly BattleLifecycleGuard lifecycleGuard;
    private readonly Action<Exception> fatalErrorHandler;

    private Coroutine resolveCoroutine;

    // 기존 생성자 호출부 호환.
    public TurnManager(
        BattleContext battleContext,
        ActionManager actionManager,
        AIManager aiManager,
        SpeedManager speedManager,
        ActionResolver actionResolver,
        MomentumManager momentumManager,
        ClashBuilder clashBuilder)
        : this(
            battleContext,
            actionManager,
            aiManager,
            speedManager,
            actionResolver,
            momentumManager,
            clashBuilder,
            CreateReadyGuard(),
            null)
    {
    }

    public TurnManager(
        BattleContext battleContext,
        ActionManager actionManager,
        AIManager aiManager,
        SpeedManager speedManager,
        ActionResolver actionResolver,
        MomentumManager momentumManager,
        ClashBuilder clashBuilder,
        BattleLifecycleGuard lifecycleGuard,
        Action<Exception> fatalErrorHandler = null)
    {
        this.battleContext = battleContext;
        this.actionManager = actionManager;
        this.aiManager = aiManager;
        this.speedManager = speedManager;
        this.actionResolver = actionResolver;
        this.momentumManager = momentumManager;
        this.clashBuilder = clashBuilder;
        this.lifecycleGuard = lifecycleGuard;
        this.fatalErrorHandler = fatalErrorHandler;
    }

    public int CurrentTurn { get; private set; }

    public bool IsBattleRunning =>
        lifecycleGuard != null &&
        lifecycleGuard.IsBattleRunning;

    public bool IsResolving =>
        lifecycleGuard != null &&
        lifecycleGuard.IsResolving;

    public void StartBattle()
    {
        if (lifecycleGuard == null ||
            !lifecycleGuard.TryStartBattle())
        {
            Debug.LogWarning(
                "[TurnManager] 전투 시작이 거부되었습니다. " +
                $"State={lifecycleGuard?.State}");
            return;
        }

        try
        {
            CurrentTurn = 1;

            actionManager?.ResetForBattle();
            momentumManager?.Reset();

            battleContext?._battleEvent?
                .RaiseBattleStarted();

            StartTurnInternal();
        }
        catch (Exception exception)
        {
            HandleFatalError(
                "Battle Start",
                exception);
        }
    }

    public void StartTurn()
    {
        if (!IsBattleRunning ||
            lifecycleGuard == null ||
            !lifecycleGuard.CanStartTurn)
        {
            return;
        }

        try
        {
            StartTurnInternal();
        }
        catch (Exception exception)
        {
            HandleFatalError(
                $"Turn {CurrentTurn} Start",
                exception);
        }
    }

    private void StartTurnInternal()
    {
        if (!IsBattleRunning ||
            lifecycleGuard == null ||
            !lifecycleGuard.CanStartTurn)
        {
            return;
        }

        Debug.Log(
            $"===== TURN {CurrentTurn} START =====");

        actionManager?.Clear();

        if (battleContext?.battleManager?.BattleLogger != null)
        {
            battleContext.battleManager
                .BattleLogger
                .Clear();
        }

        battleContext?._battleEvent?
            .RaiseTurnStart(CurrentTurn);

        RunCharacterTurnStart();

        speedManager?.RollAllSpeed();
        speedManager?.PrintSpeeds();

        aiManager?.DecideEnemyActions();

        actionManager?.PrintSlots(
            "AFTER AI SLOT CREATE");
    }

    public void ResolveTurn(
        Action onComplete = null)
    {
        if (!IsBattleRunning ||
            lifecycleGuard == null ||
            !lifecycleGuard.TryBeginResolution(
                out int resolutionToken))
        {
            return;
        }

        BattleManager manager =
            battleContext?.battleManager;

        if (manager == null ||
            !manager.isActiveAndEnabled)
        {
            lifecycleGuard.CompleteResolution(
                resolutionToken);

            HandleFatalError(
                $"Turn {CurrentTurn} Resolve",
                new InvalidOperationException(
                    "BattleManager가 없거나 비활성 상태입니다."));
            return;
        }

        try
        {
            resolveCoroutine =
                manager.StartCoroutine(
                    ResolveTurnRoutine(
                        resolutionToken,
                        onComplete));

            if (resolveCoroutine == null)
            {
                lifecycleGuard.CompleteResolution(
                    resolutionToken);

                HandleFatalError(
                    $"Turn {CurrentTurn} Resolve",
                    new InvalidOperationException(
                        "ResolveTurn 코루틴 시작에 실패했습니다."));
            }
        }
        catch (Exception exception)
        {
            lifecycleGuard.CompleteResolution(
                resolutionToken);

            HandleFatalError(
                $"Turn {CurrentTurn} Resolve",
                exception);
        }
    }

    private IEnumerator ResolveTurnRoutine(
        int resolutionToken,
        Action onComplete)
    {
        Exception failure = null;
        bool turnEnded = false;

        try
        {
            Debug.Log(
                $"===== TURN {CurrentTurn} RESOLVE =====");

            ActionExecutionQueue queue = null;

            try
            {
                IReadOnlyList<ActionSlot> snapshot =
                    actionManager?.CreateExecutionSnapshot();

                queue = clashBuilder?.BuildQueue(
                    snapshot);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            IEnumerator resolutionRoutine = null;

            if (failure == null)
            {
                try
                {
                    resolutionRoutine =
                        actionResolver?.Resolve(queue);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }

            if (failure == null &&
                resolutionRoutine != null)
            {
                yield return ExecuteSafely(
                    resolutionRoutine,
                    resolutionToken,
                    exception => failure = exception);
            }

            if (failure == null &&
                lifecycleGuard.IsResolutionCurrent(
                    resolutionToken))
            {
                try
                {
                    EndTurnInternal();
                    turnEnded = true;
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }
        }
        finally
        {
            resolveCoroutine = null;

            lifecycleGuard?.CompleteResolution(
                resolutionToken);
        }

        if (failure != null)
        {
            HandleFatalError(
                $"Turn {CurrentTurn} Resolve",
                failure);
            yield break;
        }

        if (!turnEnded)
            yield break;

        Exception completionFailure =
            InvokeCompletionSafely(
                onComplete);

        if (completionFailure != null)
        {
            HandleFatalError(
                $"Turn {CurrentTurn - 1} Completion",
                completionFailure);
        }
    }

    private IEnumerator ExecuteSafely(
        IEnumerator rootRoutine,
        int resolutionToken,
        Action<Exception> onException)
    {
        if (rootRoutine == null)
            yield break;

        Stack<IEnumerator> stack = new();
        stack.Push(rootRoutine);

        try
        {
            while (stack.Count > 0)
            {
                if (lifecycleGuard == null ||
                    !lifecycleGuard.IsResolutionCurrent(
                        resolutionToken))
                {
                    yield break;
                }

                IEnumerator currentRoutine =
                    stack.Peek();

                bool movedNext = false;
                object yieldedObject = null;
                Exception moveFailure = null;

                try
                {
                    movedNext =
                        currentRoutine.MoveNext();

                    if (movedNext)
                    {
                        yieldedObject =
                            currentRoutine.Current;
                    }
                }
                catch (Exception exception)
                {
                    moveFailure = exception;
                }

                if (moveFailure != null)
                {
                    onException?.Invoke(moveFailure);
                    yield break;
                }

                if (!movedNext)
                {
                    DisposeEnumerator(
                        stack.Pop());
                    continue;
                }

                if (yieldedObject is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                yield return yieldedObject;
            }
        }
        finally
        {
            while (stack.Count > 0)
            {
                DisposeEnumerator(
                    stack.Pop());
            }
        }
    }

    private void EndTurnInternal()
    {
        RunCharacterTurnEnd();

        battleContext?._battleEvent?
            .RaiseTurnEnd(CurrentTurn);

        battleContext?.battleManager?
            .BattleLogger?
            .PrintTurn(CurrentTurn);

        Debug.Log(
            $"===== TURN {CurrentTurn} END =====");

        CurrentTurn++;
    }

    public void NextTurn()
    {
        if (IsResolving)
            return;

        StartTurn();
    }

    public void EndBattle()
    {
        Coroutine activeCoroutine =
            resolveCoroutine;

        resolveCoroutine = null;

        if (activeCoroutine != null &&
            battleContext?.battleManager != null)
        {
            try
            {
                battleContext.battleManager
                    .StopCoroutine(activeCoroutine);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        lifecycleGuard?.EndBattle();
        actionManager?.Clear();
    }

    private void RunCharacterTurnStart()
    {
        if (battleContext?.AllCharacters == null)
            return;

        foreach (Character character
                 in battleContext.AllCharacters)
        {
            if (character == null ||
                character.IsDead)
            {
                continue;
            }

            character.TurnStart();
        }
    }

    private void RunCharacterTurnEnd()
    {
        if (battleContext?.AllCharacters == null)
            return;

        foreach (Character character
                 in battleContext.AllCharacters)
        {
            if (character == null ||
                character.IsDead)
            {
                continue;
            }

            character.TurnEnd();
        }
    }

    private void HandleFatalError(
        string phase,
        Exception exception)
    {
        lifecycleGuard?.Fault(exception);

        Debug.LogError(
            "[TurnManager] 치명적 턴 처리 예외 / " +
            $"Phase={phase}, Turn={CurrentTurn}");

        if (exception != null)
            Debug.LogException(exception);

        try
        {
            fatalErrorHandler?.Invoke(exception);
        }
        catch (Exception handlerException)
        {
            Debug.LogException(handlerException);
        }
    }

    private static Exception InvokeCompletionSafely(
        Action onComplete)
    {
        if (onComplete == null)
            return null;

        Exception firstFailure = null;

        foreach (Delegate subscriber
                 in onComplete.GetInvocationList())
        {
            try
            {
                ((Action)subscriber)();
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;

                Debug.LogError(
                    "[TurnManager] ResolveTurn 완료 콜백 예외 격리");

                Debug.LogException(exception);
            }
        }

        return firstFailure;
    }

    private static BattleLifecycleGuard CreateReadyGuard()
    {
        BattleLifecycleGuard guard = new();
        guard.MarkReady();
        return guard;
    }

    private static void DisposeEnumerator(
        IEnumerator enumerator)
    {
        if (enumerator is not IDisposable disposable)
            return;

        try
        {
            disposable.Dispose();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}