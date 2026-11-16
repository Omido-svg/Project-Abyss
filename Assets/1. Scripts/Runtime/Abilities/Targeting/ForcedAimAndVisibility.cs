using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R4-K pure forced-aim draft. One incoming attack ACTION consumes one
    /// taunt charge regardless of its number of rolls. It cannot directly
    /// overwrite an ActionSlot or execute an attack.
    /// </summary>
    public readonly struct IncomingAim
    {
        public readonly long ActionId;
        public readonly int Speed;
        public readonly int RollCount;
        public readonly CombatSlotRef OriginalTargetSlot;
        public IncomingAim(long actionId,int speed,int rollCount,CombatSlotRef original)
        {
            ActionId=actionId;Speed=speed;RollCount=rollCount;
            OriginalTargetSlot=original;
        }
    }
    public readonly struct TauntTarget
    {
        public readonly CombatSlotRef TargetSlot;
        public readonly int RedirectActionCount;
        public readonly bool Eligible;
        public TauntTarget(CombatSlotRef target,int count,bool eligible)
        {
            TargetSlot=target;RedirectActionCount=Math.Max(0,count);Eligible=eligible;
        }
    }
    public readonly struct ForcedAim
    {
        public readonly long ActionId;
        public readonly CombatSlotRef OriginalSlot;
        public readonly CombatSlotRef ResolvedSlot;
        public readonly bool WasForced;
        public ForcedAim(long action,CombatSlotRef original,
            CombatSlotRef resolved,bool forced)
        {
            ActionId=action;OriginalSlot=original;
            ResolvedSlot=resolved;WasForced=forced;
        }
    }
    public static class TauntAimPlanner
    {
        /// <summary>
        /// Process highest-N eligible taunt part first, and assign highest
        /// speed attacking ActionSlots first. Explicit seeded RNG is consumed
        /// ONLY for N/speed ties. Missing targets keep original planned aim.
        /// </summary>
        public static IReadOnlyList<ForcedAim> Plan(
            IEnumerable<IncomingAim> incoming,
            IEnumerable<TauntTarget> taunts,int seed)
        {
            var attacks=(incoming??Array.Empty<IncomingAim>())
                .Where(x=>x.ActionId>0)
                .OrderByDescending(x=>x.Speed)
                .ThenBy(x=>x.ActionId).ToList();
            var rules=(taunts??Array.Empty<TauntTarget>())
                .Where(x=>x.Eligible&&x.RedirectActionCount>0&&
                    x.TargetSlot.ActorRuntimeId>0&&!string.IsNullOrEmpty(x.TargetSlot.SlotId))
                .OrderByDescending(x=>x.RedirectActionCount)
                .ThenBy(x=>x.TargetSlot.ActorRuntimeId)
                .ThenBy(x=>x.TargetSlot.SlotId,StringComparer.Ordinal)
                .ThenBy(x=>x.TargetSlot.PartId,StringComparer.Ordinal)
                .ToList();
            var rng=new Random(seed);
            // Shuffle strictly tied speeds/taunt priorities, with input order
            // already stabilized so repeated seed is reproducible.
            static void ShuffleGroup<T>(List<T> list,int start,int end,Random random)
            {
                for(int i=end-1;i>start;i--)
                {
                    int j=random.Next(start,i+1);
                    T t=list[i];list[i]=list[j];list[j]=t;
                }
            }
            for(int i=0;i<attacks.Count;)
            {
                int j=i+1;while(j<attacks.Count&&attacks[i].Speed==attacks[j].Speed)j++;
                if(j-i>1)ShuffleGroup(attacks,i,j,rng);
                i=j;
            }
            for(int i=0;i<rules.Count;)
            {
                int j=i+1;while(j<rules.Count&&
                    rules[i].RedirectActionCount==rules[j].RedirectActionCount)j++;
                if(j-i>1)ShuffleGroup(rules,i,j,rng);
                i=j;
            }
            var output=new List<ForcedAim>(attacks.Count);
            int next=0;
            foreach(var t in rules)
                for(int i=0;i<t.RedirectActionCount&&next<attacks.Count;i++,next++)
                {
                    var incomingAction=attacks[next];
                    output.Add(new ForcedAim(incomingAction.ActionId,
                        incomingAction.OriginalTargetSlot,t.TargetSlot,true));
                }
            for(;next<attacks.Count;next++)
            {
                var action=attacks[next];
                output.Add(new ForcedAim(action.ActionId,
                    action.OriginalTargetSlot,action.OriginalTargetSlot,false));
            }
            return output.AsReadOnly();
        }
    }

    /// <summary>R4-K visibility source: existing character/part mechanic owns state.</summary>
    public interface ITargetVisibilityQuery
    {
        bool IsTargetable(long attackerId,in CombatSlotRef target);
    }

    public sealed class VisibilityPlanningRule : ITargetPlanningRule
    {
        private readonly ITargetVisibilityQuery source;
        public VisibilityPlanningRule(ITargetVisibilityQuery source)
        {
            this.source=source??throw new ArgumentNullException(nameof(source));
        }
        public bool Allows(in TargetPlanningContext context,WeightedTargetSlot candidate)
        {
            if(candidate==null)return false;
            var slot=candidate.Slot;
            return source.IsTargetable(context.OwnerActorId,in slot);
        }
    }
}
