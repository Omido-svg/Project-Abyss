using System.Collections.Generic;
using UnityEngine;

public static class DamageDistributionUtility
{
    public static List<int> DistributeIncreasing(
        int totalDamage,
        int hitCount)
    {
        List<int> result =
            new List<int>(Mathf.Max(0, hitCount));

        if (totalDamage <= 0)
            return result;

        if (hitCount <= 1)
        {
            result.Add(totalDamage);
            return result;
        }

        int weightSum =
            hitCount * (hitCount + 1) / 2;

        int allocated = 0;

        for (int i = 1; i <= hitCount; i++)
        {
            int damage;

            if (i == hitCount)
            {
                damage =
                    totalDamage - allocated;
            }
            else
            {
                damage =
                    Mathf.FloorToInt(
                        totalDamage * (i / (float)weightSum));
            }

            result.Add(damage);
            allocated += damage;
        }

        return result;
    }

    public static List<int> DistributeByWeights(
        int totalDamage,
        IReadOnlyList<int> weights)
    {
        int weightCount =
            weights != null
                ? weights.Count
                : 0;

        List<int> result =
            new List<int>(weightCount);

        if (totalDamage <= 0)
            return result;

        if (weightCount == 0)
        {
            result.Add(totalDamage);
            return result;
        }

        int weightSum = 0;
        int lastPositiveWeightIndex = -1;

        for (int i = 0; i < weightCount; i++)
        {
            int weight = Mathf.Max(0, weights[i]);
            weightSum += weight;

            if (weight > 0)
                lastPositiveWeightIndex = i;
        }

        if (weightSum <= 0)
        {
            result.Add(totalDamage);
            return result;
        }

        int remainingDamage = totalDamage;

        for (int i = 0; i < weightCount; i++)
        {
            int splitDamage;

            if (i == lastPositiveWeightIndex)
            {
                splitDamage = remainingDamage;
            }
            else
            {
                int weight = Mathf.Max(0, weights[i]);

                splitDamage =
                    Mathf.FloorToInt(
                        totalDamage * (float)weight / weightSum);

                splitDamage =
                    Mathf.Clamp(
                        splitDamage,
                        0,
                        remainingDamage);
            }

            result.Add(splitDamage);
            remainingDamage -= splitDamage;
        }

        return result;
    }
}
