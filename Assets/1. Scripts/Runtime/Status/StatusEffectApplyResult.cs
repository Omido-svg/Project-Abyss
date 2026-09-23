public enum CanonicalStatusApplySemanticKind
{
    None = 0,
    NumericEntryApplied = 1,
    PresenceApplied = 2,
    PresenceRefreshed = 3,
    BespokeApplied = 4,
    BespokeStacked = 5,
    Other = 6
}

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

    /// <summary>
    /// 0922 canonical presentation/event semantic.
    /// StorageKind와 ApplyKind를 한 번만 결합해 Numeric 새 Entry,
    /// Presence refresh, Bespoke stack을 서로 다른 사건으로 노출한다.
    /// </summary>
    public CanonicalStatusApplySemanticKind CanonicalSemantic
    {
        get
        {
            if (!Succeeded ||
                Effect == null ||
                Kind == StatusEffectApplyKind.Ignored ||
                Kind == StatusEffectApplyKind.Rejected)
            {
                return CanonicalStatusApplySemanticKind.None;
            }

            return Effect.StorageKind switch
            {
                StatusEffectStorageKind.NumericTimed =>
                    IsNewApplication
                        ? CanonicalStatusApplySemanticKind.NumericEntryApplied
                        : CanonicalStatusApplySemanticKind.Other,

                StatusEffectStorageKind.PresenceTimed =>
                    Kind == StatusEffectApplyKind.Refreshed
                        ? CanonicalStatusApplySemanticKind.PresenceRefreshed
                        : IsNewApplication
                            ? CanonicalStatusApplySemanticKind.PresenceApplied
                            : CanonicalStatusApplySemanticKind.Other,

                StatusEffectStorageKind.Bespoke =>
                    Kind == StatusEffectApplyKind.Stacked
                        ? CanonicalStatusApplySemanticKind.BespokeStacked
                        : IsNewApplication
                            ? CanonicalStatusApplySemanticKind.BespokeApplied
                            : CanonicalStatusApplySemanticKind.Other,

                _ =>
                    CanonicalStatusApplySemanticKind.Other
            };
        }
    }
}