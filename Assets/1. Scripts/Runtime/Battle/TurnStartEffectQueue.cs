using System;
using System.Collections.Generic;

/// <summary>1112 §2/18.2: one FIFO for all turn-start reservations.</summary>
public sealed class TurnStartEffectQueue
{
    private readonly Queue<Action> pending = new();
    private bool draining;
    public int Count => pending.Count;
    public void Enqueue(Action apply) { if (apply != null) pending.Enqueue(apply); }
    public void Clear() => pending.Clear();
    public void Drain()
    {
        if (draining) return;
        draining = true;
        try { while (pending.Count > 0) pending.Dequeue().Invoke(); }
        finally { draining = false; }
    }
}
