public enum SkillEffectResultState
{
    NotScheduled,
    ConditionFailed,
    Applied,
    Failed
}

public sealed class SkillEffectResult
{
    public SkillEffectDefinition Effect { get; }
    public SkillEffectTiming Timing { get; }
    public SkillEffectResultState State { get; }

    public Character Target { get; }
    public BodyPart TargetPart { get; }

    public string Message { get; }

    public bool WasApplied =>
        State == SkillEffectResultState.Applied;

    private SkillEffectResult(
        SkillEffectDefinition effect,
        SkillEffectTiming timing,
        SkillEffectResultState state,
        Character target,
        BodyPart targetPart,
        string message)
    {
        Effect = effect;
        Timing = timing;
        State = state;
        Target = target;
        TargetPart = targetPart;
        Message = message;
    }

    public static SkillEffectResult NotScheduled(
        SkillEffectDefinition effect,
        SkillEffectTiming timing) =>
        new(
            effect,
            timing,
            SkillEffectResultState.NotScheduled,
            null,
            null,
            null);

    public static SkillEffectResult ConditionFailed(
        SkillEffectDefinition effect,
        SkillEffectTiming timing,
        SkillEffectContext context,
        string message) =>
        new(
            effect,
            timing,
            SkillEffectResultState.ConditionFailed,
            context?.Target,
            context?.TargetPart,
            message);

    public static SkillEffectResult Applied(
        SkillEffectDefinition effect,
        SkillEffectTiming timing,
        SkillEffectContext context) =>
        new(
            effect,
            timing,
            SkillEffectResultState.Applied,
            context?.Target,
            context?.TargetPart,
            null);

    public static SkillEffectResult Failed(
        SkillEffectDefinition effect,
        SkillEffectTiming timing,
        SkillEffectContext context,
        string message) =>
        new(
            effect,
            timing,
            SkillEffectResultState.Failed,
            context?.Target,
            context?.TargetPart,
            message);
}
