using UnityEngine;

public sealed class ArmDisabled : PartDisabledStatus
{
    private readonly PartType armType;

    public ArmDisabled(PartType armType)
        : base(armType == PartType.LEFT_HAND ? "Left Arm Weakened" : "Right Arm Weakened")
    {
        this.armType = armType;
    }

    public override int ModifyRoll(BattleAction action, int roll)
    {
        if (!IsOwnerAction(action)) return roll;
        return action.ActionType == ActionType.NormalAttack || action.ActionType == ActionType.Duel
            ? Mathf.Max(0, roll - 1)
            : roll;
    }

    public override bool CanUseSkill(BodyPart part, Skill skill) => skill != null;
}
