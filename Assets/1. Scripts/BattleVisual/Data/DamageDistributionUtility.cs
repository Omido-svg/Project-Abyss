using System.Collections.Generic;
using UnityEngine;

public static class DamageDistributionUtility
{
    public static List<int> DistributeIncreasing(
        int totalDamage,
        int hitCount)
    {
        List<int> result =
            new List<int>();

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
}