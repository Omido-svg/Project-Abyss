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
    public Character Source;
    public Character Target;
    public BodyPart TargetPart;

    public BattleAction SourceAction;
    public int SourceExchangeIndex = -1;
    public SkillEffectTiming SourceEffectTiming;
    public bool HasSourceEffectTiming;

    public string StatusKey;
    public StatusEffectVisualPhase Phase;

    public int Stack;
    public int Duration;

    public StatusEffectRemoveReason RemoveReason;
    public DamageContext DamageContext;
}