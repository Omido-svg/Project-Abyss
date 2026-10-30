using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectAbyss.Abilities;

public class ActionResolver
{
    private readonly BattleContext battleContext;
    private readonly ClashManager clashManager;

    private readonly IBattleActionReporter actionReporter;
    private readonly IBattleActionPresentation actionPresentation;
    private BattleReactionScheduler reactionScheduler;
    private R6ReactionActionRegistry reactionRegistry;
    private bool useR6StepwiseClashes;

    /// <summary>
    /// R6-Ib opt-in observation of a fully committed exchange, emitted
    /// AFTER damage/events and BEFORE reaction checkpoint pumping.
    /// Subscribers may enqueue requests only, not recursively resolve damage.
    /// </summary>
    public event System.Action<ClashExchangeResult> R6ExchangeSettled;

    public void EnableR6StepwiseClashes()
    {
        if(reactionScheduler==null || reactionRegistry==null)
            throw new System.InvalidOperationException(
                "R6-Ib requires an existing reaction scheduler and action registry.");
        if(!(actionPresentation is IBattleActionStepPresentation))
            throw new System.InvalidOperationException(
                "R6-Ic requires a step presenter to prevent duplicated/out-of-order playback.");
        r6StepTrace.Clear();
        useR6StepwiseClashes=true;
    }

    public void DisableR6StepwiseClashes() => useR6StepwiseClashes=false;
    private readonly List<R6ReactionExecutionRecord> reactionExecutionTrace =
        new List<R6ReactionExecutionRecord>();
    private readonly List<R6StepTraceRecord> r6StepTrace =
        new List<R6StepTraceRecord>();

    public IReadOnlyList<R6StepTraceRecord> R6StepTrace =>
        r6StepTrace.AsReadOnly();

    private void Trace(R6StepTraceKind kind, long actionId,
        long rootActionId, long causeEventId, int exchangeIndex)
    {
        r6StepTrace.Add(new R6StepTraceRecord(
            r6StepTrace.Count, kind, actionId, rootActionId,
            causeEventId, exchangeIndex));
    }

    public IReadOnlyList<R6ReactionExecutionRecord> ReactionExecutionTrace =>
        reactionExecutionTrace.AsReadOnly();

    /// <summary>
    /// R6-Ia opt-in only. Existing BattleRuntimeFactory does not bind this
    /// automatically, preventing duplicate legacy Hifumi counter execution.
    /// </summary>
    public void EnableR6ReactionExecution(
        BattleReactionScheduler scheduler,R6ReactionActionRegistry registry)
    {
        reactionScheduler=scheduler ?? throw new System.ArgumentNullException(nameof(scheduler));
        reactionRegistry=registry ?? throw new System.ArgumentNullException(nameof(registry));
        reactionExecutionTrace.Clear();
    }

    public void DisableR6ReactionExecution()
    {
        reactionScheduler=null;
        reactionRegistry=null;
        reactionExecutionTrace.Clear();
        useR6StepwiseClashes=false;
    }

    public ActionResolver(
        BattleContext battleContext,
        ClashManager clashManager,
        IBattleActionReporter actionReporter,
        IBattleActionPresentation actionPresentation)
    {
        this.battleContext = battleContext;
        this.clashManager = clashManager;
        this.actionReporter = actionReporter;
        this.actionPresentation = actionPresentation;
    }

    public IEnumerator Resolve(
        ActionExecutionQueue executionQueue)
    {
        if (executionQueue == null)
            yield break;

        // START 전에는 도사림을 계획 상태로 유지하고,
        // 확정 이후 PRETURN -> FORESIGHT -> COMBAT 순서로 실행한다.
        yield return ResolveStandaloneQueue(
            executionQueue.PrestigeQueue);

        yield return ResolveStandaloneQueue(
            executionQueue.PreparationQueue);

        yield return ResolveClashQueue(
            executionQueue.ClashQueue);
    }

    private IEnumerator ResolveStandaloneQueue(
        Queue<ActionSlot> queue)
    {
        if (queue == null)
            yield break;

        while (queue.Count > 0)
        {
            ActionSlot slot =
                queue.Dequeue();

            if (!CanExecuteSlot(slot))
                continue;

            BattleAction action =
                CreateBattleAction(slot);

            bool actionStarted = false;

            try
            {
                battleContext._battleEvent
                    .RaiseActionStart(action);

                actionStarted = true;

                if (!ExecuteSkill(action))
                    continue;

                actionReporter?
                    .ReportStandaloneAction(action);

                if (actionPresentation != null)
                {
                    yield return actionPresentation
                        .PlayStandalone(action);
                }
            }
            finally
            {
                if (actionStarted)
                {
                    battleContext._battleEvent
                        .RaiseActionEnd(action);
                }
            }

            // Standalone RaiseActionEnd is finalized in finally above.
            if(reactionScheduler!=null && reactionRegistry!=null)
                yield return PumpR6ReactionCheckpoint(
                    ReactionTimingCheckpoint.AfterActionCompleted);
        }
    }

    private IEnumerator ResolveClashQueue(
        Queue<ClashPair> clashQueue)
    {
        if (clashQueue == null)
            yield break;

        while (clashQueue.Count > 0)
        {
            ClashPair pair =
                clashQueue.Dequeue();

            ClashResultContext result;
            bool streamed = useR6StepwiseClashes &&
                reactionScheduler!=null && reactionRegistry!=null;
            if(streamed)
            {
                // A single coordinator, no recursive ResolvePair from a
                // BattleEvent callback. Default production ResolvePair is
                // preserved unless the caller explicitly opts in.
                using(var session=clashManager.BeginStepwise(pair))
                {
                    while(!session.IsComplete)
                    {
                        bool committed=session.Advance(out var exchange);
                        if(!committed)
                            break;
                        // This is a new *notification*, not a new gameplay
                        // ExchangeResolved event or additional damage.
                        R6ExchangeSettled?.Invoke(exchange);
                        long originalId=session.RootActionId;
                        Trace(R6StepTraceKind.ExchangeCommitted,originalId,
                            originalId,0,exchange?.ExchangeIndex ?? -1);
                        // Timeline must display this factual exchange BEFORE
                        // any reaction and BEFORE a future original exchange.
                        yield return ((IBattleActionStepPresentation)actionPresentation)
                            .PlayCommittedExchange(session.Result,exchange);
                        Trace(R6StepTraceKind.ExchangeVisualCompleted,originalId,
                            originalId,0,exchange?.ExchangeIndex ?? -1);
                        yield return PumpR6ReactionCheckpoint(
                            ReactionTimingCheckpoint.AfterExchangeSettled);
                        session.ResumeAfterCheckpoint();
                    }
                    result=session.Result;
                }
            }
            else
                result=clashManager.ResolvePair(pair);

            if(result==null)
                continue;

            // The R6-Ic stream already played EVERY committed exchange.
            // Do not replay the complete root result and duplicate damage UI.
            if (!streamed && actionPresentation != null)
            {
                yield return actionPresentation
                    .PlayClash(result);
            }
            if(streamed)
                Trace(R6StepTraceKind.OriginalActionCompleted,
                    result.FirstAction?.ActionId ?? 0, result.FirstAction?.ActionId ?? 0,
                    0,-1);

            // ResolvePair and its RaiseClashResolved callbacks have completely
            // returned. The original result presentation also precedes newly
            // inserted reaction presentation. NEVER pump in event handlers.
            if(reactionScheduler!=null && reactionRegistry!=null)
            {
                yield return PumpR6ReactionCheckpoint(
                    ReactionTimingCheckpoint.AfterClashCompleted);
                yield return PumpR6ReactionCheckpoint(
                    ReactionTimingCheckpoint.AfterActionCompleted);
            }
        }
    }

    private IEnumerator PumpR6ReactionCheckpoint(
        ReactionTimingCheckpoint checkpoint)
    {
        if(reactionScheduler==null || reactionRegistry==null)
            yield break;

        while(reactionScheduler.TryDequeue(checkpoint,out var request))
        {
            if(!reactionRegistry.TryTake(request,out var slot,out var problem))
            {
                reactionExecutionTrace.Add(
                    new R6ReactionExecutionRecord(request,0,false,problem));
                Debug.LogWarning("[R6-Exec] Reaction request rejected at checkpoint: "+
                    request.RequestId+" / "+problem);
                continue;
            }

            // Re-validate at execution time, AFTER other actions may have
            // killed the target/broken the slot. No new execution on stale slot.
            // Never collide with a planned action ID already in ActionManager.
            bool idCollision=battleContext?.Services?.ActionManager
                ?.FindSlotById(slot.ActionId)!=null;
            bool hostileSide=battleContext!=null &&
                (slot.Owner==battleContext.Player &&
                 battleContext.Enemies!=null &&
                 battleContext.Enemies.Contains(slot.TargetCharacter) ||
                 battleContext.Enemies!=null &&
                 battleContext.Enemies.Contains(slot.Owner) &&
                 slot.TargetCharacter==battleContext.Player);
            if(!CanExecuteSlot(slot) || slot.TargetCharacter==null ||
                slot.TargetCharacter==slot.Owner ||
                idCollision || !hostileSide)
            {
                reactionExecutionTrace.Add(
                    new R6ReactionExecutionRecord(request,slot.ActionId,false,
                        "Reaction slot no longer executable."));
                continue;
            }

            // A reaction is an independent ONE-SIDED action, never an automatic
            // extra clash against the reaction target's next planned slot.
            // Existing ClashManager remains the only damage/roll authority.
            ClashResultContext result=clashManager.ResolvePair(new ClashPair(slot));
            if(result==null)
            {
                reactionExecutionTrace.Add(
                    new R6ReactionExecutionRecord(request,slot.ActionId,false,
                        "ClashManager returned no executable reaction result."));
                continue;
            }

            reactionExecutionTrace.Add(
                new R6ReactionExecutionRecord(request,slot.ActionId,true,
                    "One-sided reaction resolved by ClashManager."));

            if(useR6StepwiseClashes)
                Trace(R6StepTraceKind.ReactionCommitted,slot.ActionId,
                    request.RootActionId,request.CauseEventId,-1);
            // An actual reaction result is played after its own commit,
            // before the original action resumes its next exchange.
            if(actionPresentation!=null)
                yield return actionPresentation.PlayClash(result);
            if(useR6StepwiseClashes)
                Trace(R6StepTraceKind.ReactionVisualCompleted,slot.ActionId,
                    request.RootActionId,request.CauseEventId,-1);
        }
    }







    private bool ExecuteSkill(
        BattleAction action)
    {
        if (action?.Skill == null || action.Owner == null)
            return false;

        if (!action.Skill.TryConsumeResource(
                action.Owner,
                action))
        {
            Debug.LogWarning(
                $"[ActionResolver] 행동 비용 부족으로 실행 취소 / " +
                $"Owner={action.Owner.Data?.CharacterName ?? action.Owner.name}, " +
                $"Skill={action.Skill.SkillName}, " +
                $"Energy={action.Owner.CurrentEnergy}/" +
                $"{action.Owner.MaxEnergy}, Cost={action.Skill.EnergyCost}");
            return false;
        }

        // 비용 검증/소비가 성공한 실제 사용만 "사용 전" 페이즈를 발행한다.
        action.Skill.NotifyBeforeUse(action);
        action.Skill.Execute(action);
        return true;
    }

    private BattleAction CreateBattleAction(
        ActionSlot slot)
    {
        return new BattleAction
        {
            Slot = slot
        };
    }

    private bool CanExecuteSlot(
        ActionSlot slot)
    {
        if (slot == null ||
            slot.Owner == null ||
            slot.Skill == null)
        {
            return false;
        }

        if (slot.Owner.IsDead)
            return false;

        if (slot.TargetCharacter != null &&
            slot.TargetCharacter.IsDead)
        {
            return false;
        }

        // OwnerPart가 null인 독립 위세와
        // 이후 단일 HP 일반몹 행동을 허용한다.
        if (slot.Part != null &&
            slot.Part.IsBroken)
        {
            Debug.Log(
                $"[SKIP SLOT - BROKEN OWNER PART] " +
                $"ActionId={slot.ActionId}, " +
                $"{slot.Owner.Data?.CharacterName} / " +
                $"{slot.Part.Type} / " +
                $"{slot.Skill?.SkillName}");

            return false;
        }

        // TargetPart가 Broken이어도 공격 가능하다.
        return true;
    }
}