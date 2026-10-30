#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R4PlanningSnapshotCacheVerification20261030
{
    private static R4LiveSlotSnapshot Make(long id, int revision, bool valid)
    {
        var main = valid ?
            new WeightedTargetSlot(new CombatSlotRef(10,"S1","HEAD",0),
                new HitTargetKey(10,"HEAD"),1,0,0,0) : null;
        var slots = new List<WeightedTargetSlot>();
        if(main!=null)slots.Add(main);
        var result=SlotWeightedTargetPlanner.Plan(id,revision,1,main,slots);
        return new R4LiveSlotSnapshot(id,revision,slots,main,new List<string>(),result);
    }

    [MenuItem("Game System Verification/R4 - Planning Snapshot Cache (Edit Mode)")]
    public static void Run()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Edit Mode only");
        int pass=0,fail=0;
        void Check(string id,bool good)
        {
            if(good){pass++;Debug.Log("[R4-Cache] PASS "+id);}
            else{fail++;Debug.LogError("[R4-Cache] FAIL "+id);}
        }
        var cache = new R4PlanningAllocationCache();
        int captured=0;
        var first=cache.GetOrCapture(101,()=>{captured++;return Make(101,3,true);});
        var again=cache.GetOrCapture(101,()=>{captured++;return Make(101,4,true);});
        Check("C01 snapshot retained without recompute",
            object.ReferenceEquals(first,again) && captured==1 && cache.Count==1);
        Check("C02 other allied aim does not reallocate",
            !cache.Invalidate(101,R4PlanMutationKind.OtherActionAimChanged) &&
            cache.TryGet(101,out var read) && object.ReferenceEquals(read,first));
        Check("C03 source target change invalidates",
            cache.Invalidate(101,R4PlanMutationKind.SourceTargetChanged) && cache.Count==0);
        var recalc=cache.GetOrCapture(101,()=>{captured++;return Make(101,5,true);});
        Check("C04 explicit recapture receives new planning revision",
            captured==2 && recalc.PlanningRevision==5);
        cache.Invalidate(101,R4PlanMutationKind.AttackWeightPolicyChanged);
        Check("C05 weight policy change invalidates",cache.Count==0);
        cache.GetOrCapture(101,()=>Make(101,7,true));
        cache.Invalidate(101,R4PlanMutationKind.SourceSkillChanged);
        Check("C06 own skill change invalidates",cache.Count==0);
        cache.GetOrCapture(101,()=>Make(101,8,true));
        cache.Invalidate(101,R4PlanMutationKind.ForcedTargetChanged);
        Check("C07 forced retarget invalidates",cache.Count==0);
        cache.GetOrCapture(101,()=>Make(101,9,true));
        cache.Invalidate(101,R4PlanMutationKind.ExplicitReplan);
        Check("C08 user explicit replan invalidates",cache.Count==0);
        var invalid=cache.GetOrCapture(102,()=>Make(102,1,false));
        Check("C09 invalid primary is never cached",invalid!=null && cache.Count==0);
        bool mismatchCaught=false;
        try{cache.GetOrCapture(103,()=>Make(999,2,true));}
        catch(InvalidOperationException){mismatchCaught=true;}
        Check("C10 wrong action ID rejected",mismatchCaught);
        cache.GetOrCapture(104,()=>Make(104,1,true));
        cache.GetOrCapture(105,()=>Make(105,1,true));
        Check("C11 different source actions remain isolated",cache.Count==2);
        cache.Invalidate(104,R4PlanMutationKind.SourceReassigned);
        Check("C12 single action reassignment leaves other untouched",
            cache.Count==1 && cache.TryGet(105,out var _));
        cache.Invalidate(105,R4PlanMutationKind.BattleEnded);
        Check("C13 battle-end clears all snapshots",cache.Count==0);
        Debug.Log("[R4-Cache] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R4 cache checks failed "+fail);
    }
}
#endif
