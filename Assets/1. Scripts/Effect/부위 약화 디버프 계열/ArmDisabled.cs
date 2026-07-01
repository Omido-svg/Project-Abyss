using UnityEngine;

public class ArmDisabled : PartDisabledStatus
{
    private readonly PartType armType;

    public ArmDisabled(PartType armType)
        : base(
            armType == PartType.LEFT_HAND
                ? "Left Arm Disabled"
                : "Right Arm Disabled")
    {
        this.armType = armType;
    }

    public override int ModifyRoll(
        BattleAction action,
        int roll)
    {
        if (!IsMyAction(action))
            return roll;

        if (action.OwnerPart.Type != armType)
            return roll;

        switch (action.ActionType)
        {
            case ActionType.NormalAttack:
            case ActionType.Duel:
                return Mathf.Max(0, roll - 1);

            default:
                return roll;
        }
    }

    public override bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (!IsMyPart(part))
            return true;

        if (skill == null)
            return false;

        // 팔 약화:
        // 팔은 공격 슬롯이므로 공격은 가능하지만 약해질 뿐.
        // 따라서 막지는 않는다.
        return true;
    }
}