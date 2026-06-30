using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class Utils
{
    //--------------------------------------------------
    // Shuffle
    //--------------------------------------------------

    public static void Shuffle<T>(List<T> list)
    {
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);

            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }

    //--------------------------------------------------
    // Character List
    //--------------------------------------------------

    public static void PrintList(List<Character> list)
    {
        PrintList(list, null);
    }

    public static void PrintList(
        List<Character> list,
        BattleManager battleManager)
    {
        if (list == null)
        {
            Debug.Log("Character List : NULL");
            return;
        }

        StringBuilder sb = new();

        sb.AppendLine("========================================================");
        sb.AppendLine($" Character List ({list.Count})");
        sb.AppendLine("========================================================");

        foreach (Character character in list)
        {
            AppendCharacter(sb, character, battleManager);

            sb.AppendLine("--------------------------------------------------------");
        }

        Debug.Log(sb.ToString());
    }

    //--------------------------------------------------

    private static void AppendCharacter(
        StringBuilder sb,
        Character character,
        BattleManager battleManager)
    {
        if (character == null)
        {
            sb.AppendLine("NULL Character");
            return;
        }

        CurrentStatus current = character.CurrentStatus;
        RuntimeStatus runtime = character.RuntimeStatus;

        sb.AppendLine($"[{GetCharacterName(character)}]");

        if (current != null && runtime != null)
        {
            sb.AppendLine($"  HP        : {runtime.currentHP}/{GetMaxHP(character)}");
            sb.AppendLine($"  Prestige  : {runtime.currentPrestige}/{current.maxPrestige}");
            sb.AppendLine($"  Speed     : {current.minSpeed} ~ {current.maxSpeed}");
            sb.AppendLine($"  DamageM   : {current.damageMultiplier:0.00}");
        }
        else
        {
            sb.AppendLine("  Status Not Initialized");
        }

        sb.AppendLine($"  Dead      : {character.IsDead}");

        AppendCharacterStatusEffects(sb, character);
        AppendBodyParts(sb, character, battleManager);
    }

    //--------------------------------------------------
    // Character Status Effects
    //--------------------------------------------------

    private static void AppendCharacterStatusEffects(
        StringBuilder sb,
        Character character)
    {
        sb.AppendLine();
        sb.AppendLine("  Character Status Effects");

        if (character.StatusEffects == null ||
            character.StatusEffects.Count == 0)
        {
            sb.AppendLine("    None");
            return;
        }

        foreach (StatusEffect effect in character.StatusEffects)
        {
            AppendEffect(sb, effect, "    - ");
        }
    }

    //--------------------------------------------------
    // Body Parts
    //--------------------------------------------------

    private static void AppendBodyParts(
        StringBuilder sb,
        Character character,
        BattleManager battleManager)
    {
        sb.AppendLine();
        sb.AppendLine("  Body Parts");

        if (character.BodyParts == null ||
            character.BodyParts.Count == 0)
        {
            sb.AppendLine("    None");
            return;
        }

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            AppendBodyPart(sb, character, part, battleManager);
        }
    }

    private static void AppendBodyPart(
        StringBuilder sb,
        Character character,
        BodyPart part,
        BattleManager battleManager)
    {
        sb.AppendLine(
            $"    [{part.Type}] [{part.State}]");

        sb.AppendLine(
            $"      HP     : {part.PartHP:0}/{part.MaxPartHP:0}");

        sb.AppendLine(
            $"      Usable : {part.IsUsable}");

        sb.AppendLine(
            $"      Speed  : {GetPartSpeed(part, battleManager)}");

        //--------------------------------
        // 현재 ActionSlot
        //--------------------------------

        ActionSlot slot =
            GetSlot(character, part, battleManager);

        if (slot == null)
        {
            sb.AppendLine("      Slot   : None");
        }
        else
        {
            sb.AppendLine("      Slot");

            sb.AppendLine(
                $"        Skill      : {GetSkillName(slot.Skill)}");

            sb.AppendLine(
                $"        Phase      : {slot.Phase}");

            sb.AppendLine(
                $"        Speed      : {slot.Speed}");

            sb.AppendLine(
                $"        Target     : {GetCharacterName(slot.TargetCharacter)}");

            sb.AppendLine(
                $"        TargetPart : {GetPartName(slot.TargetPart)}");

            sb.AppendLine(
                $"        TargetSlot : {GetSlotName(slot.TargetSlot)}");
        }

        //--------------------------------
        // 스킬 목록
        //--------------------------------

        AppendPartSkills(sb, character, part);

        //--------------------------------
        // 부위 상태이상
        //--------------------------------

        AppendPartStatusEffects(sb, part);
    }

    //--------------------------------------------------
    // Part Skills
    //--------------------------------------------------

    private static void AppendPartSkills(
        StringBuilder sb,
        Character character,
        BodyPart part)
    {
        sb.AppendLine("      Skills");

        if (part.AvailableSkills == null ||
            part.AvailableSkills.Count == 0)
        {
            sb.AppendLine("        None");
            return;
        }

        foreach (Skill skill in part.AvailableSkills)
        {
            if (skill == null)
                continue;

            string usable =
                character.CanUseSkill(part, skill)
                    ? "OK"
                    : "BLOCKED";

            sb.AppendLine(
                $"        - {skill.SkillName} " +
                $"[{skill.ActionType}] " +
                $"PWR {skill.MinPower}~{skill.MaxPower} " +
                $"({usable})");
        }
    }

    //--------------------------------------------------
    // Part Status Effects
    //--------------------------------------------------

    private static void AppendPartStatusEffects(
        StringBuilder sb,
        BodyPart part)
    {
        sb.AppendLine("      Effects");

        if (part.StatusEffects == null ||
            part.StatusEffects.Count == 0)
        {
            sb.AppendLine("        None");
            return;
        }

        foreach (StatusEffect effect in part.StatusEffects)
        {
            AppendEffect(sb, effect, "        - ");
        }
    }

    //--------------------------------------------------
    // ActionSlot List
    //--------------------------------------------------

    public static void PrintSlots(
        IReadOnlyList<ActionSlot> slots)
    {
        if (slots == null)
        {
            Debug.Log("ActionSlot List : NULL");
            return;
        }

        StringBuilder sb = new();

        sb.AppendLine("========================================");
        sb.AppendLine($" Action Slots ({slots.Count})");
        sb.AppendLine("========================================");

        for (int i = 0; i < slots.Count; i++)
        {
            ActionSlot slot = slots[i];

            sb.AppendLine($"[{i}]");
            AppendSlot(sb, slot, "  ");
            sb.AppendLine("----------------------------------------");
        }

        Debug.Log(sb.ToString());
    }

    private static void AppendSlot(
        StringBuilder sb,
        ActionSlot slot,
        string indent)
    {
        if (slot == null)
        {
            sb.AppendLine($"{indent}NULL SLOT");
            return;
        }

        sb.AppendLine(
            $"{indent}Owner      : {GetCharacterName(slot.Owner)}");

        sb.AppendLine(
            $"{indent}Part       : {GetPartName(slot.Part)}");

        sb.AppendLine(
            $"{indent}Skill      : {GetSkillName(slot.Skill)}");

        sb.AppendLine(
            $"{indent}Speed      : {slot.Speed}");

        sb.AppendLine(
            $"{indent}Phase      : {slot.Phase}");

        sb.AppendLine(
            $"{indent}Target     : {GetCharacterName(slot.TargetCharacter)}");

        sb.AppendLine(
            $"{indent}TargetPart : {GetPartName(slot.TargetPart)}");

        sb.AppendLine(
            $"{indent}TargetSlot : {GetSlotName(slot.TargetSlot)}");
    }

    //--------------------------------------------------
    // BattleAction List
    //--------------------------------------------------

    public static void PrintActions(List<BattleAction> actions)
    {
        if (actions == null)
        {
            Debug.Log("BattleAction List : NULL");
            return;
        }

        StringBuilder sb = new();

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

            AppendBattleAction(sb, action, i);

            sb.AppendLine("----------------------------------------");
        }

        Debug.Log(sb.ToString());
    }

    private static void AppendBattleAction(
        StringBuilder sb,
        BattleAction action,
        int index)
    {
        sb.AppendLine($"[{index}]");

        sb.AppendLine(
            $"  {GetCharacterName(action.Owner)} ({GetPartName(action.OwnerPart)})");

        sb.AppendLine("      ↓");

        sb.AppendLine(
            $"  {GetCharacterName(action.Target)} ({GetPartName(action.TargetPart)})");

        sb.AppendLine();

        sb.AppendLine(
            $"  Skill      : {GetSkillName(action.Skill)}");

        sb.AppendLine(
            $"  Type       : {action.ActionType}");

        sb.AppendLine(
            $"  Phase      : {action.Phase}");

        sb.AppendLine(
            $"  Speed      : {action.Speed}");

        string rolledPower =
            action.HasRolled
                ? action.RolledPower.ToString()
                : "-";

        sb.AppendLine(
            $"  RolledPow  : {rolledPower}");

        sb.AppendLine(
            $"  FinalPower : {action.finalPower}");
    }

    //--------------------------------------------------
    // Effect
    //--------------------------------------------------

    private static void AppendEffect(
        StringBuilder sb,
        StatusEffect effect,
        string prefix)
    {
        if (effect == null)
        {
            sb.AppendLine($"{prefix}NULL");
            return;
        }

        string durationText =
            effect.Duration < 0
                ? "Permanent"
                : $"{effect.Duration}T";

        sb.AppendLine(
            $"{prefix}{effect.Name} " +
            $"Stack {effect.Stack} / {durationText}");
    }

    //--------------------------------------------------
    // Utility
    //--------------------------------------------------

    private static ActionSlot GetSlot(
        Character character,
        BodyPart part,
        BattleManager battleManager)
    {
        if (battleManager == null)
            return null;

        if (battleManager.ActionManager == null)
            return null;

        return battleManager.ActionManager.FindSlot(
            character,
            part);
    }

    private static int GetPartSpeed(
        BodyPart part,
        BattleManager battleManager)
    {
        if (part == null)
            return 0;

        if (battleManager == null)
            return 0;

        if (battleManager.SpeedManager == null)
            return 0;

        return battleManager.SpeedManager.GetSpeed(part);
    }

    private static int GetMaxHP(Character character)
    {
        if (character == null)
            return 0;

        int maxHP = 0;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            maxHP += Mathf.RoundToInt(part.MaxPartHP);
        }

        return maxHP;
    }

    private static string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        if (character.Data == null)
            return character.name;

        return character.Data.CharacterName;
    }

    private static string GetPartName(BodyPart part)
    {
        if (part == null)
            return "NULL";

        return part.Type.ToString();
    }

    private static string GetSkillName(Skill skill)
    {
        if (skill == null)
            return "NULL";

        return skill.SkillName;
    }

    private static string GetSlotName(ActionSlot slot)
    {
        if (slot == null)
            return "None";

        return
            $"{GetCharacterName(slot.Owner)} / " +
            $"{GetPartName(slot.Part)}";
    }
}