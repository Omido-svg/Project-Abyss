public abstract class BrokenPartStatus : StatusEffect
{
    protected PartType PartType;

    protected BrokenPartStatus(PartType part)
    {
        PartType = part;

        Duration = -1;
    }

    public PartType BrokenPart => PartType;
}