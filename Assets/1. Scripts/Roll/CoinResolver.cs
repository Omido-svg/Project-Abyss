public class CoinResolver : SkillResolver
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

    public override RollResult RollResult(Skill skill)
    {
        int successCount =
            0;

        int basePower =
            skill != null
                ? skill.BasePower
                : 0;

        RollResult result =
            new RollResult
            {
                ResolverType = SkillResolverType.Coin,

                BasePower = basePower,

                RawValue = 0,
                ModifiedValue = 0,
                FinalPower = basePower,

                IsMax = false,
                IsCritical = false
            };

        for (int i = 0; i < coinCount; i++)
        {
            bool isFront =
                UnityEngine.Random.value >= 0.5f;

            result.CoinFaces.Add(isFront);

            if (isFront)
                successCount++;
        }

        result.RawValue =
            successCount;

        result.ModifiedValue =
            successCount;

        result.FinalPower =
            basePower + successCount;

        result.IsMax =
            successCount >= MaxValue;

        result.IsCritical =
            result.IsMax;

        return result;
    }
}