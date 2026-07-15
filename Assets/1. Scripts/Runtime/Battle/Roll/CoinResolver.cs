using UnityEngine;

public class CoinResolver : SkillResolver
{
    private readonly int coinCount;

    public override int MinValue => 0;
    public override int MaxValue => coinCount;

    public CoinResolver(int coinCount)
    {
        this.coinCount = Mathf.Max(0, coinCount);
    }

    public override int Roll()
    {
        int sum = 0;

        for (int i = 0; i < coinCount; i++)
        {
            if (Random.value >= 0.5f)
                sum++;
        }

        return sum;
    }

    public override RollResult RollResult(Skill skill)
    {
        int successCount = 0;

        RollResult result =
            CreateResult(
                skill,
                SkillResolverType.Coin,
                0,
                false);

        for (int i = 0; i < coinCount; i++)
        {
            bool isFront =
                Random.value >= 0.5f;

            result.CoinFaces.Add(isFront);

            if (isFront)
                successCount++;
        }

        result.RawValue = successCount;
        result.ModifiedValue = successCount;
        result.IsMax =
            coinCount > 0 &&
            successCount >= coinCount;
        result.IsCritical = result.IsMax;
        result.RecalculateFinalPower();

        return result;
    }
}
