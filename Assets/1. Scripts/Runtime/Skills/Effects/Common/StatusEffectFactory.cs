public enum StatusEffectId
{
    Bleeding = 0, // legacy serialized id: Olaf unique keyword 「혈상」
    Burn = 1,
    Stun = 2,

    Strength = 3,
    Weakness = 4,
    Sturdy = 5,
    Disarm = 6,
    Fracture = 7,
    Protection = 8,
    Rupture = 9,
    Heat = 10,
    Regeneration = 11,
    Pain = 12,
    OlafBloodWound = 13,
    Swift = 14
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
            3,
            0,
            RegenerationRecoveryChannel.HitPoints);
    }

    public static StatusEffect Create(
        StatusEffectId id,
        int stack,
        int duration)
    {
        return Create(
            id,
            stack,
            duration,
            0,
            RegenerationRecoveryChannel.HitPoints);
    }

    public static StatusEffect Create(
        StatusEffectId id,
        int stack,
        int duration,
        int regenerationHealAmount,
        RegenerationRecoveryChannel regenerationChannel)
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
            StatusEffectId.Strength =>
                new StrengthStatus(safeStack),
            StatusEffectId.Weakness =>
                new WeaknessStatus(safeStack),
            // 0915 C-27: (미정) 키워드는 신규 Runtime 효과를 만들지 않는다.
            StatusEffectId.Sturdy => null,
            StatusEffectId.Disarm => null,
            StatusEffectId.Fracture =>
                new FractureStatus(safeStack),
            StatusEffectId.Protection =>
                new ProtectionStatus(safeStack),
            StatusEffectId.Rupture =>
                new RuptureStatus(safeStack),
            StatusEffectId.Heat =>
                new HeatStatus(safeStack),
            StatusEffectId.Swift =>
                new SwiftStatus(safeStack),
            StatusEffectId.Regeneration =>
                new RegenerationStatus(
                    safeDuration,
                    regenerationHealAmount > 0 ? regenerationHealAmount : safeStack,
                    regenerationChannel),
            StatusEffectId.Pain =>
                new PainStatus(safeStack),
            StatusEffectId.OlafBloodWound =>
                new Bleeding(safeStack, -1),
            _ => null
        };
    }
}