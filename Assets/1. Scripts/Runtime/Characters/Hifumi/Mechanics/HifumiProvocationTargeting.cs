using System;
using System.Collections.Generic;
using System.Linq;
using ProjectAbyss.Abilities;

/// <summary>
/// Hifumi-only forced target projection. One pure resolver is shared by the
/// planning UI and ClashBuilder execution; no random-global mutation and no
/// ActionSlot change until ApplyToExecutionSlots.
/// </summary>
public static class HifumiProvocationTargeting
{
    /// <summary>
    /// Read-only aim projection for planning overlays and the order rail.
    /// Returned BodyParts are live target parts; none of the ActionSlots,
    /// target links, original skill assets or Unity RNG are changed.
    /// </summary>
    public static IReadOnlyDictionary<ActionSlot,BodyPart> PreviewTargetParts(
        IReadOnlyList<ActionSlot> slots)
    {
        var projected=new Dictionary<ActionSlot,BodyPart>();
        if(slots==null||slots.Count==0)return projected;

        var defenders=slots.Where(x=>x?.Phase==ActionPhase.COMBAT&&
                x.TargetCharacter is Hifumi)
            .Select(x=>x.TargetCharacter).Distinct().ToArray();

        foreach(var defender in defenders)
        {
            if(defender==null||defender.IsDead)continue;
            var mechanic=defender.GetMechanic<HifumiMechanic>();
            var active=mechanic?.GetActiveProvocationSnapshot();
            if(active==null||active.Count==0)continue;
            var valid=active.Where(x=>x.Key!=null&&x.Value>0&&
                    x.Key.Owner==defender&&!x.Key.IsBroken&&
                    defender.IsValidTargetPart(x.Key))
                .OrderBy(x=>x.Key.PartId,StringComparer.Ordinal).ToArray();
            if(valid.Length==0)continue;

            var attackers=slots.Where(x=>x!=null&&x.Phase==ActionPhase.COMBAT&&
                    x.ActionId>0&&x.TargetCharacter==defender&&
                    x.Owner!=null&&x.Owner!=defender&&!x.Owner.IsDead&&
                    x.Part?.IsBroken!=true&&x.Skill!=null&&
                    (x.Skill.ActionType==ActionType.Duel ||
                     x.Skill.ActionType==ActionType.NormalAttack))
                .GroupBy(x=>x.ActionId).Select(x=>x.First()).ToArray();
            if(attackers.Length==0)continue;
            long actorId=R4LiveSlotQuery.ActorRuntimeId(defender);
            var aims=attackers.Select(x=>new R4IncomingAim(
                x.ActionId,x.Speed,1,
                new CombatSlotRef(actorId,
                    x.TargetSlot?.SlotId??x.SlotId??x.ActionId.ToString(),
                    x.TargetPart?.PartId??string.Empty,0))).ToArray();
            var taunts=valid.Select(x=>new R4TauntTarget(
                new CombatSlotRef(actorId,
                    "R7_HIFUMI_TAUNT_"+x.Key.PartId,x.Key.PartId,0),
                x.Value,true)).ToArray();

            int seed=unchecked((int)(actorId*397)^attackers.Length);
            foreach(var x in attackers.OrderBy(x=>x.ActionId))
                seed=unchecked(seed*31+(int)(x.ActionId^(x.ActionId>>32)));

            var plan=R4TauntAimPlanner.Plan(aims,taunts,seed);
            var byId=attackers.ToDictionary(x=>x.ActionId);
            var byPart=valid.ToDictionary(x=>x.Key.PartId,x=>x.Key,
                StringComparer.Ordinal);
            foreach(var item in plan)
            {
                if(!item.WasForced||
                   !byId.TryGetValue(item.ActionId,out var attack)||
                   !byPart.TryGetValue(item.ResolvedSlot.PartId,out var part)||
                   part==null||part.IsBroken||
                   !defender.IsValidTargetPart(part))continue;
                projected[attack]=part;
            }
        }
        return projected;
    }

    public static int ApplyToExecutionSlots(IReadOnlyList<ActionSlot> combatSlots)
    {
        // All target choices come from the exact read-only planner above.
        var projected=PreviewTargetParts(combatSlots);
        int changed=0;
        foreach(var pair in projected)
        {
            if(pair.Key==null||pair.Value==null||pair.Key.TargetPart==pair.Value)
                continue;
            pair.Key.TargetPart=pair.Value;
            changed++;
        }
        return changed;
    }
}