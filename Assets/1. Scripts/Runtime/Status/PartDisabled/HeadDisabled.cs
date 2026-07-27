using UnityEngine;

public sealed class HeadDisabled : PartDisabledStatus
{
    public HeadDisabled() : base("Head Weakened") { }

    public override int ModifyRoll(BattleAction action, int roll) =>
        IsOwnerAction(action) ? Mathf.Max(0, roll - 1) : roll;

    public override bool CanUseSkill(BodyPart part, Skill skill)
    {
        if (skill == null) return false;
        return skill.ActionType != ActionType.Prestige;
    }
}
