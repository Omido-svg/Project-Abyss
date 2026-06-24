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

            if (c == null)
            {
                sb.AppendLine($"[{i}] NULL");
                sb.AppendLine("----------------------------------------");
                continue;
            }

            CurrentStatus current = c.CurrentStatus;
            RuntimeStatus runtime = c.RuntimeStatus;

            sb.AppendLine($"[{i}] {c.Data.CharacterName}");

            if (current != null && runtime != null)
            {
                sb.AppendLine($"    HP        : {runtime.currentHP}/{current.maxHP}");
                sb.AppendLine($"    Prestige  : {runtime.currentPrestige}/{current.maxPrestige}");
            }
            else
            {
                sb.AppendLine("    Status Not Initialized");
            }

            sb.AppendLine($"    Dead      : {c.IsDead}");

            sb.AppendLine("    BodyParts");

            foreach (BodyPart part in c.BodyParts)
            {
                sb.Append($"      - {part.Type}");

                if (part.IsBroken)
                {
                    sb.Append(" (Broken)");
                }
                else
                {
                    sb.Append($" HP : {part.PartHP}/{part.PartMaxHP}");
                }

                if (part.CurrentSkill != null)
                {
                    sb.Append($" | Skill : {part.CurrentSkill.SkillName}");
                }

                sb.AppendLine();
            }

            sb.AppendLine("----------------------------------------");
        }

        Debug.Log(sb.ToString());
    }
    
    public static void PrintActions(List<BattleAction> actions)
    {
        if (actions == null)
        {
            Debug.Log("BattleAction List : NULL");
            return;
        }

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("========================================");
        sb.AppendLine($" Battle Actions ({actions.Count})");
        sb.AppendLine("========================================");

        for (int i = 0; i < actions.Count; i++)
        {
            BattleAction action = actions[i];

            if (action == null)
            {
                sb.AppendLine($"[{i}] NULL");
                sb.AppendLine("----------------------------------------");
                continue;
            }

            string ownerName =
                action.Owner != null ? action.Owner.Data.CharacterName : "NULL";

            string targetName =
                action.Target != null ? action.Target.Data.CharacterName : "NULL";

            string ownerPart =
                action.OwnerPart != null ? action.OwnerPart.Type.ToString() : "None";

            string targetPart =
                action.TargetPart != null ? action.TargetPart.Type.ToString() : "None";

            string skill =
                action.Skill != null ? action.Skill.SkillName : "None";

            sb.AppendLine($"[{i}]");
            sb.AppendLine($"  {ownerName} ({ownerPart})");
            sb.AppendLine($"      ↓");
            sb.AppendLine($"  {targetName} ({targetPart})");
            sb.AppendLine($"  Skill : {skill}");
            sb.AppendLine($"  Speed : {action.Speed}");
            sb.AppendLine("----------------------------------------");
        }

        Debug.Log(sb.ToString());
    }
    
}