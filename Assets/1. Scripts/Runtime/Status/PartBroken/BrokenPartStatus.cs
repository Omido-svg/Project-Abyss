public abstract class BrokenPartStatus : StatusEffect
{
    protected PartType PartType;
    protected string PartId;

    protected BrokenPartStatus(PartType part)
    {
        PartType = part;
        Duration = -1;
    }

    protected BrokenPartStatus(BodyPart part)
    {
        PartType = part?.Type ?? PartType.CUSTOM;
        PartId = part?.PartId;
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
        if (part == null)
            return false;

        if (!string.IsNullOrWhiteSpace(PartId))
            return string.Equals(part.PartId, PartId, System.StringComparison.Ordinal);

        return part.Type == PartType;
    }

    protected override string GetMergeKey()
    {
        // 좌/우 팔은 같은 BrokenArm 타입이므로 PartType까지 키에 포함한다.
        return !string.IsNullOrWhiteSpace(PartId)
            ? $"{GetType().FullName}:{PartId}"
            : $"{GetType().FullName}:{PartType}";
    }
}