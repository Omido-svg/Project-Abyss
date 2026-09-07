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

        bool hasCharacterLevelSlots =
            (character.CombatRulesRuntime?.GetSlotCountForPart(null) ?? 0) > 0;

        if (character.IsSingleHpTarget || hasCharacterLevelSlots)
        {
            speedByCharacter[character] =
                RollSpeed(character, part: null);

            if (character.IsSingleHpTarget)
                return;
        }

        if (character.BodyParts == null)
            return;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken)
            {
                speedByPart[part] = 0;
                continue;
            }

            // 속도 굴림 단위는 ActionSlot이 아니라 BodyPart다.
            // 한 부위에 슬롯이 2개, 3개로 늘어나도 여기서 딱 한 번만 굴리고
            // 모든 ActionIndex가 speedByPart[part] 값을 공유한다.
            speedByPart[part] =
                RollSpeed(
                    character,
                    part);
        }
    }

    private int RollSpeed(
        Character character,
        BodyPart part)
    {
        if (character?.CurrentStatus == null)
            return 0;

        int minSpeed =
            character.CurrentStatus.minSpeed;

        int maxSpeed =
            character.CurrentStatus.maxSpeed;

        // 과거 슬롯별 OverrideSpeedRange 데이터가 있더라도
        // 이제는 같은 부위 전체가 공유하는 범위로 해석한다.
        if (character.CombatRulesRuntime
                ?.TryGetSharedSpeedRange(
                    part,
                    out int sharedMin,
                    out int sharedMax) == true)
        {
            minSpeed = sharedMin;
            maxSpeed = sharedMax;
        }

        BodyPart legs =
            character.GetBodyPart(
                PartType.LEGS);

        if (legs?.IsWeakened == true)
        {
            maxSpeed = Mathf.Max(
                minSpeed,
                maxSpeed - 1);
        }

        return Random.Range(
            minSpeed,
            maxSpeed + 1);
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