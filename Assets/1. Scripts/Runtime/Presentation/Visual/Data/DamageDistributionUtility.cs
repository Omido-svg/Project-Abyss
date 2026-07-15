using System.Collections.Generic;
using UnityEngine;

public static class DamageDistributionUtility
{
    public static List<int> DistributeIncreasing(
        int totalDamage,
        int hitCount)
    {
        if (hitCount <= 0)
            return new List<int>();

        List<int> weights = new List<int>(hitCount);

        for (int i = 1; i <= hitCount; i++)
            weights.Add(i);

        return DistributeByWeights(totalDamage, weights, hitCount);
    }

    public static List<int> DistributeByWeights(
        int totalDamage,
        IReadOnlyList<int> weights)
    {
        int count = weights?.Count ?? 0;

        if (count <= 0)
        {
            return totalDamage > 0
                ? new List<int> { totalDamage }
                : new List<int>();
        }

        return DistributeByWeights(totalDamage, weights, count);
    }

    public static List<int> DistributeByWeights(
        int totalDamage,
        IReadOnlyList<int> weights,
        int expectedHitCount)
    {
        int count = Mathf.Max(0, expectedHitCount);
        List<int> result = new List<int>(count);

        if (count == 0)
            return result;

        for (int i = 0; i < count; i++)
            result.Add(0);

        int safeDamage = Mathf.Max(0, totalDamage);

        if (safeDamage == 0)
            return result;

        long weightSum = 0;
        int lastPositiveIndex = -1;

        for (int i = 0; i < count; i++)
        {
            int weight = weights != null && i < weights.Count
                ? Mathf.Max(0, weights[i])
                : 1;

            weightSum += weight;

            if (weight > 0)
                lastPositiveIndex = i;
        }

        if (weightSum <= 0 || lastPositiveIndex < 0)
        {
            result[count - 1] = safeDamage;
            return result;
        }

        int allocated = 0;

        for (int i = 0; i < count; i++)
        {
            int weight = weights != null && i < weights.Count
                ? Mathf.Max(0, weights[i])
                : 1;

            int split;

            if (i == lastPositiveIndex)
            {
                split = safeDamage - allocated;
            }
            else if (weight <= 0)
            {
                split = 0;
            }
            else
            {
                split = Mathf.FloorToInt(
                    safeDamage * (weight / (float)weightSum));

                split = Mathf.Clamp(split, 0, safeDamage - allocated);
            }

            result[i] = split;
            allocated += split;
        }

        return result;
    }

    public static int Sum(IReadOnlyList<int> values)
    {
        if (values == null)
            return 0;

        int total = 0;

        for (int i = 0; i < values.Count; i++)
            total += Mathf.Max(0, values[i]);

        return total;
    }
}
