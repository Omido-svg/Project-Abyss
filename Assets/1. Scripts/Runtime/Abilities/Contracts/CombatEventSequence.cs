using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>Battle-owned event identities. Repeated observation of a boundary returns the same ID.</summary>
    public sealed class CombatEventSequence
    {
        public CombatProvenance Provenance { get; }=new();
        public void Record(EffectCause cause)=>Provenance.Record(cause);
        public void RecordContributors(long eventId,System.Collections.Generic.IEnumerable<EffectCause> contributors)=>Provenance.RecordContributors(eventId,contributors);
        private long nextId;
        private readonly Dictionary<(long,AbilityEventKind,int),long> boundaries=new();
        public long Next() => checked(++nextId);
        public long Boundary(long actionId,AbilityEventKind kind,int rollIndex)
        {
            if(actionId<=0)throw new ArgumentException("An event boundary needs an action ID.");
            var key=(actionId,kind,rollIndex);
            if(!boundaries.TryGetValue(key,out var id))boundaries.Add(key,id=Next());
            return id;
        }
    }
}
