using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct AITargetSelection
{
    public AITargetSelection(
        TargetPoint targetPoint,
        float score)
    {
        TargetPoint = targetPoint;
        Score = score;
    }

    public TargetPoint TargetPoint { get; }
    public float Score { get; }

    public bool IsValid =>
        TargetPoint.IsValid &&
        !float.IsNegativeInfinity(Score);

    public static AITargetSelection Invalid =>
        new AITargetSelection(
            default,
            float.NegativeInfinity);
}

public sealed class AITargetSelector
{
    private readonly ActionManager actionManager;

    public AITargetSelector(
        ActionManager actionManager)
    {
        this.actionManager = actionManager;
    }

    public AITargetSelection SelectTarget(
        AIPlanningState state,
        AIActionSource source,
        Skill skill,
        int actionIndex = 0)
    {
        if (state == null ||
            source == null ||
            skill == null)
        {
            return AITargetSelection.Invalid;
        }

        if (skill.ActionType == ActionType.Preparation)
        {
            return SelectSelfTarget(
                state.Owner,
                source.Part);
        }

        Character target =
            ChooseOpponent(state);

        if (target == null)
            return AITargetSelection.Invalid;

        bool includeBrokenParts =
            ShouldIncludeBrokenTargets(skill);

        IReadOnlyList<TargetPoint> targetPoints =
            target.GetTargetPoints(includeBrokenParts);

        CharacterSlotConfig slotConfig = source.GetSlotConfig(actionIndex);
        if (slotConfig?.TargetingPolicy == AITargetingPolicy.RandomValid)
            return SelectRandomValidTarget(targetPoints, includeBrokenParts);

        TargetPoint bestPoint = default;
        float bestScore = float.NegativeInfinity;

        foreach (TargetPoint point in targetPoints)
        {
            if (!IsValidPoint(
                    point,
                    includeBrokenParts))
            {
                continue;
            }

            float score =
                ScoreTargetPoint(
                    state,
                    source,
                    skill,
                    point);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestPoint = point;
        }

        return bestPoint.IsValid
            ? new AITargetSelection(
                bestPoint,
                bestScore)
            : AITargetSelection.Invalid;
    }

    public static bool ShouldIncludeBrokenTargets(
        Skill skill)
    {
        if (skill == null)
            return false;

        // 파괴 부위 재공격은 직접 HP 피해이므로 일반/결투 공격에는 유효하다.
        // 위세는 약화 부위 파괴나 조건부 효과를 노리는 경우가 많아서
        // 이미 파괴된 부위를 기본 후보에서 제외한다.
        return skill.ActionType == ActionType.NormalAttack ||
               skill.ActionType == ActionType.Duel;
    }

    private AITargetSelection SelectRandomValidTarget(
        IReadOnlyList<TargetPoint> points,
        bool allowBrokenPart)
    {
        if (points == null || points.Count == 0)
            return AITargetSelection.Invalid;

        List<TargetPoint> valid = new();
        for (int i = 0; i < points.Count; i++)
        {
            if (IsValidPoint(points[i], allowBrokenPart))
                valid.Add(points[i]);
        }

        if (valid.Count == 0)
            return AITargetSelection.Invalid;

        TargetPoint chosen = valid[UnityEngine.Random.Range(0, valid.Count)];
        return new AITargetSelection(chosen, 100f);
    }

    private AITargetSelection SelectSelfTarget(
        Enemy owner,
        BodyPart sourcePart)
    {
        if (owner == null || owner.IsDead)
            return AITargetSelection.Invalid;

        if (sourcePart != null &&
            !sourcePart.IsBroken &&
            owner.IsValidTargetPart(
                sourcePart,
                allowBrokenPart: false))
        {
            return new AITargetSelection(
                TargetPoint.ForBodyPart(
                    owner,
                    sourcePart),
                100f);
        }

        if (owner.IsSingleHpTarget &&
            owner.IsValidTargetPart(
                null,
                allowBrokenPart: false))
        {
            return new AITargetSelection(
                TargetPoint.ForCharacter(owner),
                100f);
        }

        IReadOnlyList<TargetPoint> points =
            owner.GetTargetPoints(
                includeBrokenParts: false);

        foreach (TargetPoint point in points)
        {
            if (IsValidPoint(point, false))
            {
                return new AITargetSelection(
                    point,
                    80f);
            }
        }

        return AITargetSelection.Invalid;
    }

    private Character ChooseOpponent(
        AIPlanningState state)
    {
        Character player =
            state.Context?.Player;

        if (player == null || player.IsDead)
            return null;

        return player;
    }

    private bool IsValidPoint(
        TargetPoint point,
        bool allowBrokenPart)
    {
        if (!point.IsValid ||
            point.Character.IsDead)
        {
            return false;
        }

        return point.Character.IsValidTargetPart(
            point.Part,
            allowBrokenPart);
    }

    private float ScoreTargetPoint(
        AIPlanningState state,
        AIActionSource source,
        Skill skill,
        TargetPoint point)
    {
        Character target = point.Character;
        BodyPart targetPart = point.Part;

        float score =
            UnityEngine.Random.Range(0f, 8f);

        if (targetPart == null)
        {
            float hpRate =
                target.MaxCombatHP <= 0
                    ? 1f
                    : (float)target.CurrentHP /
                      target.MaxCombatHP;

            score += 80f;
            score += (1f - hpRate) * 70f;
        }
        else
        {
            score += ScoreBodyPart(
                skill,
                targetPart);

            score += ScoreClashOpportunity(
                skill,
                target,
                targetPart);
        }

        int existingTargetCount =
            CountExistingTargetAssignments(
                state,
                target,
                targetPart);

        score += existingTargetCount == 0
            ? 45f
            : -30f * existingTargetCount;

        if (source.Part != null &&
            targetPart != null &&
            source.Part.Type == targetPart.Type)
        {
            score += 3f;
        }

        return score;
    }

    private float ScoreBodyPart(
        Skill skill,
        BodyPart targetPart)
    {
        if (targetPart == null)
            return float.NegativeInfinity;

        float score = 35f;

        if (targetPart.IsBroken)
        {
            // 파괴 부위 공격은 캐릭터 직접 피해지만 합 상대가 사라질 수 있으므로
            // 결투보다 일반공격이 조금 더 선호한다.
            score += skill.ActionType == ActionType.NormalAttack
                ? 85f
                : 55f;

            return score;
        }

        if (targetPart.IsWeakened)
        {
            // 약화 부위는 파괴 전까지 HP 피해를 받지 않는다.
            // 파괴 권한이 있는 스킬만 상태 전환 가치가 있다.
            score += skill.CanBreakPart
                ? 130f
                : -180f;
        }

        if (targetPart.MaxPartHP > 0f)
        {
            float hpRate =
                Mathf.Clamp01(
                    targetPart.PartHP /
                    targetPart.MaxPartHP);

            score += (1f - hpRate) * 75f;
        }

        return score;
    }

    private float ScoreClashOpportunity(
        Skill skill,
        Character target,
        BodyPart targetPart)
    {
        if (actionManager == null ||
            skill == null ||
            !skill.CanClash)
        {
            return 0f;
        }

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot == null ||
                slot.Owner != target ||
                slot.Part != targetPart ||
                slot.Skill?.CanClash != true)
            {
                continue;
            }

            return skill.ActionType == ActionType.Duel
                ? 120f
                : 65f;
        }

        return 0f;
    }

    private int CountExistingTargetAssignments(
        AIPlanningState state,
        Character target,
        BodyPart targetPart)
    {
        int count = 0;

        if (actionManager != null)
        {
            foreach (ActionSlot slot in actionManager.Slots)
            {
                if (slot == null ||
                    slot.Owner == state.Context?.Player)
                {
                    continue;
                }

                if (IsSameTarget(
                        slot.TargetCharacter,
                        slot.TargetPart,
                        target,
                        targetPart))
                {
                    count++;
                }
            }
        }

        foreach (ActionSlot slot in state.PlannedSlots)
        {
            if (IsSameTarget(
                    slot?.TargetCharacter,
                    slot?.TargetPart,
                    target,
                    targetPart))
            {
                count++;
            }
        }

        return count;
    }

    private bool IsSameTarget(
        Character firstCharacter,
        BodyPart firstPart,
        Character secondCharacter,
        BodyPart secondPart)
    {
        return firstCharacter == secondCharacter &&
               firstPart == secondPart;
    }
}