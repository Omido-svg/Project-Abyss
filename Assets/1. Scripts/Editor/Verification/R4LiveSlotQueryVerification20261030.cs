#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R4LiveSlotQueryVerification20261030
{
    [MenuItem("Game System Verification/R4 - Live Slot Query (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("R4 live slot query requires Play Mode.");

        var manager = UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var battle = manager?.BattleContext;
        if (battle?.Player == null || battle.Enemies == null || battle.Enemies.Count == 0)
            throw new InvalidOperationException("BattleManager, player and enemy roster required.");

        int pass=0, fail=0;
        void Check(string id,bool ok, string info="")
        {
            if(ok){pass++;Debug.Log("[R4-Live] PASS "+id+" "+info);}
            else{fail++;Debug.LogError("[R4-Live] FAIL "+id+" "+info);}
        }

        var elite = battle.Enemies.FirstOrDefault(e => e != null && e.UsesBodyParts &&
            e.BodyParts != null && e.BodyParts.Count >= 2);
        var normal = battle.Enemies.FirstOrDefault(e => e != null && !e.UsesBodyParts);
        if(elite == null || normal == null)
            throw new InvalidOperationException("Battle Test Scene needs one elite with parts and one partless normal.");

        var points=elite.GetTargetPoints(false).Where(p=>p.IsValid).ToArray();
        var first=points.First().Part;
        var second=points.Skip(1).First().Part;
        long eliteId=R4LiveSlotQuery.ActorRuntimeId(elite);
        long normalId=R4LiveSlotQuery.ActorRuntimeId(normal);
        var source=new ActionSlot {ActionId=92100, Owner=battle.Player,
            TargetCharacter=elite,TargetPart=first,Phase=ActionPhase.COMBAT};
        var planned=new List<ActionSlot>{source};
        var beforeRandom=UnityEngine.JsonUtility.ToJson(UnityEngine.Random.state);
        var snapshot=R4LiveSlotQuery.Capture(source,battle.Enemies,planned,7,3);
        var afterRandom=UnityEngine.JsonUtility.ToJson(UnityEngine.Random.state);

        Check("L01 real elite actor ID scoped to runtime",eliteId>0 && eliteId!=normalId);
        Check("L02 actual configured elite slots imported",snapshot.Candidates.Count(c=>c.Slot.ActorRuntimeId==eliteId) >=4,
            "eliteCandidateCount="+snapshot.Candidates.Count(c=>c.Slot.ActorRuntimeId==eliteId));
        Check("L03 no executable ActionSlot created",planned.Count==1 && planned[0]==source);
        Check("L04 main body part resolved",snapshot.HasUnambiguousMain &&
            snapshot.Primary.HitTarget.PartId==first.PartId);
        Check("L05 slot planner works with live targetable candidates",
            snapshot.Selection.Success &&
            snapshot.Selection.Allocation.SelectedSlots.Count==3);
        Check("L06 point-only extra body part retained",
            points.All(pt=>snapshot.Candidates.Any(c=>c.Slot.ActorRuntimeId==eliteId &&
                c.HitTarget.PartId==pt.Part?.PartId)),
            "targetPoints="+points.Length);
        Check("L07 partless enemy targetable as actor",
            snapshot.Candidates.Any(c=>c.Slot.ActorRuntimeId==normalId &&
                c.HitTarget.PartId==""));
        Check("L08 occupancy reads current planning, not secondary selection",
            snapshot.Candidates.Any(c=>c.Slot.ActorRuntimeId==eliteId &&
                c.HitTarget.PartId==first.PartId && c.OccupiedMainTargetCount==1));
        Check("L09 preview does not consume Unity Random",beforeRandom==afterRandom);
        var repeat=R4LiveSlotQuery.Capture(source,battle.Enemies,planned,7,3);
        var seq=snapshot.Selection.Allocation.SelectedSlots.Select(s=>s.Slot.ActorRuntimeId+":"+s.Slot.SlotId).ToArray();
        var seq2=repeat.Selection.Allocation.SelectedSlots.Select(s=>s.Slot.ActorRuntimeId+":"+s.Slot.SlotId).ToArray();
        Check("L10 exact-repeat deterministic",seq.SequenceEqual(seq2));

        var twoPlans=new List<ActionSlot>{source,new ActionSlot{ActionId=92101,
            Owner=battle.Player,TargetCharacter=elite,TargetPart=first,Phase=ActionPhase.COMBAT}};
        var occupied=R4LiveSlotQuery.Capture(source,battle.Enemies,twoPlans,8,2);
        Check("L11 two main plans occupy same target",occupied.Candidates.Any(c=>
            c.HitTarget.ActorRuntimeId==eliteId &&
            c.HitTarget.PartId==first.PartId && c.OccupiedMainTargetCount==2));

        var explicitTargetSlot = new ActionSlot{Owner=elite,SlotId=snapshot.Primary.Slot.SlotId,
            Part=first,Phase=ActionPhase.COMBAT};
        var cross=new ActionSlot{Owner=battle.Player,ActionId=92102,
            TargetCharacter=elite,TargetPart=second,TargetSlot=explicitTargetSlot,Phase=ActionPhase.COMBAT};
        var redirected=R4LiveSlotQuery.Capture(cross,battle.Enemies,new[]{cross},9,2);
        Check("L12 selected clash slot independent of damage part",
            redirected.HasUnambiguousMain &&
            redirected.Primary.Slot.SlotId==explicitTargetSlot.SlotId &&
            redirected.Primary.HitTarget.PartId==second.PartId);

        var noPart=new ActionSlot {Owner=battle.Player,ActionId=92103,
            TargetCharacter=elite,TargetPart=null,TargetSlot=explicitTargetSlot,Phase=ActionPhase.COMBAT};
        var badPart=R4LiveSlotQuery.Capture(noPart,battle.Enemies,new[]{noPart},10,2);
        Check("L13 bodypart actor requires explicit valid hit part",!badPart.HasUnambiguousMain);

        var wrongSide=R4LiveSlotQuery.Capture(source,new[]{normal},planned,11,2);
        Check("L14 cross-faction/invalid-side primary rejected",!wrongSide.HasUnambiguousMain);

        var normalSource=new ActionSlot{Owner=battle.Player,ActionId=92104,
            TargetCharacter=normal,TargetPart=null,Phase=ActionPhase.COMBAT};
        var noParts=R4LiveSlotQuery.Capture(normalSource,battle.Enemies,new[]{normalSource},12,2);
        Check("L15 real partless normal resolves without fake body part",
            noParts.HasUnambiguousMain && noParts.Primary.HitTarget.PartId=="");

        Check("L16 planning revision and action ID preserved",
            snapshot.Selection.Allocation.PlanningRevision==7 &&
            snapshot.Selection.Allocation.ActionId==92100);

        Debug.Log("[R4-Live] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)
            throw new InvalidOperationException("R4-Live checks failed: "+fail);
    }
}
#endif
