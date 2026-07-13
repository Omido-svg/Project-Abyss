public abstract class BrokenPartStatus : StatusEffect
{
    protected PartType PartType;

    protected BrokenPartStatus(PartType part)
    {
        PartType = part;
        Duration = -1;
    }

    public PartType BrokenPart => PartType;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.Permanent;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.Ignore;

    public override bool TransferToCharacterOnPartBreak => false;

    public bool MatchesPart(BodyPart part)
    {
        return part != null &&
               part.Type == PartType;
    }

    protected override string GetMergeKey()
    {
        // 좌/우 팔은 같은 BrokenArm 타입이므로 PartType까지 키에 포함한다.
        return $"{GetType().FullName}:{PartType}";
    }
}
