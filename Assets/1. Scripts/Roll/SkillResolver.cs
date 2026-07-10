public abstract class SkillResolver
{
    public abstract int MinValue { get; }
    public abstract int MaxValue { get; }

    public virtual RollResult RollResult(Skill skill)
    {
        int raw =
            Roll();

        int basePower =
            skill != null
                ? skill.BasePower
                : 0;

        return new RollResult
        {
            RawValue = raw,
            ModifiedValue = raw,
            BasePower = basePower,
            FinalPower = basePower + raw,
            IsMax = raw >= MaxValue,
            IsCritical = raw >= MaxValue
        };
    }

    public abstract int Roll();
}