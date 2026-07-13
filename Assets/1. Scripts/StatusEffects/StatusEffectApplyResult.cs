public enum StatusEffectApplyKind
{
    Applied,
    Refreshed,
    Stacked,
    Replaced,
    Ignored,
    Rejected
}

public sealed class StatusEffectApplyResult
{
    public Character TargetCharacter;
    public BodyPart TargetPart;

    public StatusEffect Effect;
    public StatusEffect IncomingEffect;

    public StatusEffectApplyKind Kind;

    public int StackBefore;
    public int StackAfter;

    public int DurationBefore;
    public int DurationAfter;

    public bool WasTransferred;

    public bool Succeeded =>
        Kind != StatusEffectApplyKind.Rejected;

    public bool IsPartStatus =>
        TargetPart != null;

    public bool IsNewApplication =>
        Kind == StatusEffectApplyKind.Applied ||
        Kind == StatusEffectApplyKind.Replaced;
}
