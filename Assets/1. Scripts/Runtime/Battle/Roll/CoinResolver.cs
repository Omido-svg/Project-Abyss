using UnityEngine;

public class CoinResolver : SkillResolver
{
    private readonly int coinCount;
    private readonly float frontChance;
    private readonly int frontValue;
    private readonly int backValue;
    private readonly bool frontIsCritical;

    public override int MinValue =>
        coinCount * Mathf.Min(frontValue, backValue);

    public override int MaxValue =>
        coinCount * Mathf.Max(frontValue, backValue);

    public CoinResolver(int coinCount)
        : this(
            coinCount,
            0.5f,
            1,
            0,
            false)
    {
    }

    public CoinResolver(
        int coinCount,
        float frontChance,
        int frontValue,
        int backValue,
        bool frontIsCritical)
    {
        this.coinCount = Mathf.Max(0, coinCount);
        this.frontChance = Mathf.Clamp01(frontChance);
        this.frontValue = frontValue;
        this.backValue = backValue;
        this.frontIsCritical = frontIsCritical;
    }

    public override int Roll()
    {
        int sum = 0;

        for (int i = 0; i < coinCount; i++)
        {
            bool front =
                Random.value < frontChance;

            sum += front
                ? frontValue
                : backValue;
        }

        return sum;
    }

    public override RollResult RollResult(
        Skill skill)
    {
        int sum = 0;
        int frontCount = 0;

        RollResult result = CreateResult(
            skill,
            SkillResolverType.Coin,
            0,
            false);

        for (int i = 0; i < coinCount; i++)
        {
            bool isFront =
                Random.value < frontChance;

            int value = isFront
                ? frontValue
                : backValue;

            result.CoinFaces.Add(isFront);
            result.CoinValues.Add(value);
            sum += value;

            if (isFront)
                frontCount++;
        }

        result.RawValue = sum;
        result.ModifiedValue = sum;
        result.IsMax =
            coinCount > 0 &&
            sum >= MaxValue;
        result.IsCritical = frontIsCritical
            ? frontCount > 0
            : result.IsMax;
        result.RecalculateFinalPower();

        return result;
    }
}
