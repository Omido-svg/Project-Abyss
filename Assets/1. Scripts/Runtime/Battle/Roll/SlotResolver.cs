using UnityEngine;

public class SlotResolver : SkillResolver
{
    public override int MinValue => 1;
    public override int MaxValue => 81;

    public override int Roll()
    {
        int a = Random.Range(1, 10);
        int b = Random.Range(1, 10);

        return a * b;
    }

    public override RollResult RollResult(Skill skill)
    {
        int a = Random.Range(1, 10);
        int b = Random.Range(1, 10);
        int value = a * b;

        RollResult result =
            CreateResult(
                skill,
                SkillResolverType.Slot,
                value,
                value >= MaxValue);

        result.SlotA = a;
        result.SlotB = b;
        result.SlotValue = value;

        return result;
    }
}
