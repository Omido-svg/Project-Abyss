using UnityEngine;

public class BrokenLegs : BrokenPartStatus
{
    public BrokenLegs()
        : base(PartType.LEGS)
    {
        Name = "Broken Legs";
    }

    public override int ModifySpeed(
        BodyPart part,
        int speed)
    {
        return Mathf.Max(0, speed - 2);
    }

    public override bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (skill == null)
            return false;

        if (skill.ActionType == ActionType.Preparation)
            return false;

        return true;
    }
}