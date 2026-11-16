using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>Iterative effect continuations. Distinct events in a cyclic chain remain legal.</summary>
    public sealed class AbilityEffectWorkQueue
    {
        private readonly Queue<(EffectCause Cause,Action Apply)> work=new();
        private bool draining;
        public int ProcessingBudget { get; set; }=4096;
        public bool IsFaulted { get; private set; }
        public string Fault { get; private set; }
        public int Count=>work.Count;
        public void Enqueue(EffectCause cause,Action apply)
        {
            if(IsFaulted)throw new InvalidOperationException(Fault);
            if(apply==null)throw new ArgumentNullException(nameof(apply));
            work.Enqueue((cause,apply));
        }
        public void Drain(int seed)
        {
            if(draining)return;
            if(IsFaulted)throw new InvalidOperationException(Fault);
            draining=true;int processed=0;
            try
            {
                while(work.Count>0)
                {
                    var item=work.Peek();
                    if(processed++>=Math.Max(1,ProcessingBudget))
                        throw new InvalidOperationException("Effect processing budget exhausted; pending="+work.Count+
                            ",event="+item.Cause.EventId+",parent="+item.Cause.ParentEventId+",root="+item.Cause.RootActionId+",seed="+seed);
                    work.Dequeue();item.Apply();
                }
            }
            catch(Exception e){IsFaulted=true;Fault=e.Message;throw;}
            finally{draining=false;}
        }
        public void Cancel(){work.Clear();IsFaulted=false;Fault=null;}
    }
}
