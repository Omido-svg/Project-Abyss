#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>R4 deterministic candidate selection and immutable target boundaries.</summary>
public static class TargetPlanningContractVerification
{
    private sealed class Exclude : IR4TargetPlanningRule
    {
        private readonly long actor;
        public Exclude(long actor){this.actor=actor;}
        public bool Allows(in R4TargetPlanningContext ctx,WeightedTargetSlot slot)=>
            slot.Slot.ActorRuntimeId!=actor;
    }
    private static WeightedTargetSlot S(long id,string slot,string part,
        int cost=1,int occupied=0,int area=0,int order=0,bool eligible=true)=>
        new WeightedTargetSlot(new CombatSlotRef(id,slot,part,0),
            new HitTargetKey(id,part),cost,occupied,area,order,eligible);

    private static string Key(IEnumerable<WeightedTargetSlot> slots)=>
        string.Join("|",slots.Select(s=>s.Slot.ActorRuntimeId+":"+s.Slot.SlotId+":"+s.Slot.PartId));

    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("R4 query test requires EditMode");
        int pass=0,fail=0;
        void Check(string name,bool yes,string details="")
        {
            if(yes){pass++;Debug.Log("[R4-Query] PASS "+name+" "+details);}
            else{fail++;Debug.LogError("[R4-Query] FAIL "+name+" "+details);}
        }
        var values=new[]{S(403,"Z","arm"),S(401,"A",""),S(402,"X","head"),
            S(403,"A","leg"),S(404,"N","",eligible:false)};
        var ctx=new R4TargetPlanningContext(9801,1,401,7);
        var original=values.ToArray();
        var sorted=R4DeterministicTargetQuery.Select(values,10,
            R4TargetSelectionMode.StableFirst,1,null,ctx);
        Check("Q01 stable actor/slot/part sorting",
            Key(sorted)=="401:A:|402:X:head|403:A:leg|403:Z:arm",
            Key(sorted));
        var reversed=R4DeterministicTargetQuery.Select(values.Reverse(),10,
            R4TargetSelectionMode.StableFirst,1,null,ctx);
        Check("Q02 candidate input enumeration does not change order",
            Key(sorted)==Key(reversed));
        Check("Q03 ineligible slots removed without changing source collection",
            sorted.Count==4 && values.Length==5 &&
            values.SequenceEqual(original));
        var rule=new R4TargetPlanningRules(new IR4TargetPlanningRule[]{new Exclude(402),null});
        var filtered=R4DeterministicTargetQuery.Select(values,10,
            R4TargetSelectionMode.StableFirst,17,rule,ctx);
        Check("Q04 read-only eligibility policy excludes actor before selection",
            filtered.Count==3&&filtered.All(v=>v.Slot.ActorRuntimeId!=402));
        var randomBefore=JsonUtility.ToJson(UnityEngine.Random.state);
        var seeded=R4DeterministicTargetQuery.Select(values,3,
            R4TargetSelectionMode.SeededRandom,7654321,null,ctx);
        string expected=Key(seeded);
        bool identical=true;
        for(int i=0;i<1000;i++)
            if(Key(R4DeterministicTargetQuery.Select(
                    i%2==0?values:values.Reverse(),3,
                    R4TargetSelectionMode.SeededRandom,7654321,null,ctx))!=expected)
            {identical=false;break;}
        Check("Q05 1000 fixed-seed picks stable independent of candidate order",
            identical,"seed=7654321 "+expected);
        Check("Q06 query does not consume Unity Random",
            randomBefore==JsonUtility.ToJson(UnityEngine.Random.state));
        Check("Q07 no candidates fail safely",
            R4DeterministicTargetQuery.Select(Array.Empty<WeightedTargetSlot>(),2,
                R4TargetSelectionMode.StableFirst,0,null,ctx).Count==0);
        Check("Q08 take zero yields zero without spinning RNG",
            R4DeterministicTargetQuery.Select(values,0,
                R4TargetSelectionMode.StableFirst,0,null,ctx).Count==0);
        var main=S(500,"MAIN","HEAD",2);
        var sub=S(501,"SECOND","ARM",3);
        var p=SlotWeightedTargetPlanner.Plan(901,7,3,main,new[]{sub});
        Check("Q09 main cost 2 and final partial cost 1",
            p.Success&&p.Allocation.SelectedSlots.Count==2 &&
            p.Allocation.SelectedSlots[0].ActuallyConsumedWeight==2 &&
            p.Allocation.SelectedSlots[1].ActuallyConsumedWeight==1 &&
            p.UnspentBudget==0);
        var duplicate=SlotWeightedTargetPlanner.Plan(902,8,4,main,new[]{
            S(500,"ANOTHER","HEAD"),S(500,"ANOTHER2","HEAD")});
        Check("Q10 multiple budget slots do not multiply same physical part damage",
            duplicate.Success&&duplicate.Allocation.SelectedSlots.Count==3 &&
            duplicate.Allocation.DistinctHitTargets.Count==1);
        var partless=SlotWeightedTargetPlanner.Plan(903,9,2,S(600,"MAIN",""),
            new[]{S(600,"EXTRA","")});
        Check("Q11 normal enemy actor-only target is one physical hit",
            partless.Success&&partless.Allocation.DistinctHitTargets.Count==1);
        Check("Q12 planner never inserts new executable ActionSlots",
            p.Allocation.SelectedSlots.All(x=>x.Slot.ActorRuntimeId>0)&&
            p.Allocation.ActionId==901,"pure CombatSlotRef and HitTargetKey");
        Debug.Log("[R4-Query] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R4 stable query failed "+fail);
    }
}
#endif
