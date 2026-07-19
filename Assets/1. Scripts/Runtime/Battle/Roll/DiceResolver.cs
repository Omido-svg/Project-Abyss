using UnityEngine;

public class DiceResolver : SkillResolver
{
    private readonly int min;
    private readonly int max;

    public override int MinValue => min;
    public override int MaxValue => max;

    public DiceResolver(int min, int max)
    {
        this.min = Mathf.Min(min, max);
        this.max = Mathf.Max(min, max);
    }

    public override int Roll()
    {
        return Random.Range(
            min,
            max + 1);
    }

    public override RollResult RollResult(Skill skill)
    {
        int value = Roll();

        RollResult result =
            CreateResult(
                skill,
                SkillResolverType.Dice,
                value,
                value >= MaxValue);

        result.DiceMin = min;
        result.DiceMax = max;
        result.DiceValues.Add(value);

        // 균등 주사위의 최대 눈은 단순 최대값이다.
        // 유진 코인처럼 명시적인 규칙이 없는 한 크리티컬이 아니다.
        result.IsCritical = false;

        return result;
    }
}