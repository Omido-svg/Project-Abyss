public class SlotResolver : SkillResolver
{
    public override int MinValue => 1;

    public override int MaxValue => 81;

    public override int Roll()
    {
        int a =
            UnityEngine.Random.Range(
                1,
                10);

        int b =
            UnityEngine.Random.Range(
                1,
                10);

        return a * b;
    }

    public override RollResult RollResult(Skill skill)
    {
        int a =
            UnityEngine.Random.Range(
                1,
                10);

        int b =
            UnityEngine.Random.Range(
                1,
                10);

        int value =
            a * b;

        int basePower =
            skill != null
                ? skill.BasePower
                : 0;

        return new RollResult
        {
            ResolverType = SkillResolverType.Slot,

            BasePower = basePower,

            RawValue = value,
            ModifiedValue = value,
            FinalPower = basePower + value,

            IsMax = value >= MaxValue,
            IsCritical = value >= MaxValue,

            SlotA = a,
            SlotB = b,
            SlotValue = value
        };
    }
}