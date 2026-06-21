public class CoinResolver : RandomResolver
{
    private int coinCount;

    public CoinResolver(Character owner,
                        int coinCount)
        : base(owner)
    {
        this.coinCount = coinCount;
    }

    public override int Roll()
    {
        int sum = 0;

        for(int i = 0; i < coinCount; i++)
        {
            bool head = UnityEngine.Random.value > 0.5f;

            sum += head ? 2 : 0;
        }

        return sum;
    }

    public override string GetResultText()
    {
        return "코인";
    }
}