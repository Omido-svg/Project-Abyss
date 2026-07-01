using System.Collections.Generic;
using UnityEngine;

public abstract class Enemy : Character
{
    //--------------------------------------------------
    // 기본 AI 정책
    //--------------------------------------------------

    protected virtual bool AllowPreparationSkillAI => false;

    protected virtual bool RequireClashForCombatSkill => true;

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

        Character target =
            ChooseTarget(context);

        if (target == null)
            return result;

        foreach (BodyPart part in BodyParts)
        {
            if (part == null)
                continue;

            if (!part.IsUsable)
                continue;

            int maxSlotCount =
                GetMaxActionSlotsForPart(part);

            if (maxSlotCount <= 0)
                continue;

            for (int actionIndex = 0;
                 actionIndex < maxSlotCount;
                 actionIndex++)
            {
                ActionSlot slot =
                    SelectBestSlotForPart(
                        context,
                        part,
                        target,
                        actionIndex,
                        result);

                if (slot == null)
                    continue;

                result.Add(slot);
            }
        }

        return result;
    }

    //--------------------------------------------------
    // 공격 대상 선택
    //--------------------------------------------------

    protected virtual Character ChooseTarget(
        BattleContext context)
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
        int actionIndex,
        List<ActionSlot> plannedSlots)
    {
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

        List<ActionSlot> candidates = new();

        foreach (Skill skill in part.AvailableSkills)
        {
            if (skill == null)
                continue;

            if (!IsSkillUsable(
                    context,
                    part,
                    skill,
                    plannedSlots))
            {
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

                    Speed =
                        context.battleManager.SpeedManager.GetSpeed(part),

                    Phase = skill.DefaultPhase,

                    TargetSlot = null,

                    ActionIndex = actionIndex
                };

                candidates.Add(slot);
            }
        }

        if (candidates.Count == 0)
            return null;

        return SelectHighestScoreSlot(
            context,
            candidates,
            plannedSlots);
    }

    //--------------------------------------------------
    // 점수가 가장 높은 슬롯 선택
    //--------------------------------------------------

    protected ActionSlot SelectHighestScoreSlot(
        BattleContext context,
        List<ActionSlot> candidates,
        List<ActionSlot> plannedSlots)
    {
        if (candidates == null ||
            candidates.Count == 0)
        {
            return null;
        }

        ActionSlot bestSlot = null;
        float bestScore = float.MinValue;

        foreach (ActionSlot slot in candidates)
        {
            float score =
                ScoreSlot(
                    context,
                    slot,
                    plannedSlots);

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
        ActionSlot slot,
        List<ActionSlot> plannedSlots)
    {
        if (slot == null)
            return float.MinValue;

        if (slot.Skill == null)
            return float.MinValue;

        if (slot.TargetCharacter == null ||
            slot.TargetPart == null)
            return float.MinValue;

        if (slot.TargetCharacter.IsDead)
            return float.MinValue;

        if (slot.TargetPart.IsBroken)
            return float.MinValue;

        float score = 0f;

        //--------------------------------
        // 완전 고정 AI 방지용 랜덤성
        //--------------------------------

        score += Random.Range(0f, 25f);

        //--------------------------------
        // 스킬 타입 가치
        //--------------------------------

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
                score += 40f;
                break;
        }

        //--------------------------------
        // 합 가능 스킬 선호
        //--------------------------------

        if (slot.Skill.CanClash)
            score += 30f;

        //--------------------------------
        // Phase 기반 가중치
        //--------------------------------

        if (slot.Phase == ActionPhase.COMBAT)
            score += 15f;

        if (slot.Phase == ActionPhase.FORESIGHT)
            score += 10f;

        if (slot.Phase == ActionPhase.PRETURN)
            score += 5f;

        //--------------------------------
        // 타겟 부위 자체 가치
        //--------------------------------

        score +=
            ScoreTargetPart(
                slot.TargetPart);

        //--------------------------------
        // 여러 적이 같은 플레이어 부위를 몰빵하지 않도록 감점
        //--------------------------------

        score +=
            ScoreTargetSpread(
                context,
                slot,
                plannedSlots);

        //--------------------------------
        // 속도는 약하게만 반영
        //--------------------------------

        score += slot.Speed * 1.5f;

        return score;
    }

    //--------------------------------------------------
    // 타겟 부위 점수
    //--------------------------------------------------

    protected virtual float ScoreTargetPart(
        BodyPart targetPart)
    {
        if (targetPart == null)
            return -10000f;

        if (targetPart.IsBroken)
            return -10000f;

        float score = 0f;

        // 살아있는 부위를 기본적으로 선호
        score += 30f;

        // 약화된 부위는 약간 선호
        if (targetPart.IsWeakened)
            score += 20f;

        // HP 낮은 부위 마무리 선호는 약하게만
        if (targetPart.MaxPartHP > 0)
        {
            float hpRate =
                targetPart.PartHP / targetPart.MaxPartHP;

            float missingHpRate =
                1f - hpRate;

            score += missingHpRate * 15f;
        }

        // 합 가능한 부위를 조금 더 선호
        if (HasClashSkill(targetPart))
            score += 25f;

        return score;
    }

    //--------------------------------------------------
    // 타겟 분산 점수
    //--------------------------------------------------

    protected virtual float ScoreTargetSpread(
        BattleContext context,
        ActionSlot candidate,
        List<ActionSlot> plannedSlots)
    {
        if (candidate == null ||
            candidate.TargetCharacter == null ||
            candidate.TargetPart == null)
        {
            return 0f;
        }

        int sameTargetPartCount = 0;

        //--------------------------------
        // 이미 ActionManager에 들어간 슬롯 검사
        //--------------------------------

        if (context != null &&
            context.battleManager != null &&
            context.battleManager.ActionManager != null)
        {
            foreach (ActionSlot existingSlot in context.battleManager.ActionManager.Slots)
            {
                if (existingSlot == null)
                    continue;

                // 플레이어 슬롯은 AI 타겟 분산 계산에서 제외
                if (existingSlot.Owner == context.Player)
                    continue;

                if (existingSlot.TargetCharacter != candidate.TargetCharacter)
                    continue;

                if (!IsSamePart(
                        existingSlot.TargetPart,
                        candidate.TargetPart))
                {
                    continue;
                }

                sameTargetPartCount++;
            }
        }

        //--------------------------------
        // 이번 DecideSlots에서 이미 계획한 슬롯 검사
        //--------------------------------

        if (plannedSlots != null)
        {
            foreach (ActionSlot plannedSlot in plannedSlots)
            {
                if (plannedSlot == null)
                    continue;

                if (plannedSlot.TargetCharacter != candidate.TargetCharacter)
                    continue;

                if (!IsSamePart(
                        plannedSlot.TargetPart,
                        candidate.TargetPart))
                {
                    continue;
                }

                sameTargetPartCount++;
            }
        }

        if (sameTargetPartCount == 0)
            return 80f;

        return -120f * sameTargetPartCount;
    }

    //--------------------------------------------------
    // 타겟 부위 선택 가능 여부
    //--------------------------------------------------

    protected virtual bool IsTargetPartSelectable(
        BodyPart targetPart)
    {
        if (targetPart == null)
            return false;

        if (targetPart.IsBroken)
            return false;

        return true;
    }

    //--------------------------------------------------
    // 스킬 사용 가능 여부
    //--------------------------------------------------

    protected virtual bool IsSkillUsable(
        BattleContext context,
        BodyPart part,
        Skill skill,
        List<ActionSlot> plannedSlots)
    {
        if (part == null)
            return false;

        if (skill == null)
            return false;

        if (part.IsBroken)
            return false;

        //--------------------------------
        // AI 사용 가능 여부
        //--------------------------------

        if (!skill.CanAIUse(this, part, context))
            return false;

        //--------------------------------
        // 기본 Enemy는 도사림 사용 금지
        // 특수 Enemy는 AllowPreparationSkillAI override
        //--------------------------------

        if (!AllowPreparationSkillAI &&
            skill.ActionType == ActionType.Preparation)
        {
            return false;
        }

        //--------------------------------
        // 자원 조건
        // 기본 Prestige는 currentPrestige 확인
        // 김삿갓 같은 특수 Prestige는 Skill override
        //--------------------------------

        if (!skill.CanUseByResource(this))
            return false;

        //--------------------------------
        // Prestige 사용 횟수 정책
        //--------------------------------

        if (skill.ActionType == ActionType.Prestige)
        {
            if (skill.PrestigeUsePolicy == PrestigeUsePolicy.None)
                return false;

            if (skill.PrestigeUsePolicy == PrestigeUsePolicy.OncePerTurn)
            {
                if (HasPrestigeSlotSelected(
                        context,
                        plannedSlots))
                {
                    return false;
                }
            }
        }

        //--------------------------------
        // 기본 Enemy는 COMBAT 비합 스킬 사용 제한
        //--------------------------------

        if (RequireClashForCombatSkill &&
            skill.DefaultPhase == ActionPhase.COMBAT &&
            skill.ActionType != ActionType.Prestige &&
            !skill.CanClash)
        {
            return false;
        }

        //--------------------------------
        // 캐릭터 / 상태이상 / 메커닉 사용 가능 여부
        //--------------------------------

        if (!CanUseSkill(part, skill))
            return false;

        return true;
    }

    //--------------------------------------------------
    // 위세 슬롯 이미 선택했는지 확인
    //--------------------------------------------------

    protected virtual bool HasPrestigeSlotSelected(
        BattleContext context,
        List<ActionSlot> plannedSlots)
    {
        //--------------------------------
        // 이번 AI 결정 중 이미 고른 슬롯
        //--------------------------------

        if (plannedSlots != null)
        {
            foreach (ActionSlot slot in plannedSlots)
            {
                if (slot == null)
                    continue;

                if (slot.Owner != this)
                    continue;

                if (slot.Skill == null)
                    continue;

                if (slot.Skill.ActionType == ActionType.Prestige)
                    return true;
            }
        }

        //--------------------------------
        // 이미 ActionManager에 들어간 슬롯
        //--------------------------------

        if (context != null &&
            context.battleManager != null &&
            context.battleManager.ActionManager != null)
        {
            foreach (ActionSlot slot in context.battleManager.ActionManager.Slots)
            {
                if (slot == null)
                    continue;

                if (slot.Owner != this)
                    continue;

                if (slot.Skill == null)
                    continue;

                if (slot.Skill.ActionType == ActionType.Prestige)
                    return true;
            }
        }

        return false;
    }

    //--------------------------------------------------
    // 합 가능 스킬 보유 여부
    //--------------------------------------------------

    protected bool HasClashSkill(
        BodyPart part)
    {
        if (part == null)
            return false;

        if (part.AvailableSkills == null)
            return false;

        foreach (Skill skill in part.AvailableSkills)
        {
            if (skill == null)
                continue;

            if (skill.CanClash)
                return true;
        }

        return false;
    }

    //--------------------------------------------------
    // 같은 부위인지 확인
    //--------------------------------------------------

    protected bool IsSamePart(
        BodyPart a,
        BodyPart b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        return a.Type == b.Type;
    }
}