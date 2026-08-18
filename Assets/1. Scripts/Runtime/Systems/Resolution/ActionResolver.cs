using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionResolver
{
    private readonly BattleContext battleContext;
    private readonly ClashManager clashManager;

    private readonly IBattleActionReporter actionReporter;
    private readonly IBattleActionPresentation actionPresentation;

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

    // 기존 concrete 생성 경로 호환.
    public ActionResolver(
        BattleContext battleContext,
        ClashManager clashManager,
        BattleAnimationDirector battleAnimationDirector,
        SkillVisualProfile defaultVisualProfile)
        : this(
            battleContext,
            clashManager,
            new BattleActionLogReporter(
                battleContext?.Services?.BattleLogger),
            new BattleActionPresentationService(
                battleAnimationDirector,
                defaultVisualProfile))
    {
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

            // 굴림부터 HP 적용까지의 전투 결과를 먼저 확정합니다.
            ClashResultContext result =
                clashManager.ResolvePair(
                    pair);

            if (result == null)
                continue;

            // 결과 재생은 확정된 전투 데이터를 소비하기만 합니다.
            if (actionPresentation != null)
            {
                yield return actionPresentation
                    .PlayClash(result);
            }
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