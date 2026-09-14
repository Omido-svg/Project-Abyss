using System;
using UnityEngine;

public sealed class DataDrivenBrokenPart : BrokenPartStatus
{
    private int appliedEnergyMaximumPenalty;

    public DataDrivenBrokenPart(BodyPart part)
        : base(part)
    {
        Name = $"{part?.DisplayName ?? "Enemy Part"} Broken";
    }

    public override void OnApply()
    {
        if (Owner == null || SourcePart == null)
            return;

        appliedEnergyMaximumPenalty = Mathf.Max(0, SourcePart.BrokenEnergyMaxPenalty);
        if (appliedEnergyMaximumPenalty > 0)
        {
            Owner.AdjustEnergyMaximum(
                -appliedEnergyMaximumPenalty,
                fillToMaximum: false);
        }
    }

    public override void OnRemove()
    {
        if (Owner != null && appliedEnergyMaximumPenalty > 0)
        {
            Owner.AdjustEnergyMaximum(
                appliedEnergyMaximumPenalty,
                fillToMaximum: false);
        }

        appliedEnergyMaximumPenalty = 0;
    }

    public override int ModifyExchangeRollCount(
        BattleAction action,
        int rollCount)
    {
        if (action?.Owner != Owner || SourcePart == null)
            return rollCount;

        return Mathf.Max(
            1,
            rollCount - SourcePart.BrokenRollCountPenalty);
    }

    public override bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (skill == null)
            return false;

        if (SourcePart == null)
            return true;

        if (SourcePart.BrokenNormalOnly &&
            skill.ActionType != ActionType.NormalAttack)
        {
            return false;
        }

        string skillId = skill.Definition?.SkillId;
        if (string.IsNullOrWhiteSpace(skillId) ||
            SourcePart.BrokenForbiddenSkillIds == null)
        {
            return true;
        }

        foreach (string forbiddenId in SourcePart.BrokenForbiddenSkillIds)
        {
            if (string.Equals(
                    forbiddenId,
                    skillId,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
