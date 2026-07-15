using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SpeedManager
{
    private readonly BattleContext battleContext;

    private readonly Dictionary<BodyPart, int>
        speedByPart = new();

    private readonly Dictionary<Character, int>
        speedByCharacter = new();

    public SpeedManager(
        BattleContext battleContext)
    {
        this.battleContext =
            battleContext;
    }

    public void RollAllSpeed()
    {
        speedByPart.Clear();
        speedByCharacter.Clear();

        if (battleContext?.AllCharacters == null)
            return;

        foreach (Character character
                 in battleContext.AllCharacters)
        {
            RollCharacterSpeed(character);
        }
    }

    private void RollCharacterSpeed(
        Character character)
    {
        if (character == null ||
            character.CurrentStatus == null)
        {
            return;
        }

        if (character.IsSingleHpTarget)
        {
            speedByCharacter[character] =
                RollSpeed(character);

            return;
        }

        if (character.BodyParts == null)
            return;

        List<BodyPart> activeParts = new();

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken)
            {
                speedByPart[part] = 0;
                continue;
            }

            activeParts.Add(part);
        }

        List<int> rolledSpeeds = new();

        for (int i = 0;
             i < activeParts.Count;
             i++)
        {
            rolledSpeeds.Add(
                RollSpeed(character));
        }

        rolledSpeeds.Sort(
            (first, second) =>
                second.CompareTo(first));

        for (int i = 0;
             i < activeParts.Count;
             i++)
        {
            speedByPart[activeParts[i]] =
                rolledSpeeds[i];
        }
    }

    private int RollSpeed(
        Character character)
    {
        if (character?.CurrentStatus == null)
            return 0;

        return Random.Range(
            character.CurrentStatus.minSpeed,
            character.CurrentStatus.maxSpeed + 1);
    }

    public int GetSpeed(
        BodyPart part)
    {
        if (part == null)
            return 0;

        if (!speedByPart.TryGetValue(
                part,
                out int speed))
        {
            return 0;
        }

        foreach (StatusEffect effect
                 in part.StatusEffects)
        {
            if (effect == null)
                continue;

            speed =
                effect.ModifySpeed(
                    part,
                    speed);
        }

        return Mathf.Max(0, speed);
    }

    public int GetSpeed(
        Character character)
    {
        if (character == null)
            return 0;

        if (!speedByCharacter.TryGetValue(
                character,
                out int speed))
        {
            return 0;
        }

        return Mathf.Max(0, speed);
    }

    public int GetSpeed(
        Character character,
        BodyPart part)
    {
        return part != null
            ? GetSpeed(part)
            : GetSpeed(character);
    }

    public void ApplySpeed(
        ActionSlot slot)
    {
        if (slot == null)
            return;

        slot.Speed =
            GetSpeed(
                slot.Owner,
                slot.Part);
    }

    public void ApplySpeedToSlots(
        IEnumerable<ActionSlot> slots)
    {
        if (slots == null)
            return;

        foreach (ActionSlot slot in slots)
            ApplySpeed(slot);
    }

    public void SetSpeedForDebug(
        Character character,
        BodyPart part,
        int speed)
    {
        if (character == null)
            return;

        int safeSpeed =
            Mathf.Max(
                0,
                speed);

        if (character.IsSingleHpTarget ||
            part == null)
        {
            speedByCharacter[character] =
                safeSpeed;

            return;
        }

        if (part.Owner != null &&
            part.Owner != character)
        {
            return;
        }

        speedByPart[part] =
            part.IsBroken
                ? 0
                : safeSpeed;
    }

    public void ClearSpeedForDebug(
        Character character)
    {
        if (character == null)
            return;

        speedByCharacter.Remove(
            character);

        if (character.BodyParts == null)
            return;

        foreach (BodyPart part
                 in character.BodyParts)
        {
            if (part != null)
                speedByPart.Remove(part);
        }
    }

    public void PrintSpeeds()
    {
        if (!BattleDebugLog.ShowSpeedRoll)
            return;

        StringBuilder builder = new();

        builder.AppendLine(
            "========== SPEED ROLL ==========");
        builder.AppendLine();

        if (battleContext == null)
        {
            builder.AppendLine(
                "BattleContext : NULL");

            Debug.Log(
                builder.ToString());

            return;
        }

        PrintCharacterSpeeds(
            builder,
            battleContext.Player);

        if (battleContext.Enemies != null)
        {
            foreach (Character enemy
                     in battleContext.Enemies)
            {
                PrintCharacterSpeeds(
                    builder,
                    enemy);
            }
        }

        builder.AppendLine(
            "================================");

        Debug.Log(
            builder.ToString());
    }

    private void PrintCharacterSpeeds(
        StringBuilder builder,
        Character character)
    {
        if (character == null)
            return;

        builder.AppendLine(
            $"[{GetCharacterName(character)}]");

        if (character.IsSingleHpTarget)
        {
            builder.AppendLine(
                $"  - Source : SINGLE_HP  | " +
                $"Speed : {GetSpeed(character),2}");

            builder.AppendLine();
            return;
        }

        if (character.BodyParts == null)
        {
            builder.AppendLine();
            return;
        }

        foreach (BodyPart part
                 in character.BodyParts)
        {
            if (part == null)
                continue;

            string state =
                part.State switch
                {
                    BodyPartState.Normal =>
                        "NORMAL",
                    BodyPartState.Weakened =>
                        "WEAKENED",
                    BodyPartState.Broken =>
                        "BROKEN",
                    _ =>
                        "UNKNOWN"
                };

            builder.AppendLine(
                $"  - Part : {part.Type,-10} | " +
                $"Speed : {GetSpeed(part),2} | " +
                $"State : {state}");
        }

        builder.AppendLine();
    }

    private string GetCharacterName(
        Character character)
    {
        if (character == null)
            return "NULL";

        return character.Data == null
            ? character.name
            : character.Data.CharacterName;
    }
}