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

                // 최대값과 크리티컬은 서로 다른 메타데이터다.
                // 크리티컬 여부는 CoinResolver처럼 명시적으로
                // 크리티컬 규칙을 가진 Resolver가 설정한다.
                IsCritical = false,
                SpeedModifier = 0,
                MomentumModifier = 0
            };

        result.RecalculateFinalPower();
        return result;
    }

    public abstract int Roll();
}