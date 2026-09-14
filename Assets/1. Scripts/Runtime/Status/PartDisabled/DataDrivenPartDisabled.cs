using UnityEngine;

public sealed class DataDrivenPartDisabled : PartDisabledStatus
{
    public DataDrivenPartDisabled(BodyPart part)
        : base($"{part?.DisplayName ?? "Enemy Part"} Weakened")
    {
    }

    protected override string GetMergeKey() =>
        $"{GetType().FullName}:{SourcePart?.PartId ?? "NONE"}";

    public override int ModifyExchangeRollCount(BattleAction action, int rollCount)
    {
        if (!AffectsAction(action) || SourcePart == null)
            return rollCount;
        return Mathf.Max(1, rollCount - SourcePart.WeakenedRollCountPenalty);
    }

    public override bool CanUseSkill(BodyPart part, Skill skill)
    {
        if (skill == null) return false;
        if (!AffectsPart(part) || SourcePart == null || !SourcePart.WeakenedNormalOnly)
            return true;
        return skill.ActionType == ActionType.NormalAttack;
    }
}
