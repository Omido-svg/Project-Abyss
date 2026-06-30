using System.Collections.Generic;
using UnityEngine;

public class ClashBuilder
{
    public ActionExecutionQueue BuildQueue(IReadOnlyList<ActionSlot> slots)
    {
        ActionExecutionQueue queue = new();

        ClearTargetSlots(slots);

        List<ActionSlot> prestigeSlots =
            GetPhaseSlots(slots, ActionPhase.PRETURN);

        List<ActionSlot> ambushSlots =
            GetPhaseSlots(slots, ActionPhase.FORESIGHT);

        List<ActionSlot> combatSlots =
            GetPhaseSlots(slots, ActionPhase.COMBAT);

        foreach (ActionSlot slot in prestigeSlots)
            queue.PrestigeQueue.Enqueue(slot);

        foreach (ActionSlot slot in ambushSlots)
            queue.AmbushQueue.Enqueue(slot);

        queue.ClashQueue =
            BuildClashQueue(combatSlots);

        PrintSlots(slots);

        return queue;
    }
    
    private void ClearTargetSlots(IReadOnlyList<ActionSlot> slots)
    {
        if (slots == null)
            return;

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            slot.TargetSlot = null;
        }
    }
    
    private Queue<ClashPair> BuildClashQueue(
        List<ActionSlot> combatSlots)
    {
        Queue<ClashPair> result = new();

        combatSlots.Sort((a, b) => b.Speed.CompareTo(a.Speed));

        HashSet<ActionSlot> used = new();

        foreach (ActionSlot slot in combatSlots)
        {
            if (slot == null)
                continue;

            if (used.Contains(slot))
                continue;

            ActionSlot targetSlot =
                FindTargetActionSlot(slot, combatSlots);

            if (targetSlot != null &&
                !used.Contains(targetSlot) &&
                CanEnterClash(slot) &&
                CanEnterClash(targetSlot))
            {
                bool isMutual =
                    IsExactMutual(slot, targetSlot);

                bool canSteal =
                    CanStealClash(slot, targetSlot);

                if (isMutual || canSteal)
                {
                    slot.TargetSlot = targetSlot;
                    targetSlot.TargetSlot = slot;

                    result.Enqueue(
                        new ClashPair(slot, targetSlot));

                    used.Add(slot);
                    used.Add(targetSlot);

                    continue;
                }
            }

            result.Enqueue(
                new ClashPair(slot));

            used.Add(slot);
        }

        return result;
    }
    
    private ActionSlot FindTargetActionSlot(
        ActionSlot slot,
        List<ActionSlot> slots)
    {
        foreach (ActionSlot other in slots)
        {
            if (other == null)
                continue;

            if (other == slot)
                continue;

            if (other.Owner != slot.TargetCharacter)
                continue;

            if (!IsSamePart(other.Part, slot.TargetPart))
                continue;

            return other;
        }

        return null;
    }
    
    private bool CanEnterClash(ActionSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.Owner == null ||
            slot.Part == null ||
            slot.TargetCharacter == null ||
            slot.TargetPart == null)
            return false;

        if (slot.Phase != ActionPhase.COMBAT)
            return false;

        if (slot.Skill == null)
            return false;

        if (!slot.Skill.CanClash)
            return false;

        return true;
    }
    
    private bool IsExactMutual(
        ActionSlot a,
        ActionSlot b)
    {
        if (a == null || b == null)
            return false;

        return
            b.TargetCharacter == a.Owner &&
            IsSamePart(b.TargetPart, a.Part);
    }
    
    private bool CanStealClash(
        ActionSlot attacker,
        ActionSlot targetSlot)
    {
        if (attacker == null || targetSlot == null)
            return false;

        if (targetSlot.TargetCharacter != attacker.Owner)
            return false;

        if (attacker.Speed <= targetSlot.Speed)
            return false;

        return true;
    }
    
    private bool IsSamePart(
        BodyPart a,
        BodyPart b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        return a.Type == b.Type;
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

            if (IsConfirmedClash(slot))
            {
                log += " / Clash = YES";
            }

            Debug.Log(log);
        }
    }
    
    private bool IsConfirmedClash(ActionSlot slot)
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