using System.Text;
using TMPro;
using UnityEngine;

public class DebugBattleUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private TMP_Text text;

    private void Update()
    {
        if (battleManager == null || text == null)
            return;

        BattleContext context = battleManager.BattleContext;

        if (context == null)
            return;

        Refresh(context);
    }

    //--------------------------------------------------

    private void Refresh(BattleContext context)
    {
        StringBuilder sb = new();

        sb.AppendLine("========== Battle ==========");
        sb.AppendLine($"Turn : {battleManager.TurnManager.CurrentTurn}");
        sb.AppendLine();

        //---------------- Player ----------------

        if (context.Player != null)
        {
            sb.AppendLine($"Player : {context.Player.Data.CharacterName}");
        }

        //---------------- Enemy ----------------

        sb.Append("Enemies : ");

        if (context.Enemies.Count == 0)
        {
            sb.AppendLine("None");
        }
        else
        {
            for (int i = 0; i < context.Enemies.Count; i++)
            {
                sb.Append(context.Enemies[i].Data.CharacterName);

                if (i != context.Enemies.Count - 1)
                    sb.Append(", ");
            }

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("========== Selected Character ==========");

        if (battleManager.SelectedCharacter == null)
        {
            sb.AppendLine("None");
        }
        else
        {
            AppendCharacter(sb, battleManager.SelectedCharacter);
        }

        text.text = sb.ToString();
    }

    //--------------------------------------------------

    private void AppendCharacter(
        StringBuilder sb,
        Character character)
    {
        CurrentStatus c = character.CurrentStatus;
        RuntimeStatus r = character.RuntimeStatus;

        sb.AppendLine(character.Data.CharacterName);
        sb.AppendLine();

        //---------------- Status ----------------

        sb.AppendLine(
            $"HP        : {r.currentHP}/{character.CurrentHP}");

        sb.AppendLine(
            $"Prestige  : {r.currentPrestige}/{c.maxPrestige}");

        sb.AppendLine();

        //---------------- Status Effect ----------------

        sb.AppendLine("[Status Effects]");

        if (character.StatusEffects.Count == 0)
        {
            sb.AppendLine("None");
        }
        else
        {
            foreach (StatusEffect effect in character.StatusEffects)
            {
                sb.AppendLine(
                    $"- {effect.Name} (Stack : {effect.Stack})");
            }
        }

        sb.AppendLine();

        //---------------- Body Parts ----------------

        sb.AppendLine("[Body Parts]");

        foreach (BodyPart part in character.BodyParts)
        {
            sb.Append($"- {part.Type}");

            if (part.IsBroken)
                sb.Append(" (Broken)");
            else
                sb.Append($" HP : {part.PartHP}/{part.PartMaxHP}");

            sb.AppendLine();

            //----------------------------------
            // 현재 선택된 ActionSlot 출력
            //----------------------------------
            ActionSlot slot = null;

            foreach (var s in battleManager.ActionManager.Slots)
            {
                if (s.Owner == character &&
                    s.Part == part)
                {
                    slot = s;
                    break;
                }
            }

            if (slot != null)
            {
                sb.AppendLine($"    Skill : {slot.Skill.SkillName}");
                sb.AppendLine($"    Speed : {slot.Speed}");
                sb.AppendLine($"    Phase : {slot.Phase}");

                if (slot.TargetCharacter != null)
                {
                    sb.AppendLine(
                        $"    Target : {slot.TargetCharacter.Data.CharacterName}");

                    sb.AppendLine(
                        $"    TargetPart : {slot.TargetPart.Type}");
                }

                if (slot.TargetSlot != null)
                {
                    sb.AppendLine(
                        $"    Clash With : {slot.TargetSlot.Owner.Data.CharacterName}");
                }
                else
                {
                    sb.AppendLine("    Clash With : None");
                }
            }
            else
            {
                sb.AppendLine("    Skill : None");
            }

            sb.AppendLine();
        }

        //---------------- Action Slots ----------------

        sb.AppendLine("[Action Slots]");

        bool hasSlot = false;

        foreach (ActionSlot slot in battleManager.ActionManager.Slots)
        {
            if (slot.Owner != character)
                continue;

            hasSlot = true;

            string target =
                slot.TargetCharacter == null
                    ? "None"
                    : slot.TargetCharacter.Data.CharacterName;

            string targetPart =
                slot.TargetPart == null
                    ? "None"
                    : slot.TargetPart.Type.ToString();

            sb.AppendLine(
                $"{slot.Part.Type} -> {target} ({targetPart})");

            sb.AppendLine(
                $"    Skill : {slot.Skill.SkillName}");

            sb.AppendLine(
                $"    Speed : {slot.Speed}");

            sb.AppendLine(
                $"    Phase : {slot.Phase}");

            sb.AppendLine(
                $"    TargetSlot : {(slot.TargetSlot == null ? "None" : slot.TargetSlot.Owner.Data.CharacterName)}");

            sb.AppendLine();
        }

        if (!hasSlot)
        {
            sb.AppendLine("None");
        }
    }
}