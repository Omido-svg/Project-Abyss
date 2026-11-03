#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>R4-K pure taunt action budgets and stealth slot eligibility.</summary>
public static class R4TargetPolicyVerification20261102
{
    private sealed class Visibility : IR4TargetVisibilityQuery
    {
        private readonly HashSet<string> blocked=new HashSet<string>();
        public void Block(CombatSlotRef slot)=>blocked.Add(Key(slot));
        private static string Key(CombatSlotRef s)=>s.ActorRuntimeId+":"+s.SlotId+":"+s.PartId;
        public bool IsTargetable(long attackerId,in CombatSlotRef target)=>
            !blocked.Contains(Key(target));
    }
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
        int pass=0,fail=0;
        void Check(string id,bool yes,string details="")
        {
            if(yes){pass++;Debug.Log("[R4-K] PASS "+id+" "+details);}
            else{fail++;Debug.LogError("[R4-K] FAIL "+id+" "+details);}
        }
        var source=new CombatSlotRef(101,"ORIGINAL","HEAD",0);
        var taunted=new CombatSlotRef(201,"HIFUMI-TAUNT","ARM",0);
        var secondary=new CombatSlotRef(202,"LOW-TAUNT","LEG",0);
        var attacks=new[]{
            new R4IncomingAim(11,8,5,source), // 5 rolls still one action charge
            new R4IncomingAim(12,12,1,source),
            new R4IncomingAim(13,10,3,source),
            new R4IncomingAim(14,4,1,source)
        };
        var priority=new[]{
            new R4TauntTarget(taunted,2,true),
            new R4TauntTarget(secondary,1,true)
        };
        var plan=R4TauntAimPlanner.Plan(attacks,priority,554433);
        Check("K01 taunt N=2 consumes two enemy ACTIONS, not 5-roll card count",
            plan.Count==4 && plan.Count(p=>p.WasForced && p.ResolvedSlot.Equals(taunted))==2,
            "two attack actions redirected to taunted part");
        Check("K02 highest speed attacks assigned to highest N taunt",
            plan.Where(p=>p.ResolvedSlot.Equals(taunted)).Select(p=>p.ActionId)
                .OrderBy(x=>x).SequenceEqual(new long[]{12,13}),
            "speed 12+10 before 8+4");
        Check("K03 secondary taunt consumes exactly one attack action",
            plan.Count(p=>p.WasForced && p.ResolvedSlot.Equals(secondary))==1 &&
            plan.Single(p=>p.ResolvedSlot.Equals(secondary)).ActionId==11,
            "remaining next highest speed 8");
        Check("K04 surplus enemy action retains original aim",
            plan.Single(p=>p.ActionId==14).ResolvedSlot.Equals(source)&&
            !plan.Single(p=>p.ActionId==14).WasForced);
        Check("K05 taunt query does not write to original ActionSlot",
            attacks.All(x=>x.OriginalTargetSlot.Equals(source)));
        var before=JsonUtility.ToJson(UnityEngine.Random.state);
        var reversed=R4TauntAimPlanner.Plan(attacks.Reverse(),priority.Reverse(),554433);
        Check("K06 reversed input produces exactly same seeded assignment",
            string.Join("|",plan.Select(p=>p.ActionId+":"+p.ResolvedSlot.SlotId))==
            string.Join("|",reversed.Select(p=>p.ActionId+":"+p.ResolvedSlot.SlotId)));
        Check("K07 seeded taunt tie selection does not consume Unity RNG",
            before==JsonUtility.ToJson(UnityEngine.Random.state));
        var ineligible=R4TauntAimPlanner.Plan(attacks,
            new[]{new R4TauntTarget(taunted,20,false)},5);
        Check("K08 invalid/stealthed taunt candidate never redirects",
            ineligible.All(p=>!p.WasForced&&p.ResolvedSlot.Equals(source)));
        var none=R4TauntAimPlanner.Plan(Array.Empty<R4IncomingAim>(),priority,5);
        Check("K09 no enemy slots returns empty assignment",none.Count==0);

        var visible=new Visibility();visible.Block(taunted);
        var rule=new R4VisibilityPlanningRule(visible);
        var ctx=new R4TargetPlanningContext(777,101,201,3);
        var slotA=new WeightedTargetSlot(taunted,new HitTargetKey(201,"ARM"),1,0,0,0);
        var slotB=new WeightedTargetSlot(secondary,new HitTargetKey(202,"LEG"),1,0,0,1);
        Check("K10 snapshot visibility filters blocked part, not owner action",
            !rule.Allows(in ctx,slotA) && rule.Allows(in ctx,slotB));
        var chosen=R4DeterministicTargetQuery.Select(
            new[]{slotA,slotB},2,R4TargetSelectionMode.StableFirst,5,rule,ctx);
        Check("K11 visibility must filter BEFORE target budgeting",
            chosen.Count==1&&chosen[0].Slot.Equals(secondary));
        Check("K12 source visibility query preserves different PartIds",
            !rule.Allows(in ctx,slotA)&&rule.Allows(in ctx,slotB));
        Debug.Log("[R4-K] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R4 target policy failed "+fail);
    }
}
#endif
