using UnityEngine;

public class BrokenHead : BrokenPartStatus
{
    public BrokenHead()
        : base(PartType.HEAD)
    {
        Name = "Broken Head";
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