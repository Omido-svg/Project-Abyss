using System.Collections.Generic;

public class ActionBuffer
{
    private readonly List<ActionSlot> buffer = new();

    public IReadOnlyList<ActionSlot> Slots => buffer;

    //--------------------------------------------

    public void Add(ActionSlot slot)
    {
        if (slot == null)
            return;

        buffer.Remove(slot);
        buffer.Add(slot);
    }

    //--------------------------------------------

    public void Remove(ActionSlot slot)
    {
        buffer.Remove(slot);
    }

    //--------------------------------------------

    public void Clear()
    {
        buffer.Clear();
    }

    //--------------------------------------------

    public void Commit(ActionManager manager)
    {
        foreach (ActionSlot slot in buffer)
        {
            manager.AddSlot(slot);
        }

        Clear();
    }
    
    public ActionSlot FindSlot(Character owner, BodyPart part)
    {
        foreach (var slot in buffer)
        {
            if (slot.Owner == owner &&
                slot.Part == part)
                return slot;
        }

        return null;
    }
}