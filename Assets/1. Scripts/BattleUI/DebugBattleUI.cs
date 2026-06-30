using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class DebugBattleUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private TMP_Text text;

    [Header("Options")]
    [SerializeField] private bool showSelectedCharacter = true;
    [SerializeField] private bool showAllActionSlots = true;
    [SerializeField] private bool showBodyPartSkills = true;
    [SerializeField] private bool showStatusEffects = true;

    private readonly StringBuilder sb = new();

    //--------------------------------------------------

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();
    }

    private void Update()
    {
        if (battleManager == null)
            return;

        if (text == null)
            return;

        BattleContext context = battleManager.BattleContext;

        if (context == null)
        {
            text.text = "BattleContext : NULL";
            return;
        }

        Refresh(context);
    }

    //--------------------------------------------------

    private void Refresh(BattleContext context)
    {
        sb.Clear();

        AppendBattleHeader(context);
        AppendCharactersSummary(context);

        if (showSelectedCharacter)
        {
            AppendSelectedCharacter();
        }

        if (showAllActionSlots)
        {
            AppendAllActionSlots();
        }

        text.text = sb.ToString();
    }

    //--------------------------------------------------
    // Battle Header
    //--------------------------------------------------

    private void AppendBattleHeader(BattleContext context)
    {
        sb.AppendLine("========== BATTLE DEBUG ==========");

        int turn = 0;

        if (battleManager.TurnManager != null)
            turn = battleManager.TurnManager.CurrentTurn;

        sb.AppendLine($"Turn : {turn}");

        if (battleManager.TurnManager != null)
        {
            sb.AppendLine(
                $"Running : {battleManager.TurnManager.IsBattleRunning}");
        }

        sb.AppendLine();
    }

    //--------------------------------------------------
    // Characters Summary
    //--------------------------------------------------

    private void AppendCharactersSummary(BattleContext context)
    {
        sb.AppendLine("========== CHARACTERS ==========");

        if (context.Player != null)
        {
            sb.AppendLine(
                $"Player : {GetCharacterName(context.Player)} " +
                $"HP {GetCurrentHP(context.Player)}/{GetMaxHP(context.Player)}");
        }
        else
        {
            sb.AppendLine("Player : NULL");
        }

        sb.Append("Enemies : ");

        if (context.Enemies == null ||
            context.Enemies.Count == 0)
        {
            sb.AppendLine("None");
        }
        else
        {
            sb.AppendLine();

            foreach (Enemy enemy in context.Enemies)
            {
                if (enemy == null)
                    continue;

                sb.AppendLine(
                    $"- {GetCharacterName(enemy)} " +
                    $"HP {GetCurrentHP(enemy)}/{GetMaxHP(enemy)} " +
                    $"{(enemy.IsDead ? "[DEAD]" : "")}");
            }
        }

        sb.AppendLine();
    }

    //--------------------------------------------------
    // Selected Character
    //--------------------------------------------------

    private void AppendSelectedCharacter()
    {
        sb.AppendLine("========== SELECTED CHARACTER ==========");

        Character selected = battleManager.SelectedCharacter;

        if (selected == null)
        {
            sb.AppendLine("None");
            sb.AppendLine();
            return;
        }

        AppendCharacterDetail(selected);

        sb.AppendLine();
    }

    //--------------------------------------------------
    // Character Detail
    //--------------------------------------------------

    private void AppendCharacterDetail(Character character)
    {
        if (character == null)
            return;

        CurrentStatus c = character.CurrentStatus;
        RuntimeStatus r = character.RuntimeStatus;

        sb.AppendLine($"Name : {GetCharacterName(character)}");
        sb.AppendLine($"Dead : {character.IsDead}");
        sb.AppendLine();

        //--------------------------------
        // 기본 상태
        //--------------------------------

        sb.AppendLine("[Status]");

        sb.AppendLine(
            $"HP       : {GetCurrentHP(character)}/{GetMaxHP(character)}");

        if (r != null && c != null)
        {
            sb.AppendLine(
                $"Prestige : {r.currentPrestige}/{c.maxPrestige}");

            sb.AppendLine(
                $"Speed    : {c.minSpeed} ~ {c.maxSpeed}");

            sb.AppendLine(
                $"DamageM  : {c.damageMultiplier:0.00}");
        }

        sb.AppendLine();

        //--------------------------------
        // 캐릭터 상태이상
        //--------------------------------

        if (showStatusEffects)
        {
            AppendCharacterStatusEffects(character);
        }

        //--------------------------------
        // 부위 정보
        //--------------------------------

        AppendBodyParts(character);
    }

    //--------------------------------------------------
    // Status Effects
    //--------------------------------------------------

    private void AppendCharacterStatusEffects(Character character)
    {
        sb.AppendLine("[Character Status Effects]");

        IReadOnlyList<StatusEffect> effects =
            character.StatusEffects;

        if (effects == null || effects.Count == 0)
        {
            sb.AppendLine("None");
            sb.AppendLine();
            return;
        }

        foreach (StatusEffect effect in effects)
        {
            AppendEffect(effect, "- ");
        }

        sb.AppendLine();
    }

    private void AppendPartStatusEffects(BodyPart part)
    {
        if (!showStatusEffects)
            return;

        IReadOnlyList<StatusEffect> effects =
            part.StatusEffects;

        if (effects == null || effects.Count == 0)
        {
            sb.AppendLine("    Effects : None");
            return;
        }

        sb.AppendLine("    Effects :");

        foreach (StatusEffect effect in effects)
        {
            AppendEffect(effect, "      - ");
        }
    }

    private void AppendEffect(
        StatusEffect effect,
        string prefix)
    {
        if (effect == null)
            return;

        string durationText =
            effect.Duration < 0
                ? "Permanent"
                : $"{effect.Duration}T";

        sb.AppendLine(
            $"{prefix}{effect.Name} " +
            $"Stack {effect.Stack} / {durationText}");
    }

    //--------------------------------------------------
    // Body Parts
    //--------------------------------------------------

    private void AppendBodyParts(Character character)
    {
        sb.AppendLine("[Body Parts]");

        if (character.BodyParts == null ||
            character.BodyParts.Count == 0)
        {
            sb.AppendLine("None");
            sb.AppendLine();
            return;
        }

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            AppendBodyPart(character, part);
            sb.AppendLine();
        }
    }

    private void AppendBodyPart(
        Character character,
        BodyPart part)
    {
        sb.AppendLine(
            $"- {part.Type} [{part.State}]");

        sb.AppendLine(
            $"    HP    : {part.PartHP:0}/{part.MaxPartHP:0}");

        sb.AppendLine(
            $"    Speed : {GetPartSpeed(part)}");

        sb.AppendLine(
            $"    Usable: {part.IsUsable}");

        //--------------------------------
        // 현재 선택된 ActionSlot
        //--------------------------------

        ActionSlot slot = GetSlot(character, part);

        if (slot == null)
        {
            sb.AppendLine("    Slot  : None");
        }
        else
        {
            AppendSlot(slot, "    ");
        }

        //--------------------------------
        // 사용 가능 스킬 목록
        //--------------------------------

        if (showBodyPartSkills)
        {
            AppendSkills(character, part);
        }

        //--------------------------------
        // 부위 상태이상
        //--------------------------------

        AppendPartStatusEffects(part);
    }

    //--------------------------------------------------
    // Skills
    //--------------------------------------------------

    private void AppendSkills(
        Character character,
        BodyPart part)
    {
        IReadOnlyList<Skill> skills = part.AvailableSkills;

        if (skills == null || skills.Count == 0)
        {
            sb.AppendLine("    Skills : None");
            return;
        }

        sb.AppendLine("    Skills :");

        foreach (Skill skill in skills)
        {
            if (skill == null)
                continue;

            string usable =
                character.CanUseSkill(part, skill)
                    ? "OK"
                    : "BLOCKED";

            sb.AppendLine(
                $"      - {skill.SkillName} " +
                $"[{skill.ActionType}] " +
                $"PWR {skill.MinPower}~{skill.MaxPower} " +
                $"({usable})");
        }
    }

    //--------------------------------------------------
    // All Action Slots
    //--------------------------------------------------

    private void AppendAllActionSlots()
    {
        sb.AppendLine("========== ACTION SLOTS ==========");

        if (battleManager.ActionManager == null)
        {
            sb.AppendLine("ActionManager : NULL");
            sb.AppendLine();
            return;
        }

        IReadOnlyList<ActionSlot> slots =
            battleManager.ActionManager.Slots;

        if (slots == null || slots.Count == 0)
        {
            sb.AppendLine("None");
            sb.AppendLine();
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            ActionSlot slot = slots[i];

            sb.AppendLine($"[{i}]");
            AppendSlot(slot, "");
            sb.AppendLine();
        }
    }

    //--------------------------------------------------
    // Slot
    //--------------------------------------------------

    private void AppendSlot(
        ActionSlot slot,
        string indent)
    {
        if (slot == null)
        {
            sb.AppendLine($"{indent}Slot : NULL");
            return;
        }

        string ownerName =
            GetCharacterName(slot.Owner);

        string partName =
            slot.Part == null
                ? "NULL"
                : slot.Part.Type.ToString();

        string skillName =
            slot.Skill == null
                ? "NULL"
                : slot.Skill.SkillName;

        string targetName =
            GetCharacterName(slot.TargetCharacter);

        string targetPartName =
            slot.TargetPart == null
                ? "NULL"
                : slot.TargetPart.Type.ToString();

        sb.AppendLine(
            $"{indent}Owner      : {ownerName}");

        sb.AppendLine(
            $"{indent}Part       : {partName}");

        sb.AppendLine(
            $"{indent}Skill      : {skillName}");

        sb.AppendLine(
            $"{indent}Speed      : {slot.Speed}");

        sb.AppendLine(
            $"{indent}Phase      : {slot.Phase}");

        sb.AppendLine(
            $"{indent}Target     : {targetName}");

        sb.AppendLine(
            $"{indent}TargetPart : {targetPartName}");

        if (slot.TargetSlot == null)
        {
            sb.AppendLine(
                $"{indent}TargetSlot : None");
        }
        else
        {
            string targetSlotOwner =
                GetCharacterName(slot.TargetSlot.Owner);

            string targetSlotPart =
                slot.TargetSlot.Part == null
                    ? "NULL"
                    : slot.TargetSlot.Part.Type.ToString();

            sb.AppendLine(
                $"{indent}TargetSlot : " +
                $"{targetSlotOwner} / {targetSlotPart}");
        }
    }

    //--------------------------------------------------
    // Utility
    //--------------------------------------------------

    private ActionSlot GetSlot(
        Character character,
        BodyPart part)
    {
        if (battleManager == null)
            return null;

        if (battleManager.ActionManager == null)
            return null;

        return battleManager.ActionManager.FindSlot(
            character,
            part);
    }

    private int GetPartSpeed(BodyPart part)
    {
        if (battleManager == null)
            return 0;

        if (battleManager.SpeedManager == null)
            return 0;

        return battleManager.SpeedManager.GetSpeed(part);
    }

    private string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        if (character.Data == null)
            return character.name;

        return character.Data.CharacterName;
    }

    private int GetCurrentHP(Character character)
    {
        if (character == null)
            return 0;

        if (character.RuntimeStatus == null)
            return 0;

        return character.RuntimeStatus.currentHP;
    }

    private int GetMaxHP(Character character)
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
}