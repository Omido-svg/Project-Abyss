using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class Utils
{
    public static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
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

        sb.AppendLine("========================================================");
        sb.AppendLine($" Character List ({list.Count})");
        sb.AppendLine("========================================================");

        foreach (Character c in list)
        {
            if (c == null)
            {
                sb.AppendLine("NULL Character");
                sb.AppendLine("--------------------------------------------------------");
                continue;
            }

            CurrentStatus current = c.CurrentStatus;
            RuntimeStatus runtime = c.RuntimeStatus;

            sb.AppendLine($"[{c.Data.CharacterName}]");

            if (current != null && runtime != null)
            {
                sb.AppendLine($"  HP        : {runtime.currentHP}");
                sb.AppendLine($"  Block     : {runtime.currentDefensePenetration}");
                sb.AppendLine($"  Prestige  : {runtime.currentPrestige}/{current.maxPrestige}");
            }
            else
            {
                sb.AppendLine("  Status Not Initialized");
            }

            sb.AppendLine($"  Dead : {c.IsDead}");

            sb.AppendLine();
            sb.AppendLine("  Body Parts");

            foreach (BodyPart part in c.BodyParts)
            {
                sb.Append($"    [{part.Type}] ");

                if (part.IsBroken)
                {
                    sb.Append("BROKEN");
                }
                else
                {
                    sb.Append($"HP {part.PartHP}/{part.PartMaxHP}");
                }

                sb.Append($" | SPD {part.CurrentSpeed}");

                if (part.CurrentSkill != null)
                {
                    sb.Append($" | {part.CurrentSkill.SkillName}");
                    sb.Append($" ({part.CurrentSkill.ActionType})");
                }
                else
                {
                    sb.Append(" | No Skill");
                }

                sb.AppendLine();
            }

            sb.AppendLine("--------------------------------------------------------");
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

            sb.AppendLine($"  Skill     : {skill}");
            sb.AppendLine($"  Type      : {action.ActionType}");
            sb.AppendLine($"  Phase     : {action.Phase}");

            sb.AppendLine($"  Speed     : {action.Speed}");

            string rolledPower =
                action.RolledPower == 0 ? "-" : action.RolledPower.ToString();

            sb.AppendLine($"  RolledPow : {rolledPower}");

            sb.AppendLine("----------------------------------------");
        }

        Debug.Log(sb.ToString());
    }
    
}