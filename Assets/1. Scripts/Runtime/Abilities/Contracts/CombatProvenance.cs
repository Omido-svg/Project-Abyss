using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public interface IReadOnlyCombatProvenance
    {
        bool TryGet(long eventId,out EffectCause cause);
        IReadOnlyList<EffectCause> Contributors(long eventId);
    }
    public sealed class CombatProvenance : IReadOnlyCombatProvenance
    {
        private readonly Dictionary<long,EffectCause> events=new();
        private readonly Dictionary<long,IReadOnlyList<EffectCause>> contributors=new();
        internal void Record(EffectCause cause)
        {
            if(cause.EventId<=0)return;
            if(events.TryGetValue(cause.EventId,out var previous)&&!previous.IsSameEvent(cause))
                throw new System.InvalidOperationException("Conflicting immutable cause identity: "+cause.EventId);
            events[cause.EventId]=cause;
        }
        internal void RecordContributors(long eventId,IEnumerable<EffectCause> causes)=>
            contributors[eventId]=new List<EffectCause>(causes).AsReadOnly();
        public bool TryGet(long eventId,out EffectCause cause)=>events.TryGetValue(eventId,out cause);
        public IReadOnlyList<EffectCause> Contributors(long eventId)=>contributors.TryGetValue(eventId,out var list)?list:System.Array.Empty<EffectCause>();
    }
}
