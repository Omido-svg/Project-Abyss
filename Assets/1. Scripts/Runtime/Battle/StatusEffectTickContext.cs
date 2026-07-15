public enum StatusEffectTickTiming
{
    TurnStart,
    TurnEnd
}

public class StatusEffectTickContext
{
    public Character TargetCharacter { get; }
    public BodyPart TargetPart { get; }
    public StatusEffect SourceEffect { get; }
    public StatusEffectTickTiming Timing { get; }

    public int StackBefore { get; private set; }
    public int StackAfter { get; private set; }
    public int DurationBefore { get; private set; }
    public int DurationAfter { get; private set; }

    public int RequestedDamage { get; private set; }
    public int AppliedDamage { get; private set; }

    public DamageContext DamageContext { get; private set; }

    public bool DidApplyDamage =>
        DamageContext != null &&
        DamageContext.WasApplied;

    public bool ExpiredAfterTick =>
        SourceEffect != null &&
        SourceEffect.IsExpired;

    public BattleEffectResolver Resolver =>
        TargetCharacter?.BattleContext?.EffectResolver;

    public DamageManager DamageManager =>
        TargetCharacter?.BattleContext?
            .battleManager?.DamageManager;

    public bool IsPartStatus => TargetPart != null;
    public bool IsCharacterStatus => TargetPart == null;

    public StatusEffectTickContext(
        Character targetCharacter,
        BodyPart targetPart,
        StatusEffect sourceEffect,
        StatusEffectTickTiming timing =
            StatusEffectTickTiming.TurnEnd)
    {
        TargetCharacter = targetCharacter;
        TargetPart = targetPart;
        SourceEffect = sourceEffect;
        Timing = timing;
    }

    public DamageContext ApplyDamage(
        DamageRequest request)
    {
        RequestedDamage =
            request.RawPower > 0
                ? request.RawPower
                : request.Damage;

        if (DamageManager != null)
        {
            DamageContext =
                DamageManager.ApplyDamageContext(request);

            AppliedDamage =
                DamageContext?.GetDisplayDamage() ?? 0;

            Resolver?.ShowStatusTickVisual(
                SourceEffect,
                request.TargetCharacter,
                request.TargetPart,
                AppliedDamage);

            return DamageContext;
        }

        // BattleManager가 아직 준비되지 않은 예외적 상황의 폴백.
        EffectRequest effectRequest =
            request.Type == DamageType.StatusPart
                ? EffectRequest.StatusPartDamage(
                    request.SourceCharacter,
                    request.TargetCharacter,
                    request.TargetPart,
                    request.Damage,
                    request.SourceEffect)
                : EffectRequest.TrueDamage(
                    request.SourceCharacter,
                    request.TargetCharacter,
                    request.Damage,
                    request.SourceEffect);

        bool applied =
            request.Type == DamageType.StatusPart
                ? Resolver?.ApplyStatusPartDamage(effectRequest) == true
                : Resolver?.ApplyTrueDamage(effectRequest) == true;

        AppliedDamage = applied
            ? MathfMaxZero(request.Damage)
            : 0;

        return null;
    }

    internal void CaptureBefore(StatusEffect effect)
    {
        if (effect == null)
            return;

        StackBefore = effect.Stack;
        DurationBefore = effect.Duration;
    }

    internal void CaptureAfter(StatusEffect effect)
    {
        if (effect == null)
            return;

        StackAfter = effect.Stack;
        DurationAfter = effect.Duration;
    }

    private static int MathfMaxZero(int value)
    {
        return value < 0 ? 0 : value;
    }
}
