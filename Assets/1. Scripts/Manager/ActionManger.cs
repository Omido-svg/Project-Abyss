using System.Collections.Generic;

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
    private readonly BattleContext battleContext;

    // BattleAction이 아니라 ActionSlot을 저장
    private readonly List<ActionSlot> slots = new();

    public IReadOnlyList<ActionSlot> Slots => slots;

    public ActionManager(BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }

    //------------------------------------------------

    public void Clear()
    {
        slots.Clear();
    }

    //------------------------------------------------

    public void AddSlot(ActionSlot slot)
    {
        if (slot == null)
            return;

        slots.Add(slot);
    }

    //------------------------------------------------

    public void RemoveSlot(ActionSlot slot)
    {
        slots.Remove(slot);
    }

    //------------------------------------------------

    public ActionSlot FindSlot(Character owner, BodyPart part)
    {
        foreach (var slot in slots)
        {
            if (slot.Owner == owner &&
                slot.Part == part)
            {
                return slot;
            }
        }

        return null;
    }

    //------------------------------------------------

    public List<ActionSlot> GetCombatSlots()
    {
        List<ActionSlot> result = new();

        foreach (var slot in slots)
        {
            if (slot.Phase == ActionPhase.COMBAT)
                result.Add(slot);
        }

        result.Sort((a, b) => b.Speed.CompareTo(a.Speed));

        return result;
    }

    //------------------------------------------------

    public List<ActionSlot> GetPhaseSlots(ActionPhase phase)
    {
        List<ActionSlot> result = new();

        foreach (var slot in slots)
        {
            if (slot.Phase == phase)
                result.Add(slot);
        }

        result.Sort((a, b) => b.Speed.CompareTo(a.Speed));

        return result;
    }
}