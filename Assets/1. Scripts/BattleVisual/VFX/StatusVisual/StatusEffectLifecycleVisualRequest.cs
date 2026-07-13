public enum StatusEffectVisualPhase
{
    Applied,
    Refreshed,
    Stacked,
    Removed,
    Expired
}

public class StatusEffectLifecycleVisualRequest
{
    public Character Target;
    public BodyPart TargetPart;

    public string StatusKey;
    public StatusEffectVisualPhase Phase;

    public int Stack;
    public int Duration;

    public StatusEffectRemoveReason RemoveReason;
}
