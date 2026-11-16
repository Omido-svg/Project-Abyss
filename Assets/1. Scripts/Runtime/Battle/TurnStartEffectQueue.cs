using System;
using System.Collections.Generic;
using ProjectAbyss.Abilities;

/// <summary>Single FIFO application authority for turn-start reservations; trigger traps have a separate lifecycle.</summary>
public sealed class TurnStartEffectQueue
{
    private sealed class Entry
    {
        public long Sequence;
        public int DueTurn;
        public Action Apply;
        public Character Target;
        public BodyPart Part;
        public bool TargetLocked;
        public EffectCause Cause;
    }
    private readonly Queue<Entry> pending=new();
    private long sequence;
    private bool draining;
    public Func<int> CurrentTurn { get; set; }
    public int Count=>pending.Count;
    public bool IsFaulted { get; private set; }
    public long LastFailedSequence { get; private set; }
    public long AppliedCount { get; private set; }
    public long ExpiredCount { get; private set; }
    public void Enqueue(Action apply)
    {
        if(apply!=null)pending.Enqueue(new Entry{Sequence=checked(++sequence),Apply=apply});
    }
    public void EnqueueLocked(Character target,BodyPart part,Action apply,EffectCause cause=default,int dueTurn=0)
    {
        if(target==null || apply==null || part!=null && part.Owner!=target)return;
        pending.Enqueue(new Entry{Sequence=checked(++sequence),Apply=apply,Target=target,
            Part=part,TargetLocked=true,Cause=cause,DueTurn=dueTurn});
    }
    public void Clear()
    {
        // Battle-end cancellation may clear pending work from inside the current reservation.
        pending.Clear();IsFaulted=false;LastFailedSequence=0;AppliedCount=0;ExpiredCount=0;sequence=0;
    }
    public void Drain()
    {
        if(draining)return;
        if(IsFaulted)throw new InvalidOperationException("Reservation commit is indeterminate; reconcile before continuing.");
        draining=true;
        try
        {
            int skipped=0;
            while(pending.Count>0 && skipped<pending.Count)
            {
                var entry=pending.Dequeue();
                if(entry.DueTurn>0 && (CurrentTurn?.Invoke()??0)<entry.DueTurn){pending.Enqueue(entry);skipped++;continue;}
                skipped=0;
                if(entry.TargetLocked && (entry.Target==null || entry.Target.IsDead ||
                    entry.Part!=null && (entry.Part.IsBroken || entry.Part.Owner!=entry.Target)))
                {ExpiredCount++;continue;}
                try{entry.Apply();AppliedCount++;}
                catch(Exception e)
                {
                    IsFaulted=true;LastFailedSequence=entry.Sequence;
                    throw new InvalidOperationException("Reservation commit unknown: sequence="+entry.Sequence+
                        ",cause="+entry.Cause.EventId+",root="+entry.Cause.RootActionId+". Reconciliation required.",e);
                }
            }
        }
        finally{draining=false;}
    }
}
