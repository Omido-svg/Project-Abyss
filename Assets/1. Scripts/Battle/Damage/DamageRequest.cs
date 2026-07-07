public struct DamageRequest
{
    public DamageType Type;

    public BodyPart TargetPart;

    public int Damage;

    public bool CanBreakPart;

    public StatusEffect SourceEffect;

    public static DamageRequest SkillPart(
        BodyPart targetPart,
        int damage,
        bool canBreakPart)
    {
        return new DamageRequest
        {
            Type = DamageType.SkillPart,
            TargetPart = targetPart,
            Damage = damage,
            CanBreakPart = canBreakPart,
            SourceEffect = null
        };
    }

    public static DamageRequest StatusPart(
        BodyPart targetPart,
        int damage,
        StatusEffect sourceEffect)
    {
        return new DamageRequest
        {
            Type = DamageType.StatusPart,
            TargetPart = targetPart,
            Damage = damage,
            CanBreakPart = false,
            SourceEffect = sourceEffect
        };
    }

    public static DamageRequest Direct(int damage)
    {
        return new DamageRequest
        {
            Type = DamageType.Direct,
            TargetPart = null,
            Damage = damage,
            CanBreakPart = false,
            SourceEffect = null
        };
    }

    public static DamageRequest True(
        int damage,
        StatusEffect sourceEffect)
    {
        return new DamageRequest
        {
            Type = DamageType.True,
            TargetPart = null,
            Damage = damage,
            CanBreakPart = false,
            SourceEffect = sourceEffect
        };
    }
}