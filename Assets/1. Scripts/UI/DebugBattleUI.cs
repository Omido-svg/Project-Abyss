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

        // Refresh(context);
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
            $"HP        : {r.currentHP}/{c.maxHP}");

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

            if (part.CurrentSkill != null)
            {
                sb.AppendLine(
                    $"    Skill : {part.CurrentSkill.SkillName}");
            }

            sb.AppendLine();
        }

        //---------------- Actions ----------------

        sb.AppendLine("[Actions]");

        bool hasAction = false;

        foreach (BattleAction action in battleManager.ActionManager.Actions)
        {
            if (action.Owner != character)
                continue;

            hasAction = true;

            sb.AppendLine(
                $"{action.Owner.Data.CharacterName} ({action.OwnerPart.Type})"
                + $" -> {action.Target.Data.CharacterName} ({action.TargetPart.Type})"
                + $" / {action.Skill.SkillName}");
        }

        if (!hasAction)
            sb.AppendLine("None");

        sb.AppendLine();
    }
}