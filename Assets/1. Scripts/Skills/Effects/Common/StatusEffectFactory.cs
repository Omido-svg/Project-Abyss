public enum StatusEffectId
{
    Bleeding,
    Burn,
    Stun
}

public static class StatusEffectFactory
{
    public static StatusEffect Create(
        StatusEffectId id,
        int stack)
    {
        return Create(
            id,
            stack,
            3);
    }

    public static StatusEffect Create(
        StatusEffectId id,
        int stack,
        int duration)
    {
        int safeStack =
            UnityEngine.Mathf.Max(1, stack);
        int safeDuration =
            UnityEngine.Mathf.Max(1, duration);

        return id switch
        {
            StatusEffectId.Bleeding =>
                new Bleeding(
                    safeStack,
                    safeDuration),
            StatusEffectId.Burn =>
                new Burn(
                    safeStack,
                    safeDuration),
            StatusEffectId.Stun =>
                new Stun(),
            _ => null
        };
    }
}
