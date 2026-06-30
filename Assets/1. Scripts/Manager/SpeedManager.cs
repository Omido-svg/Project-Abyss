using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SpeedManager
{
    private readonly BattleContext battleContext;

    // 이번 턴 BodyPart별 속도 저장소
    private readonly Dictionary<BodyPart, int> speedByPart = new();

    public SpeedManager(BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }

    //------------------------------------------------
    // 턴 시작 시 모든 캐릭터의 부위별 속도 굴림
    //------------------------------------------------

    public void RollAllSpeed()
    {
        speedByPart.Clear();

        foreach (Character character in battleContext.AllCharacters)
        {
            RollCharacterSpeed(character);
        }
    }

    //------------------------------------------------

    private void RollCharacterSpeed(Character character)
    {
        if (character == null)
            return;

        List<BodyPart> activeParts = new();

        //------------------------------------
        // 행동 가능한 부위 수집
        //------------------------------------

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

        //------------------------------------
        // 캐릭터의 minSpeed ~ maxSpeed 범위로 속도 굴림
        //------------------------------------

        List<int> rolledSpeeds = new();

        for (int i = 0; i < activeParts.Count; i++)
        {
            int speed = Random.Range(
                character.CurrentStatus.minSpeed,
                character.CurrentStatus.maxSpeed + 1);

            rolledSpeeds.Add(speed);
        }

        //------------------------------------
        // 높은 속도부터 정렬
        //------------------------------------

        rolledSpeeds.Sort((a, b) => b.CompareTo(a));

        //------------------------------------
        // 부위에 속도값 배정
        //------------------------------------

        for (int i = 0; i < activeParts.Count; i++)
        {
            BodyPart part = activeParts[i];

            speedByPart[part] = rolledSpeeds[i];
        }
    }

    //------------------------------------------------
    // 특정 부위의 이번 턴 속도 가져오기
    //------------------------------------------------

    public int GetSpeed(BodyPart part)
    {
        if (part == null)
            return 0;

        if (!speedByPart.TryGetValue(part, out int speed))
            return 0;

        foreach (StatusEffect effect in part.StatusEffects)
        {
            speed = effect.ModifySpeed(part, speed);
        }

        return Mathf.Max(0, speed);
    }

    //------------------------------------------------
    // ActionSlot에 속도 적용
    //------------------------------------------------

    public void ApplySpeed(ActionSlot slot)
    {
        if (slot == null)
            return;

        slot.Speed = GetSpeed(slot.Part);
    }

    //------------------------------------------------
    // 여러 슬롯에 속도 적용
    //------------------------------------------------

    public void ApplySpeedToSlots(IEnumerable<ActionSlot> slots)
    {
        if (slots == null)
            return;

        foreach (ActionSlot slot in slots)
        {
            ApplySpeed(slot);
        }
    }

    //------------------------------------------------
    // 디버그용
    //------------------------------------------------

    public void PrintSpeeds()
    {
        StringBuilder sb = new();

        sb.AppendLine("========== SPEED ROLL ==========");
        sb.AppendLine();

        if (battleContext == null)
        {
            sb.AppendLine("BattleContext : NULL");
            Debug.Log(sb.ToString());
            return;
        }

        PrintCharacterSpeeds(
            sb,
            battleContext.Player);

        if (battleContext.Enemies != null)
        {
            foreach (Enemy enemy in battleContext.Enemies)
            {
                PrintCharacterSpeeds(
                    sb,
                    enemy);
            }
        }

        sb.AppendLine("================================");

        Debug.Log(sb.ToString());
    }
    
    private void PrintCharacterSpeeds(
        StringBuilder sb,
        Character character)
    {
        if (character == null)
            return;

        sb.AppendLine($"[{GetCharacterName(character)}]");

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            int speed = GetSpeed(part);

            string state =
                part.IsBroken
                    ? "BROKEN"
                    : "ACTIVE";

            sb.AppendLine(
                $"  - Part : {part.Type,-10} | " +
                $"Speed : {speed,2} | " +
                $"State : {state}");
        }

        sb.AppendLine();
    }
    
    private string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        if (character.Data == null)
            return character.name;

        return character.Data.CharacterName;
    }
}