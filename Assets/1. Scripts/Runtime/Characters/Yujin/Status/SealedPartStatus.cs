public sealed class SealedPartStatus : StatusEffect
{
    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnEnd;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.RefreshDuration;

    public SealedPartStatus(int duration = 1)
    {
        Name = "Sealed";
        Stack = 1;
        Duration = duration < 1 ? 1 : duration;
    }

    public override bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        return !IsMyPart(part);
    }

    public override bool CanAct()
    {
        return false;
    }
}
