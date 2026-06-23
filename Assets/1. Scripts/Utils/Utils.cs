using System.Collections.Generic;
using System.Text;
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
    
    public static void PrintList(List<Character> list)
    {
        if (list == null)
        {
            Debug.Log("Character List : NULL");
            return;
        }

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("========================================");
        sb.AppendLine($" Character List ({list.Count})");
        sb.AppendLine("========================================");

        for (int i = 0; i < list.Count; i++)
        {
            Character c = list[i];

            sb.AppendLine($"[{i}] {c.CharacterName}");
            sb.AppendLine($"    HP      : {c.RuntimeStatus.currentHP}/{c.BaseStatus.maxHP}");
            sb.AppendLine($"    Stagger : {c.RuntimeStatus.currentStagger}/{c.BaseStatus.maxStagger}");
            sb.AppendLine($"    Dead    : {c.IsDead}");
            sb.AppendLine("----------------------------------------");
        }

        Debug.Log(sb.ToString());
    }
}