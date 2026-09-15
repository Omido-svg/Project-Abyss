using UnityEngine;

public class BrokenHead : BrokenPartStatus
{
    private bool maximumEnergyPenaltyApplied;

    public BrokenHead()
        : base(PartType.HEAD)
    {
        Name = "Broken Head";
    }

    public override void OnApply()
    {
        if (Owner == null ||
            Owner.BattleContext?.Player != Owner ||
            maximumEnergyPenaltyApplied)
        {
            return;
        }

        Owner.AdjustEnergyMaximum(-1, fillToMaximum: false);
        maximumEnergyPenaltyApplied = true;
    }

    public override void OnRemove()
    {
        if (!maximumEnergyPenaltyApplied || Owner == null)
            return;

        Owner.AdjustEnergyMaximum(+1, fillToMaximum: false);
        maximumEnergyPenaltyApplied = false;
    }

    public override int ModifyRoll(
        BattleAction action,
        int roll)
    {
        return Mathf.Max(0, roll - 1);
    }

    public override bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (skill == null)
            return false;

        if (skill.ActionType == ActionType.Prestige)
            return false;

        return true;
    }
}