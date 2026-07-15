public abstract class SkillResolver
{
    public abstract int MinValue { get; }
    public abstract int MaxValue { get; }

    public virtual RollResult RollResult(Skill skill)
    {
        int raw = Roll();

        return CreateResult(
            skill,
            default(SkillResolverType),
            raw,
            raw >= MaxValue);
    }

    protected RollResult CreateResult(
        Skill skill,
        SkillResolverType resolverType,
        int rawValue,
        bool isMaximum)
    {
        RollResult result =
            new RollResult
            {
                ResolverType = resolverType,
                BasePower = skill != null
                    ? skill.BasePower
                    : 0,
                RawValue = rawValue,
                ModifiedValue = rawValue,
                ExternalModifier = 0,
                IsMax = isMaximum,
                IsCritical = isMaximum,
                SpeedModifier = 0,
                MomentumModifier = 0
            };

        result.RecalculateFinalPower();
        return result;
    }

    public abstract int Roll();
}
