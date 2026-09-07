using System.Collections.Generic;
using UnityEngine;

public sealed class AISkillDecision
{
    public Skill Skill;
    public TargetPoint TargetPoint;
    public float Score;

    public bool IsValid =>
        Skill != null &&
        TargetPoint.IsValid;
}

public sealed class AISkillSelector
{
    private readonly AITargetSelector targetSelector;

    public AISkillSelector(
        AITargetSelector targetSelector)
    {
        this.targetSelector = targetSelector;
    }

    public AISkillDecision SelectSkill(
        AIPlanningState state,
        AIActionSource source,
        int actionIndex)
    {
        if (state == null ||
            source == null ||
            !source.IsValid)
        {
            return null;
        }

        AISkillDecision bestDecision = null;
        float bestScore = float.NegativeInfinity;

        IReadOnlyList<Skill> slotSkills =
            source.GetSkills(actionIndex);

        foreach (Skill skill in slotSkills)
        {
            if (!CanSelectSkill(
                    state,
                    source,
                    skill))
            {
                continue;
            }

            AITargetSelection targetSelection =
                targetSelector.SelectTarget(
                    state,
                    source,
                    skill,
                    actionIndex);

            if (!targetSelection.IsValid)
                continue;

            float score =
                ScoreSkill(
                    state,
                    source,
                    skill,
                    actionIndex,
                    targetSelection);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestDecision =
                new AISkillDecision
                {
                    Skill = skill,
                    TargetPoint = targetSelection.TargetPoint,
                    Score = score
                };
        }

        return bestDecision;
    }

    private bool CanSelectSkill(
        AIPlanningState state,
        AIActionSource source,
        Skill skill)
    {
        if (skill == null ||
            state.Owner == null ||
            state.Owner.IsDead)
        {
            return false;
        }

        if (!state.Owner.CanUseSkill(
                source.Part,
                skill))
        {
            return false;
        }

        if (!skill.CanAIUse(
                state.Owner,
                source.Part,
                state.Context))
        {
            return false;
        }

        if (!state.CanPlanCombatSlot(skill))
            return false;

        if (!state.CanPlanEnergy(skill))
            return false;

        if (skill.ActionType == ActionType.Prestige &&
            !state.CanPlanPrestige(skill))
        {
            return false;
        }

        return true;
    }

    private float ScoreSkill(
        AIPlanningState state,
        AIActionSource source,
        Skill skill,
        int actionIndex,
        AITargetSelection targetSelection)
    {
        float score =
            targetSelection.Score;

        score += skill.ActionType switch
        {
            ActionType.Prestige => 10000f,
            ActionType.Duel => 135f,
            ActionType.NormalAttack => 105f,
            ActionType.Preparation => 65f,
            _ => 0f
        };

        float expectedPower =
            (skill.MinPower + skill.MaxPower) * 0.5f;

        score += expectedPower * 2f;

        if (skill.CanClash)
            score += 25f;

        if (skill.CanBreakPart &&
            targetSelection.TargetPoint.Part?.IsWeakened == true)
        {
            score += 160f;
        }

        int sameSkillCount =
            state.CountPlannedSkill(skill);

        score -= sameSkillCount * 18f;
        score -= actionIndex * 3f;

        // 도사림은 같은 턴에 여러 부위가 반복 사용하지 않도록 완만한 감점.
        if (skill.ActionType == ActionType.Preparation)
        {
            score -= state.CountActionType(
                ActionType.Preparation) * 45f;
        }

        score += UnityEngine.Random.Range(0f, 12f);

        return score;
    }
}