using UnityEngine;

public class LegsDisabled : PartDisabledStatus
{
    public LegsDisabled()
        : base("Legs Disabled")
    {
    }

    public override int ModifySpeed(
        BodyPart part,
        int speed)
    {
        if (!IsMyPart(part))
            return speed;

        return Mathf.Max(0, speed - 2);
    }

    public override bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (!IsMyPart(part))
            return true;

        if (skill == null)
            return false;

        // 다리 약화:
        // 도사림은 사용 가능하지만 효과가 약해지는 방향.
        // 여기서는 사용 자체는 막지 않는다.
        return true;
    }

    public override int ModifyDamage(
        BattleAction action,
        int damage)
    {
        if (!IsMyAction(action))
            return damage;

        if (action.ActionType != ActionType.Preparation)
            return damage;

        // 도사림 하이브리드형 공격/효과 수치를 약화시키고 싶을 때 사용
        return Mathf.Max(0, damage - 1);
    }
}