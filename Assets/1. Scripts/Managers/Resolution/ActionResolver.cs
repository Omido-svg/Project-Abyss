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
        this.battleAnimationDirector = battleAnimationDirector;

        visualRequestBuilder =
            new BattleVisualRequestBuilder(
                defaultVisualProfile);
    }

    //------------------------------------------------

    public IEnumerator Resolve(ActionExecutionQueue executionQueue)
    {
        if (executionQueue == null)
            yield break;

        yield return ResolveQueue(executionQueue.PrestigeQueue);

        yield return ResolveQueue(executionQueue.AmbushQueue);

        yield return ResolveClashQueue(executionQueue.ClashQueue);
    }

    //------------------------------------------------

    private IEnumerator ResolveQueue(Queue<ActionSlot> queue)
    {
        if (queue == null)
            yield break;

        while (queue.Count > 0)
        {
            ActionSlot slot = queue.Dequeue();

            if (!CanExecuteSlot(slot))
                continue;

            if (!IsValidSlot(slot))
                continue;

            BattleAction action =
                CreateBattleAction(slot);

            battleContext._battleEvent.RaiseActionStart(
                action);

            ExecuteSkill(action);

            BattleLogType type = action.Phase switch
            {
                ActionPhase.PRETURN => BattleLogType.Prestige,
                ActionPhase.FORESIGHT => BattleLogType.Preparation,
                _ => BattleLogType.Normal
            };

            if (action.HasDamageLog)
            {
                battleContext.battleManager.BattleLogger.LogDamage(
                    action,
                    type,
                    action.LoggedDamage,
                    action.LoggedBeforeHP,
                    action.LoggedAfterHP);
            }
            else
            {
                battleContext.battleManager.BattleLogger.LogAction(
                    action,
                    type);
            }

            // 여기 추가
            yield return PlayActionVisual(
                action,
                clashSteps: null);

            battleContext._battleEvent.RaiseActionEnd(
                action);
        }
    }
    
    private IEnumerator PlayActionVisual(
        BattleAction action,
        List<ClashRollVisualStep> clashSteps,
        List<int> hitDamagesOverride = null,
        BattleAction opponentAction = null)
    {
        if (action == null)
            yield break;

        if (battleAnimationDirector == null)
            yield break;

        List<int> hitDamages =
            hitDamagesOverride != null
                ? hitDamagesOverride
                : CreateHitDamagesFromAction(action);

        BattleVisualRequest request =
            visualRequestBuilder.Build(
                action,
                clashSteps,
                hitDamages,
                opponentAction);

        if (request == null)
            yield break;

        yield return battleAnimationDirector.Play(request);
    }

    private List<int> CreateHitDamagesFromAction(BattleAction action)
    {
        List<int> result =
            new List<int>();

        if (action == null)
            return result;

        if (action.HasDamageLog)
        {
            result.Add(action.LoggedDamage);
        }

        return result;
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

            List<ClashRollVisualStep> steps = null;

            if (result.IsClash)
            {
                steps =
                    result.ClashSteps;

                if (steps == null || steps.Count == 0)
                {
                    steps =
                        new List<ClashRollVisualStep>
                        {
                            new ClashRollVisualStep(
                                result.WinnerClashPower,
                                result.LoserClashPower)
                        };
                }
            }
            
            Debug.Log(
                $"[ActionResolver] Visual 재생 요청 / " +
                $"IsClash={result.IsClash}, " +
                $"Actor={visualAction.Owner?.Data.CharacterName}, " +
                $"Target={visualAction.Target?.Data.CharacterName}, " +
                $"Skill={visualAction.Skill?.SkillName}, " +
                $"Steps={(steps == null ? 0 : steps.Count)}, " +
                $"Damages={(result.HitDamages == null ? 0 : result.HitDamages.Count)}");

                yield return PlayActionVisual(
                    visualAction,
                    steps,
                    result.HitDamages,
                    result.LoserAction);
        }
    }

    //------------------------------------------------
    // 공통 스킬 실행
    //------------------------------------------------

    private void ExecuteSkill(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Skill == null)
            return;

        action.Skill.Execute(action);

        action.Skill.ConsumeResource(
            action.Owner);
    }

    //------------------------------------------------

    private bool IsValidSlot(ActionSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.Owner == null)
            return false;

        if (slot.TargetCharacter == null)
            return false;

        if (slot.Part == null)
            return false;

        if (slot.TargetPart == null)
            return false;

        if (slot.Skill == null)
        {
            Debug.LogWarning("Skill NULL");
            return false;
        }

        if (slot.Owner.IsDead)
            return false;

        if (slot.TargetCharacter.IsDead)
            return false;

        if (slot.Part.IsBroken)
            return false;

        if (slot.TargetPart.IsBroken)
            return false;

        return true;
    }

    //------------------------------------------------

    private BattleAction CreateBattleAction(ActionSlot slot)
    {
        return new BattleAction
        {
            Slot = slot
        };
    }
    
    private bool CanExecuteSlot(ActionSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.Owner == null)
            return false;

        if (slot.Owner.IsDead)
            return false;

        if (slot.Part == null)
            return false;

        if (slot.Part.IsBroken)
        {
            Debug.Log(
                $"[SKIP SLOT - BROKEN PART] " +
                $"{slot.Owner.Data.CharacterName} / " +
                $"{slot.Part.Type} / " +
                $"{slot.Skill?.SkillName}");

            return false;
        }

        return true;
    }
}