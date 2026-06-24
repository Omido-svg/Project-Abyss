public class DiceResolver : RandomResolver
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
        return UnityEngine.Random.Range(min, max + 1);
    }
}