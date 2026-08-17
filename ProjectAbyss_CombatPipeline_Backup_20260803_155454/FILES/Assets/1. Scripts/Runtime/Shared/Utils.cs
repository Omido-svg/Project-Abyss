using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class Utils
{
    public static void Shuffle<T>(
        List<T> list)
    {
        if (list == null)
            return;

        for (int i = 0;
             i < list.Count;
             i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    list.Count);

            (list[i], list[randomIndex]) =
                (list[randomIndex], list[i]);
        }
    }

    public static void PrintList(
        List<Character> list)
    {
        PrintList(
            list,
            null);
    }

    public static void PrintList(
        List<Character> list,
        BattleManager battleManager)
    {
        if (list == null)
        {
            BattleDebugLog.Warning(
                BattleLogCategory.Combat,
                "Character List : NULL");
            return;
        }

        StringBuilder builder =
            new();

        builder.AppendLine(
            "========================================================");

        builder.AppendLine(
            $" Character List ({list.Count})");

        builder.AppendLine(
            "========================================================");

        foreach (Character character in list)
        {
            AppendCharacter(
                builder,
                character,
                battleManager);

            builder.AppendLine(
                "--------------------------------------------------------");
        }

        BattleDebugLog.Combat(
            builder.ToString(),
            BattleLogLevel.Trace);
    }

    private static void AppendCharacter(
        StringBuilder builder,
        Character character,
        BattleManager battleManager)
    {
        if (character == null)
        {
            builder.AppendLine(
                "NULL Character");
            return;
        }

        CurrentStatus current =
            character.CurrentStatus;

        RuntimeStatus runtime =
            character.RuntimeStatus;

        builder.AppendLine(
            $"[{GetCharacterName(character)}]");

        if (current != null &&
            runtime != null)
        {
            builder.AppendLine(
                $"  HP        : " +
                $"{runtime.currentHP}/" +
                $"{GetMaxHP(character)}");

            builder.AppendLine(
                $"  Prestige  : " +
                $"{runtime.currentPrestige}/" +
                $"{current.maxPrestige}");

            builder.AppendLine(
                $"  Speed     : " +
                $"{current.minSpeed} ~ " +
                $"{current.maxSpeed}");

            builder.AppendLine(
                $"  DamageM   : " +
                $"{current.damageMultiplier:0.00}");
        }
        else
        {
            builder.AppendLine(
                "  Status Not Initialized");
        }

        builder.AppendLine(
            $"  Dead      : {character.IsDead}");

        AppendCharacterStatusEffects(
            builder,
            character);

        AppendBodyParts(
            builder,
            character,
            battleManager);
    }

    private static void AppendCharacterStatusEffects(
        StringBuilder builder,
        Character character)
    {
        builder.AppendLine();
        builder.AppendLine(
            "  Character Status Effects");

        if (character.StatusEffects == null ||
            character.StatusEffects.Count == 0)
        {
            builder.AppendLine("    None");
            return;
        }

        foreach (StatusEffect effect
                 in character.StatusEffects)
        {
            AppendEffect(
                builder,
                effect,
                "    - ");
        }
    }

    private static void AppendBodyParts(
        StringBuilder builder,
        Character character,
        BattleManager battleManager)
    {
        builder.AppendLine();
        builder.AppendLine(
            "  Body Parts");

        if (character.BodyParts == null ||
            character.BodyParts.Count == 0)
        {
            builder.AppendLine("    None");
            return;
        }

        foreach (BodyPart part
                 in character.BodyParts)
        {
            if (part == null)
                continue;

            AppendBodyPart(
                builder,
                character,
                part,
                battleManager);
        }
    }

    private static void AppendBodyPart(
        StringBuilder builder,
        Character character,
        BodyPart part,
        BattleManager battleManager)
    {
        builder.AppendLine(
            $"    [{part.Type}] [{part.State}]");

        builder.AppendLine(
            $"      HP     : " +
            $"{part.PartHP:0}/" +
            $"{part.MaxPartHP:0}");

        builder.AppendLine(
            $"      Usable : {part.IsUsable}");

        builder.AppendLine(
            $"      Speed  : " +
            $"{GetPartSpeed(part, battleManager)}");

        ActionSlot slot =
            GetSlot(
                character,
                part,
                battleManager);

        if (slot == null)
        {
            builder.AppendLine(
                "      Slot   : None");
        }
        else
        {
            builder.AppendLine(
                "      Slot");

            builder.AppendLine(
                $"        Skill      : " +
                $"{GetSkillName(slot.Skill)}");

            builder.AppendLine(
                $"        Phase      : " +
                $"{slot.Phase}");

            builder.AppendLine(
                $"        Speed      : " +
                $"{slot.Speed}");

            builder.AppendLine(
                $"        Target     : " +
                $"{GetCharacterName(slot.TargetCharacter)}");

            builder.AppendLine(
                $"        TargetPart : " +
                $"{GetPartName(slot.TargetPart, slot.TargetCharacter)}");

            builder.AppendLine(
                $"        TargetSlot : " +
                $"{GetSlotName(slot.TargetSlot)}");
        }

        AppendPartSkills(
            builder,
            character,
            part);

        AppendPartStatusEffects(
            builder,
            part);
    }

    private static void AppendPartSkills(
        StringBuilder builder,
        Character character,
        BodyPart part)
    {
        builder.AppendLine(
            "      Skills");

        if (part.AvailableSkills == null ||
            part.AvailableSkills.Count == 0)
        {
            builder.AppendLine(
                "        None");
            return;
        }

        foreach (Skill skill
                 in part.AvailableSkills)
        {
            if (skill == null)
                continue;

            string usable =
                character.CanUseSkill(
                    part,
                    skill)
                    ? "OK"
                    : "BLOCKED";

            builder.AppendLine(
                $"        - {skill.SkillName} " +
                $"[{skill.ActionType}] " +
                $"PWR {skill.MinPower}~{skill.MaxPower} " +
                $"({usable})");
        }
    }

    private static void AppendPartStatusEffects(
        StringBuilder builder,
        BodyPart part)
    {
        builder.AppendLine(
            "      Effects");

        if (part.StatusEffects == null ||
            part.StatusEffects.Count == 0)
        {
            builder.AppendLine(
                "        None");
            return;
        }

        foreach (StatusEffect effect
                 in part.StatusEffects)
        {
            AppendEffect(
                builder,
                effect,
                "        - ");
        }
    }

    public static void PrintSlots(
        IReadOnlyList<ActionSlot> slots)
    {
        if (slots == null)
        {
            BattleDebugLog.Warning(
                BattleLogCategory.Combat,
                "ActionSlot List : NULL");
            return;
        }

        StringBuilder builder =
            new();

        builder.AppendLine(
            "========================================");

        builder.AppendLine(
            $" Action Slots ({slots.Count})");

        builder.AppendLine(
            "========================================");

        for (int i = 0;
             i < slots.Count;
             i++)
        {
            builder.AppendLine(
                $"[{i}]");

            AppendSlot(
                builder,
                slots[i],
                "  ");

            builder.AppendLine(
                "----------------------------------------");
        }

        BattleDebugLog.ActionSlot(
            builder.ToString());
    }

    private static void AppendSlot(
        StringBuilder builder,
        ActionSlot slot,
        string indent)
    {
        if (slot == null)
        {
            builder.AppendLine(
                $"{indent}NULL SLOT");
            return;
        }

        builder.AppendLine(
            $"{indent}ActionId   : " +
            $"{slot.ActionId}");

        builder.AppendLine(
            $"{indent}ActionIndex: " +
            $"{slot.ActionIndex}");

        builder.AppendLine(
            $"{indent}Owner      : " +
            $"{GetCharacterName(slot.Owner)}");

        builder.AppendLine(
            $"{indent}Part       : " +
            $"{GetPartName(slot.Part, slot.Owner)}");

        builder.AppendLine(
            $"{indent}Skill      : " +
            $"{GetSkillName(slot.Skill)}");

        builder.AppendLine(
            $"{indent}Speed      : " +
            $"{slot.Speed}");

        builder.AppendLine(
            $"{indent}Phase      : " +
            $"{slot.Phase}");

        builder.AppendLine(
            $"{indent}Target     : " +
            $"{GetCharacterName(slot.TargetCharacter)}");

        builder.AppendLine(
            $"{indent}TargetPart : " +
            $"{GetPartName(slot.TargetPart, slot.TargetCharacter)}");

        builder.AppendLine(
            $"{indent}TargetSlot : " +
            $"{GetSlotName(slot.TargetSlot)}");
    }

    public static void PrintActions(
        List<BattleAction> actions)
    {
        if (actions == null)
        {
            BattleDebugLog.Warning(
                BattleLogCategory.Combat,
                "BattleAction List : NULL");
            return;
        }

        StringBuilder builder =
            new();

        builder.AppendLine(
            "========================================");

        builder.AppendLine(
            $" Battle Actions ({actions.Count})");

        builder.AppendLine(
            "========================================");

        for (int i = 0;
             i < actions.Count;
             i++)
        {
            BattleAction action =
                actions[i];

            if (action == null)
            {
                builder.AppendLine(
                    $"[{i}] NULL");

                builder.AppendLine(
                    "----------------------------------------");

                continue;
            }

            AppendBattleAction(
                builder,
                action,
                i);

            builder.AppendLine(
                "----------------------------------------");
        }

        BattleDebugLog.Combat(
            builder.ToString(),
            BattleLogLevel.Trace);
    }

    private static void AppendBattleAction(
        StringBuilder builder,
        BattleAction action,
        int index)
    {
        builder.AppendLine(
            $"[{index}]");

        builder.AppendLine(
            $"  {GetCharacterName(action.Owner)} " +
            $"({GetPartName(action.OwnerPart, action.Owner)})");

        builder.AppendLine("      ↓");

        builder.AppendLine(
            $"  {GetCharacterName(action.Target)} " +
            $"({GetPartName(action.TargetPart, action.Target)})");

        builder.AppendLine();

        builder.AppendLine(
            $"  ActionId   : {action.ActionId}");

        builder.AppendLine(
            $"  ActionIndex: {action.ActionIndex}");

        builder.AppendLine(
            $"  Skill      : {GetSkillName(action.Skill)}");

        builder.AppendLine(
            $"  Type       : {action.ActionType}");

        builder.AppendLine(
            $"  Phase      : {action.Phase}");

        builder.AppendLine(
            $"  Speed      : {action.Speed}");

        builder.AppendLine(
            $"  PurePower  : " +
            $"{(action.HasRolled ? action.RolledPower.ToString() : "-")}");

        builder.AppendLine(
            $"  SpeedMod   : {action.SpeedModifier}");

        builder.AppendLine(
            $"  MomentumMod: {action.MomentumModifier}");

        builder.AppendLine(
            $"  ClashPower : {action.ClashPower}");

        builder.AppendLine(
            $"  Critical   : {action.Critical}");
    }

    private static void AppendEffect(
        StringBuilder builder,
        StatusEffect effect,
        string prefix)
    {
        if (effect == null)
        {
            builder.AppendLine(
                $"{prefix}NULL");
            return;
        }

        string durationText =
            effect.Duration < 0
                ? "Permanent"
                : $"{effect.Duration}T";

        builder.AppendLine(
            $"{prefix}{effect.Name} " +
            $"Stack {effect.Stack} / " +
            $"{durationText}");
    }

    private static ActionSlot GetSlot(
        Character character,
        BodyPart part,
        BattleManager battleManager)
    {
        if (battleManager?.ActionManager == null)
            return null;

        return battleManager.ActionManager
            .FindSlot(
                character,
                part);
    }

    private static int GetPartSpeed(
        BodyPart part,
        BattleManager battleManager)
    {
        if (part == null ||
            battleManager?.SpeedManager == null)
        {
            return 0;
        }

        return battleManager.SpeedManager
            .GetSpeed(part);
    }

    private static int GetMaxHP(
        Character character)
    {
        if (character?.BodyParts == null)
            return 0;

        int maxHP = 0;

        foreach (BodyPart part
                 in character.BodyParts)
        {
            if (part == null)
                continue;

            maxHP +=
                Mathf.RoundToInt(
                    part.MaxPartHP);
        }

        return maxHP;
    }

    private static string GetCharacterName(
        Character character)
    {
        if (character == null)
            return "NULL";

        return character.Data?.CharacterName ??
               character.name ??
               "NULL";
    }

    private static string GetPartName(
        BodyPart part,
        Character owner)
    {
        if (part != null)
            return part.Type.ToString();

        return owner == null
            ? "NONE"
            : owner.IsSingleHpTarget
                ? "SINGLE_HP"
                : "NONE";
    }

    private static string GetSkillName(
        Skill skill)
    {
        return skill?.SkillName ??
               "NULL";
    }

    private static string GetSlotName(
        ActionSlot slot)
    {
        if (slot == null)
            return "None";

        return
            $"{GetCharacterName(slot.Owner)} / " +
            $"{GetPartName(slot.Part, slot.Owner)} / " +
            $"Index={slot.ActionIndex}, " +
            $"Id={slot.ActionId}";
    }
}
