using UnityEngine;

public class BrokenArm : BrokenPartStatus
{
    public BrokenArm(PartType armType)
        : base(armType)
    {
        if (armType != PartType.LEFT_HAND &&
            armType != PartType.RIGHT_HAND)
        {
            Debug.LogWarning(
                $"BrokenArm 생성 오류 : {armType}는 팔 부위가 아닙니다.");
        }

        Name = armType == PartType.LEFT_HAND
            ? "Broken Left Arm"
            : "Broken Right Arm";
    }

    public override int ModifyRoll(
        BattleAction action,
        int roll)
    {
        if (action == null)
            return roll;

        if (action.Skill == null)
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
}