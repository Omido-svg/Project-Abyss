public enum StatusEffectId
{
    Bleeding,
    Burn
}

public static class StatusEffectFactory
{
    public static StatusEffect Create(
        StatusEffectId id,
        int stack)
    {
        switch (id)
        {
            case StatusEffectId.Bleeding:
                return new Bleeding(stack);

            case StatusEffectId.Burn:
                return new Burn(stack);

            default:
                return null;
        }
    }
}