public abstract class RandomResolver
{
    // 최소값
    public abstract int MinValue { get; }

    // 최대값
    public abstract int MaxValue { get; }

    //--------------------------------

    // 실제 굴림
    public abstract int Roll();

    //--------------------------------

    // UI 표시용
    public virtual string GetRangeText()
    {
        return $"{MinValue} ~ {MaxValue}";
    }
}