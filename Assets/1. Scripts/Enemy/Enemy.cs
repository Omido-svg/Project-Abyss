using System.Collections.Generic;
using UnityEngine;

public abstract class Enemy : Character
{
    //--------------------------------------------------
    // AI 행동 결정
    //--------------------------------------------------

    public List<ActionSlot> DecideSlots(BattleContext context)
    {
        List<ActionSlot> result = new();

        if (context == null)
            return result;

        if (context.battleManager == null)
            return result;

        if (context.battleManager.SpeedManager == null)
            return result;

        if (BodyParts == null)
            return result;

        Character target = ChooseTarget(context);

        if (target == null)
            return result;

        bool prestigeSelectedThisTurn = false;

        foreach (BodyPart part in BodyParts)
        {
            ActionSlot slot =
                SelectBestSlotForPart(
                    context,
                    part,
                    target,
                    !prestigeSelectedThisTurn,
                    out bool selectedPrestige);

            if (slot == null)
                continue;

            result.Add(slot);

            if (selectedPrestige)
                prestigeSelectedThisTurn = true;
        }

        return result;
    }

    //--------------------------------------------------
    // 공격 대상 선택
    //--------------------------------------------------

    protected virtual Character ChooseTarget(BattleContext context)
    {
        if (context == null)
            return null;

        return context.Player;
    }

    //--------------------------------------------------
    // 부위별 최선 행동 선택
    //--------------------------------------------------

    protected virtual ActionSlot SelectBestSlotForPart(
        BattleContext context,
        BodyPart part,
        Character target,
        bool allowPrestige,
        out bool selectedPrestige)
    {
        selectedPrestige = false;

        if (context == null)
            return null;

        if (context.battleManager == null)
            return null;

        if (context.battleManager.SpeedManager == null)
            return null;

        if (part == null)
            return null;

        if (!part.IsUsable)
            return null;

        if (target == null)
            return null;

        if (target.BodyParts == null ||
            target.BodyParts.Count == 0)
            return null;

        if (part.AvailableSkills == null ||
            part.AvailableSkills.Count == 0)
            return null;

        List<ActionSlot> prestigeCandidates = new();
        List<ActionSlot> normalCandidates = new();

        foreach (Skill skill in part.AvailableSkills)
        {
            if (skill == null)
                continue;

            if (!IsSkillUsable(part, skill))
                continue;

            if (skill.ActionType == ActionType.Prestige)
            {
                if (!allowPrestige)
                    continue;

                if (!IsPrestigeReady())
                    continue;
            }

            foreach (BodyPart targetPart in target.BodyParts)
            {
                if (targetPart == null)
                    continue;

                if (!IsTargetPartSelectable(targetPart))
                    continue;

                ActionSlot slot = new ActionSlot
                {
                    Owner = this,
                    Part = part,

                    Skill = skill,

                    TargetCharacter = target,
                    TargetPart = targetPart,

                    Speed = context.battleManager.SpeedManager.GetSpeed(part),

                    Phase = CalculateActionPhase(skill)
                };

                if (skill.ActionType == ActionType.Prestige)
                    prestigeCandidates.Add(slot);
                else
                    normalCandidates.Add(slot);
            }
        }

        // 1순위: 위세 게이지가 다 찼으면 Prestige 행동 중 최선 선택
        if (allowPrestige &&
            IsPrestigeReady() &&
            prestigeCandidates.Count > 0)
        {
            selectedPrestige = true;

            return SelectHighestScoreSlot(
                context,
                prestigeCandidates);
        }

        // 2순위: 일반 전투 행동 중 최선 선택
        if (normalCandidates.Count > 0)
        {
            return SelectHighestScoreSlot(
                context,
                normalCandidates);
        }

        return null;
    }

    //--------------------------------------------------
    // 점수가 가장 높은 슬롯 선택
    //--------------------------------------------------

    protected ActionSlot SelectHighestScoreSlot(
        BattleContext context,
        List<ActionSlot> candidates)
    {
        if (candidates == null ||
            candidates.Count == 0)
            return null;

        ActionSlot bestSlot = null;
        float bestScore = float.MinValue;

        foreach (ActionSlot slot in candidates)
        {
            float score =
                ScoreSlot(
                    context,
                    slot);

            if (score > bestScore)
            {
                bestScore = score;
                bestSlot = slot;
            }
        }

        return bestSlot;
    }

    //--------------------------------------------------
    // 행동 점수 계산
    //--------------------------------------------------

    protected virtual float ScoreSlot(
        BattleContext context,
        ActionSlot slot)
    {
        if (slot == null)
            return float.MinValue;

        if (slot.Skill == null)
            return float.MinValue;

        if (slot.TargetPart == null)
            return float.MinValue;

        float score = 0f;

        // 약간의 랜덤성
        score += Random.Range(0f, 5f);

        // 스킬 타입별 기본 점수
        switch (slot.Skill.ActionType)
        {
            case ActionType.Prestige:
                score += 10000f;
                break;

            case ActionType.Duel:
                score += 80f;
                break;

            case ActionType.NormalAttack:
                score += 60f;
                break;

            case ActionType.Preparation:
                score -= 10000f;
                break;
        }

        // 합 가능한 스킬 선호
        if (slot.Skill.CanClash)
            score += 30f;

        // 타겟 부위 상태 반영
        score += ScoreTargetPart(slot.TargetPart);

        // 자신의 속도가 높을수록 약간 선호
        score += slot.Speed * 3f;

        return score;
    }

    //--------------------------------------------------
    // 타겟 부위 점수
    //--------------------------------------------------

    protected virtual float ScoreTargetPart(BodyPart targetPart)
    {
        if (targetPart == null)
            return -10000f;

        float score = 0f;

        // 기존 로직에서 부서진 부위를 70% 선호했으므로,
        // 여기서는 부서진 부위에 높은 점수 부여
        if (targetPart.IsBroken)
            score += 70f;
        else
            score += 20f;

        // 약화된 부위는 공격 가치 높음
        if (targetPart.IsWeakened)
            score += 40f;

        // HP가 낮은 부위를 마무리하려는 경향
        if (targetPart.MaxPartHP > 0)
        {
            float hpRate =
                targetPart.PartHP / targetPart.MaxPartHP;

            float missingHpRate =
                1f - hpRate;

            score += missingHpRate * 50f;
        }

        return score;
    }

    //--------------------------------------------------
    // 타겟 부위 선택 가능 여부
    //--------------------------------------------------

    protected virtual bool IsTargetPartSelectable(BodyPart targetPart)
    {
        if (targetPart == null)
            return false;

        // 지금 네 기존 로직은 부서진 부위도 공격 가능하게 되어 있었음.
        // 그래서 여기서는 broken도 허용함.
        return true;
    }

    //--------------------------------------------------
    // 스킬 사용 가능 여부
    //--------------------------------------------------

    protected virtual bool IsSkillUsable(
        BodyPart part,
        Skill skill)
    {
        if (part == null)
            return false;

        if (skill == null)
            return false;

        if (part.IsBroken)
            return false;

        // 일반 Enemy는 도사림 / Preparation 사용 금지
        if (skill.ActionType == ActionType.Preparation)
            return false;

        // Prestige는 위세 게이지가 다 찼을 때만 사용 가능
        if (skill.ActionType == ActionType.Prestige)
        {
            if (!IsPrestigeReady())
                return false;
        }

        // Prestige가 아닌 일반 행동은 합 가능한 스킬만 허용
        if (skill.ActionType != ActionType.Prestige)
        {
            if (!skill.CanClash)
                return false;
        }

        if (!CanUseSkill(part, skill))
            return false;

        return true;
    }

    //--------------------------------------------------
    // 위세 게이지 확인
    //--------------------------------------------------

    protected bool IsPrestigeReady()
    {
        if (CurrentStatus.maxPrestige <= 0)
            return false;

        return RuntimeStatus.currentPrestige >= CurrentStatus.maxPrestige;
    }

    //--------------------------------------------------
    // ActionPhase 계산
    //--------------------------------------------------

    protected ActionPhase CalculateActionPhase(Skill skill)
    {
        if (skill == null)
            return ActionPhase.COMBAT;

        return skill.ActionType switch
        {
            ActionType.Prestige     => ActionPhase.PRETURN,
            ActionType.Preparation  => ActionPhase.FORESIGHT,
            ActionType.NormalAttack => ActionPhase.COMBAT,
            ActionType.Duel         => ActionPhase.COMBAT,
            _                       => ActionPhase.COMBAT
        };
    }
}