public class DiceResolver : RandomResolver
{
    private int diceCount;
    private int diceMax;

    public DiceResolver(Character owner,
                        int diceCount,
                        int diceMax)
        : base(owner)
    {
        this.diceCount = diceCount;
        this.diceMax = diceMax;
    }

    public override int Roll()
    {
        int sum = 0;

        for(int i = 0; i < diceCount; i++)
        {
            sum += UnityEngine.Random.Range(1, diceMax + 1);
        }

        return sum;
    }

    public override string GetResultText()
    {
        return "주사위";
    }
}