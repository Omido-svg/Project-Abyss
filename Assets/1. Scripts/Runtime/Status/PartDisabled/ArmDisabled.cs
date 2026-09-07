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
        // 팔 약화는 해당 팔에서 발생한 공격/결투에만 적용한다.
        // 다른 부위 및 part == null인 글로벌 보스 슬롯까지 감소시키면 안 된다.
        if (!AffectsAction(action))
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
