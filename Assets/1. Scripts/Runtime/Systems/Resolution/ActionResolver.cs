using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionResolver
{
    private readonly BattleContext battleContext;
    private readonly ClashManager clashManager;

    private readonly BattleAnimationDirector battleAnimationDirector;
    private readonly BattleVisualRequestBuilder visualRequestBuilder;

    public ActionResolver(
        BattleContext battleContext,
        ClashManager clashManager,
        BattleAnimationDirector battleAnimationDirector,
        SkillVisualProfile defaultVisualProfile)
    {
        this.battleContext = battleContext;
        this.clashManager = clashManager;
        this.battleAnimationDirector =
            battleAnimationDirector;

        visualRequestBuilder =
            new BattleVisualRequestBuilder(
                defaultVisualProfile);
    }

    /// <summary>
    /// 구형 호출부/검증 도구 호환용 즉시 도사림 실행 경로.
    /// 플레이어 UI의 정상 경로는 ActionManager에 FORESIGHT 슬롯을 계획하고
    /// START 이후 PreparationQueue에서 실행한다.
    /// </summary>
    public bool ExecutePlanningPreparation(ActionSlot slot)
    {
        if (slot == null ||
            slot.Skill == null ||
            slot.Owner == null ||
            slot.Skill.ActionType != ActionType.Preparation)
        {
            return false;
        }

        BattleAction action = CreateBattleAction(slot);
        bool started = false;

        try
        {
            battleContext._battleEvent.RaiseActionStart(action);
            started = true;

            if (!ExecuteSkill(action))
                return false;

            battleContext.battleManager.BattleLogger.LogAction(
                action,
                BattleLogType.Preparation);

            return true;
        }
        finally
        {
            if (started)
                battleContext._battleEvent.RaiseActionEnd(action);
        }
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

                BattleLogType type =
                    action.Phase switch
                    {
                        ActionPhase.PRETURN =>
                            BattleLogType.Prestige,

                        ActionPhase.FORESIGHT =>
                            BattleLogType.Preparation,

                        _ =>
                            BattleLogType.Normal
                    };

                if (action.HasDamageLog)
                {
                    battleContext.battleManager
                        .BattleLogger
                        .LogDamage(
                            action,
                            type,
                            action.LoggedDamage,
                            action.LoggedBeforeHP,
                            action.LoggedAfterHP);
                }
                else
                {
                    battleContext.battleManager
                        .BattleLogger
                        .LogAction(
                            action,
                            type);
                }

                yield return PlayActionVisual(
                    action,
                    clashSteps: null);
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
            yield return PlayResolvedClashResult(
                result);
        }
    }

    private IEnumerator PlayResolvedClashResult(
        ClashResultContext result)
    {
        if (result == null ||
            BattleSimulationRuntime.IsBatchSimulation ||
            battleAnimationDirector == null)
        {
            yield break;
        }

        BattleVisualRequest request =
            visualRequestBuilder.BuildClashSequence(
                result);

        if (request == null)
            yield break;

        yield return battleAnimationDirector
            .Play(request);
    }

    private IEnumerator PlayActionVisual(
        BattleAction action,
        List<ClashRollVisualStep> clashSteps,
        List<int> hitDamagesOverride = null,
        BattleAction opponentAction = null,
        int? targetPartHpBefore = null,
        int? targetPartHpAfter = null,
        DamageContext damageContext = null)
    {
        if (BattleSimulationRuntime.IsBatchSimulation ||
            action == null ||
            battleAnimationDirector == null)
        {
            yield break;
        }

        List<int> hitDamages =
            hitDamagesOverride ??
            CreateHitDamagesFromAction(
                action);

        BattleVisualRequest request =
            visualRequestBuilder.Build(
                action,
                clashSteps,
                hitDamages,
                opponentAction,
                targetPartHpBefore,
                targetPartHpAfter,
                damageContext);

        if (request == null)
            yield break;

        yield return battleAnimationDirector
            .Play(request);
    }

    private List<int> CreateHitDamagesFromAction(
        BattleAction action)
    {
        List<int> result = new();

        int resolvedDamage =
            action?.PrimaryDamageContext
                ?.GetDisplayDamage() ?? 0;

        if (resolvedDamage > 0)
        {
            result.Add(resolvedDamage);
            return result;
        }

        if (action?.HasDamageLog == true &&
            action.LoggedDamage > 0)
        {
            result.Add(
                action.LoggedDamage);
        }

        return result;
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