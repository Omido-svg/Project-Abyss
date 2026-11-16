using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace ProjectAbyss.Abilities
{
    public class CounterExecutionContext
    {
        public long ExecutionId { get; }
        public EffectCause Cause { get; }
        public IReadOnlyDictionary<string,long> StartFacts { get; }
        public int PendingRolls { get; internal set; }
        public int ProcessedRolls { get; internal set; }
        public int AttemptedRolls { get; internal set; }
        public long NewlyGrantedResources { get; internal set; }
        public CounterExecutionContext(long id,int rolls,EffectCause cause,IDictionary<string,long> facts)
        {
            if(id<=0||rolls<0)throw new ArgumentException("Invalid counter execution");
            ExecutionId=id;Cause=cause;PendingRolls=rolls;
            StartFacts=new ReadOnlyDictionary<string,long>(new Dictionary<string,long>(facts??new Dictionary<string,long>()));
        }
    }
}
