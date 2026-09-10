using System.Collections.Generic;
using UnityEngine;

public abstract class Enemy : Character
{
    public virtual List<ActionSlot> DecideSlots(
        BattleContext context)
    {
        List<ActionSlot> result = new();

        if (context?.Services?.SpeedManager == null)
        {
            return result;
        }

        Character target =
            ChooseTarget(context);

        if (target == null)
            return result;

        bool prestigeSelectedThisTurn = false;

        if (UsesBodyParts)
        {
            if (BodyParts == null)
                return result;

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

        ActionSlot singleHpSlot =
            SelectBestSlotForPart(
                context,
                null,
                target,
                allowPrestige: true,
                out _);

        if (singleHpSlot != null)
            result.Add(singleHpSlot);

        return result;
    }

    protected virtual Character ChooseTarget(
        BattleContext context)
    {
        return context?.Player;
    }
protected virtual ActionSlot SelectBestSlotForPart(
        BattleContext context,
        BodyPart part,
        Character target,
        bool allowPrestige,
        out bool selectedPrestige)
    {
        selectedPrestige = false;

        if (context?.Services?.SpeedManager == null ||
            target == null)
        {
            return null;
        }

        if (part != null &&
            !part.IsUsable)
        {
            return null;
        }

        IReadOnlyList<Skill> availableSkills =
            GetSelectableSkills(part, 0);

        if (availableSkills == null ||
            availableSkills.Count == 0)
        {
            return null;
        }

        List<ActionSlot> prestigeCandidates = new();
        List<ActionSlot> normalCandidates = new();

        foreach (Skill skill in availableSkills)
        {
            if (skill == null)
                continue;

            if (!IsSkillUsable(part, skill))
                continue;

            if (skill.ActionType == ActionType.Prestige)
            {
                if (!allowPrestige ||
                    !IsPrestigeReady())
                {
                    continue;
                }
            }

            TargetPoint targetPoint =
                BattleTargetValidator
                    .ChooseWeightedTargetPoint(
                        target,
                        TargetSelectionRule.StandardAttack,
                        brokenPartWeight: 0.7f);

            if (!targetPoint.IsValid)
                continue;

            ActionSlot slot =
                new ActionSlot
                {
                    Owner = this,
                    Part = part,
                    Skill = skill,
                    TargetCharacter = targetPoint.Character,
                    TargetPart = targetPoint.Part,
                    Speed = context.Services
                        .SpeedManager
                        .GetSpeed(this, part),
                    Phase = CalculateActionPhase(skill),
                    ActionIndex = 0
                };

            if (skill.ActionType ==
                ActionType.Prestige)
            {
                prestigeCandidates.Add(slot);
            }
            else
            {
                normalCandidates.Add(slot);
            }
        }

        if (allowPrestige &&
            IsPrestigeReady() &&
            prestigeCandidates.Count > 0)
        {
            selectedPrestige = true;

            return SelectHighestScoreSlot(
                context,
                prestigeCandidates);
        }

        return normalCandidates.Count > 0
            ? SelectHighestScoreSlot(
                context,
                normalCandidates)
            : null;
    }

    protected ActionSlot SelectHighestScoreSlot(
        BattleContext context,
        List<ActionSlot> candidates)
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
                    slot);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestSlot = slot;
        }

        return bestSlot;
    }

    protected virtual float ScoreSlot(
        BattleContext context,
        ActionSlot slot)
    {
        if (slot?.Skill == null ||
            slot.TargetCharacter == null ||
            slot.TargetCharacter.IsDead)
        {
            return float.MinValue;
        }

        if (!BattleTargetValidator.IsValid(
                slot.TargetCharacter,
                slot.TargetPart,
                TargetSelectionRule.StandardAttack))
        {
            return float.MinValue;
        }

        float score =
            Random.Range(0f, 25f);

        score += slot.Skill.ActionType switch
        {
            ActionType.Prestige => 10000f,
            ActionType.Duel => 80f,
            ActionType.NormalAttack => 60f,
            ActionType.Preparation => -10000f,
            _ => 0f
        };

        if (slot.Skill.CanClash)
            score += 30f;

        score += ScoreTargetPoint(
            slot.TargetCharacter,
            slot.TargetPart);

        score += ScoreTargetSpread(
            context,
            slot);

        score += slot.Speed * 1.5f;

        return score;
    }

    protected virtual float ScoreTargetPoint(
        Character target,
        BodyPart targetPart)
    {
        if (target == null)
            return -10000f;

        if (targetPart == null)
        {
            return target.IsSingleHpTarget
                ? 50f
                : -10000f;
        }

        float score = 30f;

        if (targetPart.IsBroken)
        {
            // 파괴 부위 재공격은 전체 HP 직접 피해다.
            score += 55f;
            return score;
        }

        if (targetPart.IsWeakened)
            score -= 80f;

        if (targetPart.MaxPartHP > 0f)
        {
            float hpRate =
                targetPart.PartHP /
                targetPart.MaxPartHP;

            score +=
                (1f - hpRate) * 15f;
        }

        if (HasClashSkill(targetPart))
            score += 25f;

        return score;
    }

    protected virtual float ScoreTargetSpread(
        BattleContext context,
        ActionSlot candidate)
    {
        ActionManager actionManager =
            context?.Services?.ActionManager;

        if (actionManager == null ||
            candidate?.TargetCharacter == null)
        {
            return 0f;
        }

        int sameTargetCount = 0;

        foreach (ActionSlot existingSlot
                 in actionManager.Slots)
        {
            if (existingSlot == null ||
                existingSlot.Owner == context.Player)
            {
                continue;
            }

            if (!BattleTargetValidator.IsSameTarget(
                    existingSlot.TargetCharacter,
                    existingSlot.TargetPart,
                    candidate.TargetCharacter,
                    candidate.TargetPart))
            {
                continue;
            }

            sameTargetCount++;
        }

        return sameTargetCount == 0
            ? 80f
            : -120f * sameTargetCount;
    }

    protected virtual bool IsTargetPartSelectable(
        Character target,
        BodyPart targetPart)
    {
        return BattleTargetValidator.IsValid(
            target,
            targetPart,
            TargetSelectionRule.StandardAttack);
    }

    protected virtual bool IsSkillUsable(
        BodyPart part,
        Skill skill)
    {
        if (skill == null)
            return false;

        if (part == null)
        {
            if (!IsSingleHpTarget)
                return false;
        }
        else if (part.IsBroken)
        {
            return false;
        }

        if (skill.ActionType ==
            ActionType.Preparation &&
            !AllowPreparationSkillAI)
        {
            return false;
        }

        if (skill.ActionType ==
            ActionType.Prestige &&
            !IsPrestigeReady())
        {
            return false;
        }

        if (skill.ActionType !=
            ActionType.Prestige &&
            skill.ActionType !=
            ActionType.Preparation &&
            !skill.CanClash)
        {
            return false;
        }

        return CanUseSkill(
            part,
            skill);
    }

    protected virtual bool AllowPreparationSkillAI =>
        false;

    protected bool IsPrestigeReady()
    {
        if (CurrentStatus == null ||
            RuntimeStatus == null ||
            CurrentStatus.maxPrestige <= 0)
        {
            return false;
        }

        return
            RuntimeStatus.currentPrestige >=
            CurrentStatus.maxPrestige;
    }

    protected ActionPhase CalculateActionPhase(
        Skill skill)
    {
        return skill?.DefaultPhase ??
               ActionPhase.COMBAT;
    }

    protected bool HasClashSkill(
        BodyPart part)
    {
        IReadOnlyList<Skill> skills =
            GetSelectableSkills(part, 0);

        if (skills == null)
            return false;

        foreach (Skill skill in skills)
        {
            if (skill?.CanClash == true)
                return true;
        }

        return false;
    }

    protected bool IsSamePart(
        BodyPart first,
        BodyPart second)
    {
        if (first == null ||
            second == null)
        {
            return
                first == null &&
                second == null;
        }

        return
            first == second ||
            first.Type == second.Type;
    }
}