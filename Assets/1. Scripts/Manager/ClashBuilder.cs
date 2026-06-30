using System.Collections.Generic;
using UnityEngine;

public class ClashBuilder
{
    public ActionExecutionQueue BuildQueue(List<ActionSlot> slots)
    {
        ActionExecutionQueue queue = new();

        HashSet<ActionSlot> visited = new();

        LinkTargetSlots(slots);

        //---------------- PRETURN ----------------

        foreach (var slot in slots)
        {
            if (slot.Phase == ActionPhase.PRETURN)
                queue.PrestigeQueue.Enqueue(slot);
        }

        //---------------- FORESIGHT ----------------

        foreach (var slot in slots)
        {
            if (slot.Phase == ActionPhase.FORESIGHT)
                queue.AmbushQueue.Enqueue(slot);
        }

        //---------------- COMBAT ----------------

        foreach (var slot in slots)
        {
            if (slot.Phase != ActionPhase.COMBAT)
                continue;

            if (visited.Contains(slot))
                continue;

            //----------------------------------

            if (slot.TargetSlot == null)
            {
                queue.ClashQueue.Enqueue(
                    new ClashPair(slot));

                visited.Add(slot);
                continue;
            }

            //----------------------------------

            if (slot.TargetSlot.TargetSlot == slot)
            {
                queue.ClashQueue.Enqueue(
                    new ClashPair(slot, slot.TargetSlot));

                visited.Add(slot);
                visited.Add(slot.TargetSlot);

                continue;
            }

            //----------------------------------

            queue.ClashQueue.Enqueue(
                new ClashPair(slot));

            visited.Add(slot);
        }

        PrintSlots(slots);

        return queue;
    }

    //------------------------------------

    private void PrintSlots(List<ActionSlot> slots)
    {
        Debug.Log("===== SLOT CONNECTION =====");

        foreach (ActionSlot slot in slots)
        {
            string targetSlot =
                slot.TargetSlot == null
                    ? "NULL"
                    : slot.TargetSlot.Owner.Data.CharacterName;

            Debug.Log(
                $"{slot.Owner.Data.CharacterName} ({slot.Part.Type})"
                + $" -> "
                + $"{slot.TargetCharacter.Data.CharacterName} ({slot.TargetPart.Type})"
                + $" | TargetSlot = {targetSlot}");
        }
    }
}