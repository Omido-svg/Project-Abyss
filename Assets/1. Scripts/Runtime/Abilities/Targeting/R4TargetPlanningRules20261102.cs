using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R4 planning rule is a read-only eligibility filter. Taunt, stealth,
    /// faction and locking rules participate here BEFORE budget allocation.
    /// Nothing may create ActionSlots, mutate the combat plan, or draw RNG.
    /// </summary>
    public interface IR4TargetPlanningRule
    {
        bool Allows(in R4TargetPlanningContext context, WeightedTargetSlot candidate);
    }

    public readonly struct R4TargetPlanningContext
    {
        public readonly long SourceActionId;
        public readonly long OwnerActorId;
        public readonly long PrimaryActorId;
        public readonly int Revision;
        public R4TargetPlanningContext(long actionId,long ownerId,long primaryActorId,int revision)
        {
            SourceActionId=actionId;OwnerActorId=ownerId;
            PrimaryActorId=primaryActorId;Revision=revision;
        }
    }

    public sealed class R4TargetPlanningRules : IR4TargetPlanningRule
    {
        private readonly IR4TargetPlanningRule[] rules;
        public R4TargetPlanningRules(IEnumerable<IR4TargetPlanningRule> rules)
        {
            this.rules=(rules??Array.Empty<IR4TargetPlanningRule>())
                .Where(x=>x!=null).ToArray();
        }
        public bool Allows(in R4TargetPlanningContext context,WeightedTargetSlot candidate)
        {
            foreach(var rule in rules)
                if(!rule.Allows(in context,candidate))return false;
            return true;
        }
    }

    public enum R4TargetSelectionMode
    {
        StableFirst,
        SeededRandom
    }

    /// <summary>
    /// Explicit seed, stable ordering and independent RNG stream:
    /// never consume UnityEngine.Random or the battle RNG during previews.
    /// Stable ID ordering is required before seeded selection.
    /// </summary>
    public static class R4DeterministicTargetQuery
    {
        private sealed class SlotIdentityComparer : IComparer<WeightedTargetSlot>
        {
            public int Compare(WeightedTargetSlot a,WeightedTargetSlot b)
            {
                int c=a.Slot.ActorRuntimeId.CompareTo(b.Slot.ActorRuntimeId);
                if(c!=0)return c;
                c=string.CompareOrdinal(a.Slot.SlotId,b.Slot.SlotId);
                if(c!=0)return c;
                c=string.CompareOrdinal(a.Slot.PartId,b.Slot.PartId);
                if(c!=0)return c;
                return a.Slot.Generation.CompareTo(b.Slot.Generation);
            }
        }

        public static IReadOnlyList<WeightedTargetSlot> Select(
            IEnumerable<WeightedTargetSlot> candidates,int take,
            R4TargetSelectionMode mode,int seed,
            IR4TargetPlanningRule rule,R4TargetPlanningContext context)
        {
            var list=new List<WeightedTargetSlot>();
            if(candidates!=null)
                foreach(var candidate in candidates)
                    if(candidate!=null&&candidate.Eligible&&
                        candidate.Slot.ActorRuntimeId>0 &&
                        (rule==null||rule.Allows(in context,candidate)))
                        list.Add(candidate);
            list.Sort(new SlotIdentityComparer());
            if(mode==R4TargetSelectionMode.SeededRandom)
            {
                var rng=new Random(seed);
                for(int i=list.Count-1;i>0;i--)
                {
                    int j=rng.Next(i+1);
                    var tmp=list[i];list[i]=list[j];list[j]=tmp;
                }
            }
            if(take>=0 && list.Count>take)
                list.RemoveRange(take,list.Count-take);
            return list.AsReadOnly();
        }
    }
}
