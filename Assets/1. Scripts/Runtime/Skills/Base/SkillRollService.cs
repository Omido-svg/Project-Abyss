/// <summary>
/// Skill의 resolver 선택, debug 강제 굴림, exchange별 RollData 적용을 담당한다.
/// Skill의 공개 Roll API는 유지하고 실제 RNG 요청만 이 서비스에 위임한다.
/// </summary>
internal static class SkillRollService
{
    public static RollResult RollBase(Skill skill)
    {
        if (skill == null)
            return null;

        SkillResolverType fallbackType =
            skill.Definition?.ResolverType ??
            CharacterRandomDebugOverride
                .InferResolverType(
                    skill.Resolver);

        if (CharacterRandomDebugOverride.TryCreateRoll(
                skill.Owner,
                skill,
                null,
                fallbackType,
                0,
                out RollResult debugResult))
        {
            return debugResult;
        }

        if (skill.Resolver == null)
        {
            return new RollResult
            {
                BasePower = skill.BasePower,
                RawValue = 0,
                ModifiedValue = 0,
                FinalPower = skill.BasePower
            };
        }

        return skill.Resolver.RollResult(skill);
    }

    public static RollResult RollExchange(
        Skill skill,
        int exchangeIndex)
    {
        if (skill == null)
            return null;

        SkillRollData data =
            skill.GetRollData(exchangeIndex);

        if (data == null)
            return RollBase(skill);

        SkillResolverType fallbackType =
            skill.Definition?.ResolverType ??
            CharacterRandomDebugOverride
                .InferResolverType(
                    skill.Resolver);

        if (CharacterRandomDebugOverride.TryCreateRoll(
                skill.Owner,
                skill,
                data,
                fallbackType,
                exchangeIndex,
                out RollResult debugResult))
        {
            return debugResult;
        }

        return CombatRollResolver.Roll(
            skill,
            data,
            fallbackType);
    }
}
