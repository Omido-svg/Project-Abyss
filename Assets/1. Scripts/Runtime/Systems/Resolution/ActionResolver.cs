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

    public IEnumerator Resolve(
        ActionExecutionQueue executionQueue)
    {
        if (executionQueue == null)
            yield break;

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

                ExecuteSkill(action);

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

            ClashResultContext result =
                clashManager.ResolvePair(pair);

            if (result == null)
                continue;

            BattleAction visualAction =
                result.WinnerAction;

            if (visualAction == null)
                continue;

            List<ClashRollVisualStep> steps =
                null;

            if (result.IsClash)
            {
                steps =
                    result.ClashSteps;

                if (steps == null ||
                    steps.Count == 0)
                {
                    steps =
                        new List<ClashRollVisualStep>
                        {
                            new(
                                result.WinnerClashPower,
                                result.LoserClashPower)
                        };
                }
            }

            Debug.Log(
                $"[ActionResolver] Visual 재생 요청 / " +
                $"ActionId={visualAction.ActionId}, " +
                $"ActionIndex={visualAction.ActionIndex}, " +
                $"IsClash={result.IsClash}, " +
                $"Actor={visualAction.Owner?.Data?.CharacterName}, " +
                $"Target={visualAction.Target?.Data?.CharacterName}, " +
                $"Skill={visualAction.Skill?.SkillName}, " +
                $"Steps={(steps == null ? 0 : steps.Count)}, " +
                $"Damages={(result.HitDamages == null ? 0 : result.HitDamages.Count)}");

            yield return PlayActionVisual(
                visualAction,
                steps,
                result.HitDamages,
                result.LoserAction,
                result.HasTargetPartHpSnapshot
                    ? result.TargetPartHpBefore
                    : null,
                result.HasTargetPartHpSnapshot
                    ? result.TargetPartHpAfter
                    : null,
                result.DamageContext);
        }
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
        if (action == null ||
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
            action?.LastDamageContext
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

    private void ExecuteSkill(
        BattleAction action)
    {
        if (action?.Skill == null)
            return;

        action.Skill.Execute(action);

        action.Skill.ConsumeResource(
            action.Owner);
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
