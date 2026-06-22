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
        sb.AppendLine($"Turn : {battleManager.turnManager.CurrentTurn}");
        sb.AppendLine();

        //---------------- Player ----------------

        if (context.Player != null)
        {
            sb.AppendLine($"Player : {context.Player.CharacterName}");
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
                sb.Append(context.Enemies[i].CharacterName);

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
        BaseStatus b = character.BaseStatus;
        RuntimeStatus r = character.RuntimeStatus;

        sb.AppendLine(character.CharacterName);
        sb.AppendLine();

        //---------------- Status ----------------

        sb.AppendLine(
            $"HP        : {r.currentHP}/{b.maxHP}");

        sb.AppendLine(
            $"ATK       : {b.attackLevel}");

        sb.AppendLine(
            $"DEF       : {b.defenseLevel}");

        sb.AppendLine(
            $"Mental    : {r.currentMentality}");

        sb.AppendLine(
            $"Stagger   : {r.currentStagger}/{b.maxStagger}");

        sb.AppendLine(
            $"Prestige  : {r.currentPrestige}/{b.maxPrestige}");

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
            sb.Append($"- {part.type}");

            if (part.IsBroken)
                sb.Append(" (Broken)");
            else
                sb.Append($" HP : {part.PartHP}");

            sb.AppendLine();

            if (part.SelectedSkill != null)
            {
                sb.AppendLine(
                    $"    Skill  : {part.SelectedSkill.SkillName}");

                if (part.SelectedTarget != null)
                {
                    sb.AppendLine(
                        $"    Target : {part.SelectedTarget.CharacterName}");
                }

                if (part.SelectedTargetPart != null)
                {
                    sb.AppendLine(
                        $"    Part   : {part.SelectedTargetPart.type}");
                }
            }

            sb.AppendLine();
        }
    }
}