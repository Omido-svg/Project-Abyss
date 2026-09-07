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

        SkillDefinition requiredFixedDefinition =
            ResolveFixedSlotDefinition(
                state,
                source,
                actionIndex,
                slotSkills);

        foreach (Skill skill in slotSkills)
        {
            // FixedSkill 슬롯은 평상시에는 FixedSkill만 평가한다.
            // InsufficientEnergyFallbackSkill은 FixedSkill이 "현재 AI 계획의 남은 Energy"에
            // 들어오지 않을 때에만 후보가 된다. 다른 사용 불가 사유를 fallback으로 숨기지 않는다.
            if (requiredFixedDefinition != null &&
                skill?.Definition != requiredFixedDefinition)
            {
                continue;
            }

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

    private static SkillDefinition ResolveFixedSlotDefinition(
        AIPlanningState state,
        AIActionSource source,
        int actionIndex,
        IReadOnlyList<Skill> slotSkills)
    {
        CharacterSlotConfig config =
            source?.GetSlotConfig(actionIndex);

        if (config?.FixedSkill == null)
            return null;

        // FixedSkill이 런타임 목록에 없으면 fallback으로 조용히 대체하지 않는다.
        // 데이터 연결 오류를 숨기지 않고 기존 FixedSkill 계약을 유지한다.
        Skill fixedRuntimeSkill =
            FindRuntimeSkill(
                slotSkills,
                config.FixedSkill);

        if (fixedRuntimeSkill == null)
            return config.FixedSkill;

        // fallback은 오직 "계획된 앞 슬롯들 때문에 남은 Energy가 부족한 경우"에만 사용한다.
        // 실제 ScriptableObject를 변경하지 않고 별도의 runtime fallback definition을 선택한다.
        if (config.InsufficientEnergyFallbackSkill != null &&
            state != null &&
            !state.CanPlanEnergy(fixedRuntimeSkill))
        {
            return config.InsufficientEnergyFallbackSkill;
        }

        return config.FixedSkill;
    }

    private static Skill FindRuntimeSkill(
        IReadOnlyList<Skill> skills,
        SkillDefinition definition)
    {
        if (skills == null || definition == null)
            return null;

        for (int i = 0; i < skills.Count; i++)
        {
            Skill skill = skills[i];
            if (skill?.Definition == definition)
                return skill;
        }

        return null;
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