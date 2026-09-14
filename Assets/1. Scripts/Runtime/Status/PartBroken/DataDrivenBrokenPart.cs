public sealed class DataDrivenBrokenPart : BrokenPartStatus
{
    public DataDrivenBrokenPart(BodyPart part)
        : base(part)
    {
        Name = $"{part?.DisplayName ?? "Enemy Part"} Broken";
    }
}
