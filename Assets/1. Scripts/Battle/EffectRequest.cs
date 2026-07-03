public class EffectRequest
{
    public Character SourceCharacter;
    public BodyPart SourcePart;
    public Skill SourceSkill;
    public StatusEffect SourceStatusEffect;
    public CombatMechanic SourceMechanic;

    public Character TargetCharacter;
    public BodyPart TargetPart;

    public int Value;

    public bool CanBreakPart;
    public DamageType DamageType;

    public StatusEffect StatusEffect;

    public static EffectRequest PartDamage(
        Character source,
        BattleAction action,
        int damage,
        bool canBreakPart)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            SourcePart = action?.OwnerPart,
            SourceSkill = action?.Skill,

            TargetCharacter = action?.Target,
            TargetPart = action?.TargetPart,

            Value = damage,
            CanBreakPart = canBreakPart,
            DamageType = DamageType.SkillPart
        };
    }

    public static EffectRequest BodyPartStatus(
        Character source,
        Character target,
        BodyPart targetPart,
        StatusEffect statusEffect)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            TargetCharacter = target,
            TargetPart = targetPart,
            StatusEffect = statusEffect
        };
    }

    public static EffectRequest CharacterStatus(
        Character source,
        Character target,
        StatusEffect statusEffect)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            TargetCharacter = target,
            StatusEffect = statusEffect
        };
    }

    public static EffectRequest ForceBreak(
        Character source,
        Character target,
        BodyPart targetPart)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            TargetCharacter = target,
            TargetPart = targetPart
        };
    }

    public static EffectRequest TrueDamage(
        Character source,
        Character target,
        int damage,
        StatusEffect sourceEffect)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            TargetCharacter = target,
            Value = damage,
            SourceStatusEffect = sourceEffect,
            DamageType = DamageType.True
        };
    }

    public static EffectRequest Prestige(
        Character source,
        Character target,
        int amount)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            TargetCharacter = target,
            Value = amount
        };
    }

    public static EffectRequest StatusPartDamage(
        Character source,
        Character target,
        BodyPart targetPart,
        int damage,
        StatusEffect sourceEffect)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            TargetCharacter = target,
            TargetPart = targetPart,
            Value = damage,
            SourceStatusEffect = sourceEffect,
            DamageType = DamageType.StatusPart
        };
    }

    public static EffectRequest RemoveBodyPartStatus(
        Character source,
        Character target,
        BodyPart targetPart,
        StatusEffect statusEffect)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            TargetCharacter = target,
            TargetPart = targetPart,
            StatusEffect = statusEffect
        };
    }

    public static EffectRequest RecoverPart(
        Character source,
        Character target,
        BodyPart targetPart)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            TargetCharacter = target,
            TargetPart = targetPart
        };
    }

    public static EffectRequest ForceKill(
        Character source,
        Character target)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            TargetCharacter = target
        };
    }
    
    public static EffectRequest PrestigeToMax(
        Character source,
        Character target)
    {
        return new EffectRequest
        {
            SourceCharacter = source,
            TargetCharacter = target
        };
    }
}