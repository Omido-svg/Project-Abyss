public class DiceResolver : SkillResolver
{
    private int min;
    private int max;

    public override int MinValue => min;

    public override int MaxValue => max;

    public DiceResolver(int min, int max)
    {
        this.min = min;
        this.max = max;
    }

    public override int Roll()
    {
        return UnityEngine.Random.Range(
            min,
            max + 1);
    }

    public override RollResult RollResult(Skill skill)
    {
        int value =
            Roll();

        int basePower =
            skill != null
                ? skill.BasePower
                : 0;

        RollResult result =
            new RollResult
            {
                ResolverType = SkillResolverType.Dice,

                BasePower = basePower,

                RawValue = value,
                ModifiedValue = value,
                FinalPower = basePower + value,

                IsMax = value >= MaxValue,
                IsCritical = value >= MaxValue,

                DiceMin = min,
                DiceMax = max
            };

        result.DiceValues.Add(value);

        return result;
    }
}