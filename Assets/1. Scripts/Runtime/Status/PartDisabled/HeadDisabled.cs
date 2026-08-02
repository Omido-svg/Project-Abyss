public sealed class HeadDisabled : PartDisabledStatus
{
    public HeadDisabled()
        : base("Head Weakened")
    {
    }

    public override bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        return skill != null &&
               skill.ActionType == ActionType.NormalAttack;
    }
}
