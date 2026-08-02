using UnityEngine;

public sealed class ArmDisabled : PartDisabledStatus
{
    public ArmDisabled(PartType armType)
        : base(
            armType == PartType.LEFT_HAND
                ? "Left Arm Weakened"
                : "Right Arm Weakened")
    {
    }

    public override int ModifyExchangeRollCount(
        BattleAction action,
        int rollCount)
    {
        if (!IsOwnerAction(action))
            return rollCount;

        if (action.ActionType != ActionType.NormalAttack &&
            action.ActionType != ActionType.Duel)
        {
            return rollCount;
        }

        return Mathf.Max(1, rollCount - 1);
    }

    public override bool CanUseSkill(
        BodyPart part,
        Skill skill) =>
        skill != null;
}
