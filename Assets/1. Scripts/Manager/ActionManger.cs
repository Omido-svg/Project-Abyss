using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ClashPair
{
    public ActionSlot First;
    public ActionSlot Second;

    public bool IsClash => Second != null;

    public ClashPair(ActionSlot first, ActionSlot second = null)
    {
        First = first;
        Second = second;
    }
}

public class ActionExecutionQueue
{
    public Queue<ActionSlot> PrestigeQueue = new();

    public Queue<ActionSlot> AmbushQueue = new();

    public Queue<ClashPair> ClashQueue = new();
}

public class ActionManager
{
    private readonly List<ActionSlot> slots = new();

    public IReadOnlyList<ActionSlot> Slots => slots;

    public void Clear()
    {
        slots.Clear();
    }

    public void AddSlot(ActionSlot slot)
    {
        if (slot == null)
            return;

        slots.Add(slot);

        Debug.Log(
            "[ActionManager AddSlot]\n" +
            FormatSlot(slot));
    }

    public void AddOrReplaceSlot(ActionSlot slot)
    {
        if (slot == null)
            return;

        ActionSlot oldSlot = FindSlot(slot.Owner, slot.Part);

        if (oldSlot != null)
        {
            slots.Remove(oldSlot);

            Debug.Log(
                "[ActionManager ReplaceSlot - Old]\n" +
                FormatSlot(oldSlot));
        }

        slots.Add(slot);

        Debug.Log(
            "[ActionManager AddOrReplaceSlot - New]\n" +
            FormatSlot(slot));
    }

    public void RemoveSlot(ActionSlot slot)
    {
        if (slot == null)
            return;

        slots.Remove(slot);

        Debug.Log(
            "[ActionManager RemoveSlot]\n" +
            FormatSlot(slot));
    }
    
    public void RemoveSlotsByOwner(Character owner)
    {
        if (owner == null)
            return;

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            ActionSlot slot = slots[i];

            if (slot == null)
            {
                slots.RemoveAt(i);
                continue;
            }

            if (slot.Owner == owner)
                slots.RemoveAt(i);
        }

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.TargetSlot == null)
                continue;

            if (slot.TargetSlot.Owner == owner)
                slot.TargetSlot = null;
        }
    }

    public ActionSlot FindSlot(Character owner, BodyPart part)
    {
        foreach (ActionSlot slot in slots)
        {
            if (slot.Owner == owner &&
                slot.Part == part)
            {
                return slot;
            }
        }

        return null;
    }

    public int CountSlots(Character owner)
    {
        int count = 0;

        foreach (ActionSlot slot in slots)
        {
            if (slot.Owner == owner)
                count++;
        }

        return count;
    }
    
    public void RemoveSlot(Character owner, BodyPart part)
    {
        if (owner == null || part == null)
            return;

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            ActionSlot slot = slots[i];

            if (slot == null)
            {
                slots.RemoveAt(i);
                continue;
            }

            if (slot.Owner == owner &&
                slot.Part == part)
            {
                slots.RemoveAt(i);
            }
        }

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.TargetSlot == null)
                continue;

            if (slot.TargetSlot.Owner == owner &&
                slot.TargetSlot.Part == part)
            {
                slot.TargetSlot = null;
            }
        }
    }

    public List<ActionSlot> GetAllSlots()
    {
        List<ActionSlot> result = new(slots);

        result.Sort((a, b) => b.Speed.CompareTo(a.Speed));

        return result;
    }

    public void PrintSlots(string title = "ACTION MANAGER SLOTS")
    {
        StringBuilder sb = new();

        sb.AppendLine($"========== {title} ==========");
        sb.AppendLine($"Count : {slots.Count}");
        sb.AppendLine();

        for (int i = 0; i < slots.Count; i++)
        {
            sb.AppendLine($"[{i}]");
            sb.AppendLine(FormatSlot(slots[i]));
            sb.AppendLine("--------------------------------");
        }

        sb.AppendLine("================================");

        Debug.Log(sb.ToString());
    }

    private string FormatSlot(ActionSlot slot)
    {
        if (slot == null)
            return "NULL SLOT";

        string ownerName =
            slot.Owner == null
                ? "NULL"
                : slot.Owner.Data.CharacterName;

        string partName =
            slot.Part == null
                ? "NULL"
                : slot.Part.Type.ToString();

        string skillName =
            slot.Skill == null
                ? "NULL"
                : slot.Skill.SkillName;

        string targetName =
            slot.TargetCharacter == null
                ? "NULL"
                : slot.TargetCharacter.Data.CharacterName;

        string targetPartName =
            slot.TargetPart == null
                ? "NULL"
                : slot.TargetPart.Type.ToString();

        string targetSlotName =
            slot.TargetSlot == null
                ? "NULL"
                : $"{slot.TargetSlot.Owner.Data.CharacterName} {slot.TargetSlot.Part.Type}";

        return
            $"Owner      : {ownerName}\n" +
            $"Part       : {partName}\n" +
            $"Skill      : {skillName}\n" +
            $"Target     : {targetName}\n" +
            $"TargetPart : {targetPartName}\n" +
            $"Speed      : {slot.Speed}\n" +
            $"Phase      : {slot.Phase}\n" +
            $"TargetSlot : {targetSlotName}";
    }
}