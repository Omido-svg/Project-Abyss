public class CoinResolver : RandomResolver
{
    private int coinCount;

    public override int MinValue => 0;

    public override int MaxValue => coinCount;

    public CoinResolver(int coinCount)
    {
        this.coinCount = coinCount;
    }

    public override int Roll()
    {
        int sum = 0;

        for (int i = 0; i < coinCount; i++)
        {
            if (UnityEngine.Random.value >= 0.5f)
                sum++;
        }

        return sum;
    }
}