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

    // 스킬 효과에서 유래한 상태라면 논리 Action/교환과 프레젠테이션을 연결한다.
    public BattleAction SourceAction;
    public int SourceExchangeIndex = -1;
    public SkillEffectTiming SourceEffectTiming;
    public bool HasSourceEffectTiming;

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