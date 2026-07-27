using UnityEngine;

public sealed class LegsDisabled : PartDisabledStatus
{
    public LegsDisabled() : base("Legs Weakened") { }

    public override int ModifySpeed(BodyPart part, int speed) =>
        Mathf.Max(0, speed - 2);

    public override bool CanUseSkill(BodyPart part, Skill skill) => skill != null;

    public override int ModifyDamage(BattleAction action, int damage)
    {
        if (!IsOwnerAction(action) || action.ActionType != ActionType.Preparation)
            return damage;
        return Mathf.Max(0, damage - 1);
    }
}
