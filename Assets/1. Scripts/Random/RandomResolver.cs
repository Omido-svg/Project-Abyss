public abstract class RandomResolver
{
    public abstract int MinValue { get; }
    public abstract int MaxValue { get; }

    public virtual RollResult RollResult(Skill skill)
    {
        int raw = Roll();

        return new RollResult
        {
            RawValue = raw,
            ModifiedValue = raw,
            FinalPower = skill.BasePower + raw,
            IsMax = raw >= MaxValue,
            IsCritical = raw >= MaxValue
        };
    }

    public abstract int Roll();
}