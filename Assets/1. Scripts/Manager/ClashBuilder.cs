using System.Collections.Generic;
using UnityEngine;

public class ClashBuilder
{
    public ActionExecutionQueue BuildQueue(IReadOnlyList<ActionSlot> slots)
    {
        ActionExecutionQueue queue = new();

        if (slots == null)
            return queue;

        //------------------------------------
        // Slot 연결
        //------------------------------------

        LinkTargetSlots(slots);

        //------------------------------------
        // PRETURN
        //------------------------------------

        List<ActionSlot> prestigeSlots =
            GetPhaseSlots(slots, ActionPhase.PRETURN);

        foreach (ActionSlot slot in prestigeSlots)
        {
            if (!IsValidSlot(slot))
                continue;

            queue.PrestigeQueue.Enqueue(slot);
        }

        //------------------------------------
        // FORESIGHT
        //------------------------------------

        List<ActionSlot> ambushSlots =
            GetPhaseSlots(slots, ActionPhase.FORESIGHT);

        foreach (ActionSlot slot in ambushSlots)
        {
            if (!IsValidSlot(slot))
                continue;

            queue.AmbushQueue.Enqueue(slot);
        }

        //------------------------------------
        // COMBAT
        //------------------------------------

        List<ActionSlot> combatSlots =
            GetPhaseSlots(slots, ActionPhase.COMBAT);

        HashSet<ActionSlot> visited = new();

        foreach (ActionSlot slot in combatSlots)
        {
            if (!IsValidSlot(slot))
                continue;

            if (visited.Contains(slot))
                continue;

            //------------------------------------
            // 상대 슬롯이 없음 = 일방 공격
            //------------------------------------

            if (slot.TargetSlot == null)
            {
                queue.ClashQueue.Enqueue(
                    new ClashPair(slot));

                visited.Add(slot);
                continue;
            }

            //------------------------------------
            // 서로 바라봄 = 합
            //------------------------------------

            if (slot.TargetSlot.TargetSlot == slot)
            {
                queue.ClashQueue.Enqueue(
                    new ClashPair(slot, slot.TargetSlot));

                visited.Add(slot);
                visited.Add(slot.TargetSlot);
                continue;
            }

            //------------------------------------
            // 나는 상대를 때리지만 상대는 다른 곳을 봄 = 일방 공격
            //------------------------------------

            queue.ClashQueue.Enqueue(
                new ClashPair(slot));

            visited.Add(slot);
        }

        PrintSlots(slots);

        return queue;
    }

    //------------------------------------
    // TargetSlot 연결
    //------------------------------------

    private void LinkTargetSlots(IReadOnlyList<ActionSlot> slots)
    {
        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            slot.TargetSlot = null;
        }

        foreach (ActionSlot slot in slots)
        {
            if (!IsValidSlot(slot))
                continue;

            // 합은 COMBAT끼리만 발생
            if (slot.Phase != ActionPhase.COMBAT)
                continue;

            foreach (ActionSlot other in slots)
            {
                if (!IsValidSlot(other))
                    continue;

                if (other == slot)
                    continue;

                // 상대도 COMBAT이 아니면 합 대상이 아님
                if (other.Phase != ActionPhase.COMBAT)
                    continue;

                if (other.Owner == slot.TargetCharacter &&
                    other.Part == slot.TargetPart)
                {
                    slot.TargetSlot = other;
                    break;
                }
            }
        }
    }

    //------------------------------------
    // Phase별 슬롯 추출 + 속도 정렬
    //------------------------------------

    private List<ActionSlot> GetPhaseSlots(
        IReadOnlyList<ActionSlot> slots,
        ActionPhase phase)
    {
        List<ActionSlot> result = new();

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.Phase == phase)
                result.Add(slot);
        }

        result.Sort((a, b) => b.Speed.CompareTo(a.Speed));

        return result;
    }

    //------------------------------------
    // Slot 검증
    //------------------------------------

    private bool IsValidSlot(ActionSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.Owner == null)
            return false;

        if (slot.Part == null)
            return false;

        if (slot.Skill == null)
            return false;

        if (slot.TargetCharacter == null)
            return false;

        if (slot.TargetPart == null)
            return false;

        if (slot.Owner.IsDead)
            return false;

        if (slot.TargetCharacter.IsDead)
            return false;

        if (slot.Part.IsBroken)
            return false;

        if (slot.TargetPart.IsBroken)
            return false;

        return true;
    }

    //------------------------------------
    // Debug
    //------------------------------------

    private void PrintSlots(IReadOnlyList<ActionSlot> slots)
    {
        Debug.Log("===== SLOT CONNECTION =====");

        if (slots == null)
        {
            Debug.Log("Slots : NULL");
            return;
        }

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
            {
                Debug.Log("NULL SLOT");
                continue;
            }

            string ownerName = GetCharacterName(slot.Owner);
            string ownerPart = GetPartName(slot.Part);

            string targetName = GetCharacterName(slot.TargetCharacter);
            string targetPart = GetPartName(slot.TargetPart);

            string skillName = GetSkillName(slot.Skill);

            string targetSlotText = GetTargetSlotText(slot.TargetSlot);

            string log =
                $"{ownerName} ({ownerPart}) -> " +
                $"{targetName} ({targetPart}) / " +
                $"Skill : {skillName} / " +
                $"Speed : {slot.Speed} / " +
                $"Phase : {slot.Phase} / " +
                $"TargetSlot = {targetSlotText}";

            if (IsMutualClash(slot))
            {
                log += " / Clash = YES";
            }

            Debug.Log(log);
        }
    }
    
    private bool IsMutualClash(ActionSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.TargetSlot == null)
            return false;

        ActionSlot targetSlot = slot.TargetSlot;

        // 서로를 향하고 있어야 함
        if (targetSlot.TargetSlot != slot)
            return false;

        // 둘 다 전투 페이즈여야 함
        if (slot.Phase != ActionPhase.COMBAT)
            return false;

        if (targetSlot.Phase != ActionPhase.COMBAT)
            return false;

        // 둘 다 합 가능한 스킬이어야 함
        if (slot.Skill == null || !slot.Skill.CanClash)
            return false;

        if (targetSlot.Skill == null || !targetSlot.Skill.CanClash)
            return false;

        return true;
    }
    
    private string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        if (character.Data == null)
            return character.name;

        return character.Data.CharacterName;
    }

    private string GetPartName(BodyPart part)
    {
        if (part == null)
            return "NULL";

        return part.Type.ToString();
    }

    private string GetSkillName(Skill skill)
    {
        if (skill == null)
            return "NULL";

        return skill.SkillName;
    }

    private string GetTargetSlotText(ActionSlot targetSlot)
    {
        if (targetSlot == null)
            return "NULL";

        return
            $"{GetCharacterName(targetSlot.Owner)} " +
            $"({GetPartName(targetSlot.Part)})";
    }
}