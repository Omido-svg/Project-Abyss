using System.Collections.Generic;
using UnityEngine;

public static class Utils
{
    public static void Shuffle(List<Skill> skills)
    {
        for (int i = skills.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            (skills[i], skills[j]) = (skills[j], skills[i]);
        }
    }
}