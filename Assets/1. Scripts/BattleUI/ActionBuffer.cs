using System.Collections.Generic;

public class ActionBuffer
{
    private List<BattleAction> buffer = new();

    public IReadOnlyList<BattleAction> Actions => buffer;

    public void Add(BattleAction action)
    {
        buffer.Add(action);
    }

    public void Remove(BattleAction action)
    {
        buffer.Remove(action);
    }

    public void Clear()
    {
        buffer.Clear();
    }

    public void Commit(ActionManager manager)
    {
        foreach (var action in buffer)
        {
            manager.AddAction(action);
        }

        Clear();
    }
}