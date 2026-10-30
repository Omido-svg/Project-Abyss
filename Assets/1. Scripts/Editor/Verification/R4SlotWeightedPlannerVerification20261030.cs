#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R4SlotWeightedPlannerVerification20261030
{
    static WeightedTargetSlot S(long actor, string slot, string part,
        int cost=1, int occupancy=0, int priority=0, int order=0, bool eligible=true)
    {
        return new WeightedTargetSlot(
            new CombatSlotRef(actor,slot,part,1),
            new HitTargetKey(actor,part),
            cost,occupancy,priority,order,eligible);
    }
    static string Key(SlotWeightedSelectionResult r)
    {
        if(r?.Allocation==null)return r?.Status.ToString()??"null";
        return string.Join(";",r.Allocation.SelectedSlots.Select(x=>
            x.Slot.ActorRuntimeId+":"+x.Slot.SlotId+":"+x.ActuallyConsumedWeight))+
            "|hits="+string.Join(";",r.Allocation.DistinctHitTargets.Select(x=>x.ActorRuntimeId+":"+x.PartId));
    }

    [MenuItem("Game System Verification/R4 - Slot Weighted Deterministic Planner (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only");
        int pass=0,fail=0;
        void Check(string id, bool good)
        {
            if(good){pass++;Debug.Log("[R4-W] PASS "+id);}
            else{fail++;Debug.LogError("[R4-W] FAIL "+id);}
        }

        var main=S(101,"A","HEAD");
        var a=S(102,"B","HEAD",1,3,10,2);
        var b=S(103,"C","ARM",1,0,10,2);
        var c=S(104,"D","LEG",1,1,10,2);
        var r=SlotWeightedTargetPlanner.Plan(501,2,3,main,new[]{a,b,c});
        Check("W01 primary always first",r.Success && r.Allocation.SelectedSlots[0].Slot.Equals(main.Slot));
        Check("W02 occupancy-lowest-first",r.Allocation.SelectedSlots.Count==3 &&
            r.Allocation.SelectedSlots[1].Slot.Equals(b.Slot) &&
            r.Allocation.SelectedSlots[2].Slot.Equals(c.Slot));
        Check("W03 budget consumed and 3 distinct hits",
            r.UnspentBudget==0 && r.Allocation.DistinctHitTargets.Count==3);
        var partialMain=SlotWeightedTargetPlanner.Plan(501,3,1,S(101,"A","HEAD",2),new[]{a});
        Check("W04 main cost above budget still selected",
            partialMain.Success && partialMain.Allocation.SelectedSlots.Count==1 &&
            partialMain.Allocation.SelectedSlots[0].ActuallyConsumedWeight==1);
        var partialLast=SlotWeightedTargetPlanner.Plan(501,4,2,main,
            new[]{S(102,"B","HEAD",3)});
        Check("W05 last partial allocation permitted",
            partialLast.Allocation.SelectedSlots.Count==2 &&
            partialLast.Allocation.SelectedSlots[1].NominalSlotWeightCost==3 &&
            partialLast.Allocation.SelectedSlots[1].ActuallyConsumedWeight==1);
        var doubleSlot=SlotWeightedTargetPlanner.Plan(501,5,2,main,
            new[]{S(101,"A2","HEAD")});
        Check("W06 same part different slot => one hit",
            doubleSlot.Allocation.SelectedSlots.Count==2 &&
            doubleSlot.Allocation.DistinctHitTargets.Count==1);
        var otherPart=SlotWeightedTargetPlanner.Plan(501,6,2,main,
            new[]{S(101,"A2","ARM")});
        Check("W07 same boss different parts => two hits",
            otherPart.Allocation.DistinctHitTargets.Count==2);
        var noPart=SlotWeightedTargetPlanner.Plan(501,7,2,S(101,"A",null),
            new[]{S(101,"A2",null)});
        Check("W08 partless enemy different slots => one hit",
            noPart.Allocation.DistinctHitTargets.Count==1);
        var insufficient=SlotWeightedTargetPlanner.Plan(501,8,4,main,
            Array.Empty<WeightedTargetSlot>());
        Check("W09 insufficient candidates -> unspent budget",
            insufficient.UnspentBudget==3 && insufficient.Allocation.SelectedSlots.Count==1);
        var invalid=SlotWeightedTargetPlanner.Plan(501,9,2,S(0,"",null),
            new[]{a,b});
        Check("W10 invalid main returns failure",!invalid.Success &&
            invalid.Status==SlotWeightedSelectionStatus.InvalidPrimary &&
            invalid.Allocation==null);
        var notEligible=SlotWeightedTargetPlanner.Plan(501,10,3,main,
            new[]{S(102,"B","HEAD",1,0,0,0,false),S(103,"C","ARM")});
        Check("W11 ineligible secondary filtered",notEligible.Allocation.SelectedSlots.Count==2 &&
            notEligible.Allocation.SelectedSlots[1].Slot.ActorRuntimeId==103);
        var same=SlotWeightedTargetPlanner.Plan(501,2,3,main,
            new[]{c,a,b});
        Check("W12 reversed input gives identical ordered allocation",Key(same)==Key(r));
        var duplicate=SlotWeightedTargetPlanner.Plan(501,11,3,main,new[]{a,a,a,b});
        Check("W13 duplicate identical SlotRef not allocated twice",
            duplicate.Allocation.SelectedSlots.Count==3);
        var conflict=SlotWeightedTargetPlanner.Plan(501,12,3,main,new[]{a,
            S(102,"B","HEAD",2,3,10,2)});
        Check("W14 conflicting slot snapshot explicitly rejected",
            !conflict.Success &&
            conflict.Status==SlotWeightedSelectionStatus.ConflictingSlotSnapshot);
        var priority1=SlotWeightedTargetPlanner.Plan(501,13,2,main,new[]{
            S(102,"B","HEAD",1,0,8,1),S(103,"C","ARM",1,0,2,7)});
        Check("W15 explicit area priority before slot order",
            priority1.Allocation.SelectedSlots[1].Slot.ActorRuntimeId==103);
        var noRngBefore=UnityEngine.Random.state;
        var res=SlotWeightedTargetPlanner.Plan(501,14,2,main,new[]{a,b,c});
        var noRngAfter=UnityEngine.Random.state;
        // Same state means query did not consume Unity battle RNG.
        Check("W16 planning query does not consume Unity Random",
            UnityEngine.JsonUtility.ToJson(noRngBefore)==UnityEngine.JsonUtility.ToJson(noRngAfter));
        Check("W17 planning snapshot action/revision preserved",
            res.Allocation.ActionId==501 && res.Allocation.PlanningRevision==14);
        Check("W18 no legacy AttackWeightSettings rewriting",
            typeof(AttackWeightSettings).GetField("Weight")!=null &&
            (int)AttackWeightTargetingModel.LegacyCharacterRandom==0 &&
            (int)AttackWeightTargetingModel.SlotWeighted==1);

        Debug.Log("[R4-W] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R4-W planner failed "+fail);
    }
}
#endif
