using UnityEngine;

public class HeadDisabled : PartDisabledStatus
{
    public HeadDisabled()
        : base("Head Disabled")
    {
    }

    public override int ModifyRoll(
        BattleAction action,
        int roll)
    {
        if (!IsMyAction(action))
            return roll;

        return Mathf.Max(0, roll - 1);
    }

    public override bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (!IsMyPart(part))
            return true;

        if (skill == null)
            return false;

        // 머리 약화:
        // 머리는 원래 공격 + 도사림 선택지가 많은 부위이므로
        // 약화되면 위세/특수 선택지를 제한하는 식으로 사용
        if (skill.ActionType == ActionType.Prestige)
            return false;

        return true;
    }
}