#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// Real BattleContext R4-W preview -> frozen plan -> actual R5 opt-in selection.
/// Virtual skills only, no SO authoring and no DamageManager writes.
/// </summary>
public static class R4PlanningPreviewParityVerification20261102
{
    private sealed class WeightedProbe : NormalSkill
    {
        private readonly int weight;
        private readonly AttackWeightTargetingModel mode;
        public WeightedProbe(int weight,AttackWeightTargetingModel mode)
        {
            this.weight=weight;this.mode=mode;
            SkillName="R4-W-VIRTUAL-NO-ASSET";
            BasePower=1;
            Resolver=new DiceResolver(1,1);
        }
        public override int AttackWeight=>weight;
        public override AttackWeightTargetingModel AttackWeightTargetingModel=>mode;
        public override void Execute(BattleAction a){}
    }

    private sealed class ExcludeActor : IR4TargetPlanningRule
    {
        private readonly long id;
        public ExcludeActor(long id){this.id=id;}
        public bool Allows(in R4TargetPlanningContext ctx,WeightedTargetSlot target)=>
            target.Slot.ActorRuntimeId!=id;
    }

    
    private static string Key(HitTargetKey t)=>t.ActorRuntimeId+":"+t.PartId;
    private static string Key(AttackWeightTarget t)=>
        R4LiveSlotQuery.ActorRuntimeId(t.TargetCharacter)+":"+
        (t.TargetPart?.PartId??string.Empty);
    private static string Key(R4WeightPlanningPreview p)=>
        string.Join("|",p.DistinctHitTargets.Select(Key));

    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode required.");
        var mgr=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var battle=mgr?.BattleContext;
        if(mgr?.ClashBuilder==null||battle?.Player==null)
            throw new InvalidOperationException("BattleContext, ClashBuilder and player required.");
        var normal=battle.Enemies?.FirstOrDefault(e=>e!=null&&!e.IsDead&&!e.UsesBodyParts);
        var another=battle.Enemies?.SkipWhile(e=>e!=normal).Skip(1)
            .FirstOrDefault(e=>e!=null&&!e.IsDead&&!e.UsesBodyParts);
        if(another==null)another=battle.Enemies?.FirstOrDefault(e=>e!=null &&
            !e.IsDead&&!e.UsesBodyParts&&e!=normal);
        if(normal==null||another==null)
            throw new InvalidOperationException("Two living partless enemies required.");
        int pass=0,fail=0;
        void Check(string id,bool value,string details="")
        {
            if(value){pass++;Debug.Log("[R4-Preview] PASS "+id+" | "+details);}
            else{fail++;Debug.LogError("[R4-Preview] FAIL "+id+" | "+details);}
        }

        var skill=new WeightedProbe(3,AttackWeightTargetingModel.SlotWeighted);
        var source=new ActionSlot{
            ActionId=978001,Owner=battle.Player,
            TargetCharacter=normal,TargetPart=null,
            Skill=skill,SlotId="R4-PLAYER-SLOT",Phase=ActionPhase.COMBAT
        };
        var plans=new List<ActionSlot>{source};
        var before=JsonUtility.ToJson(UnityEngine.Random.state);
        var priorTarget=source.TargetCharacter;
        var priorTargetSlot=source.TargetSlot;
        var pairBefore=mgr.ClashBuilder.BuildClashPreview(plans).ToArray();
        var preview=mgr.ClashBuilder.BuildAttackWeightPreview(battle,source,plans);
        Check("P01 real battle preview resolves explicit slot-weight skill",
            preview!=null&&preview.Success&&preview.ActionId==source.ActionId,
            preview?.Diagnostic);
        Check("P02 primary planned physical hit is originally aimed enemy",
            preview!=null&&preview.DistinctHitTargets.Count>=1&&
            preview.DistinctHitTargets[0].ActorRuntimeId==
                R4LiveSlotQuery.ActorRuntimeId(normal),
            preview==null?"null":Key(preview));
        Check("P03 weighted budget picks distinct secondary physical targets",
            preview!=null&&preview.DistinctHitTargets.Count==3&&
            preview.SelectedSlots.Count>=3&&
            preview.SelectedSlots[0].IsPrimary,
            preview==null?"null":"hits="+preview.DistinctHitTargets.Count+
                " allocations="+preview.SelectedSlots.Count);
        Check("P04 preview is non-mutating and no fake ActionSlots",
            plans.Count==1&&source.TargetCharacter==priorTarget&&
            source.TargetSlot==priorTargetSlot&&source.Skill==skill,
            "ActionSlot untouched");
        Check("P05 preview does not consume Unity RNG",
            before==JsonUtility.ToJson(UnityEngine.Random.state));
        var pairAfter=mgr.ClashBuilder.BuildClashPreview(plans);
        Check("P06 preview never adds secondary as a clash pair",
            pairBefore.Length==pairAfter.Count&&plans.Count==1,
            "pair count="+pairBefore.Length);

        var action=new BattleAction{Slot=source};
        bool frozen=R4BattlePlanningSession.TryGetFrozen(battle,action,out var locked);
        Check("P07 frozen snapshot published for execution",
            frozen&&locked.Selection.Allocation.ActionId==source.ActionId &&
            locked.Selection.Allocation.PlanningRevision==preview.PlanningRevision);
        bool exec=R5SlotWeightedTargetBridge.TryResolve(battle,action,
            out var actual,out string reason);
        Check("P08 actual R5 opt-in targets match preview in exact order",
            exec&&preview!=null&&actual.Count==preview.DistinctHitTargets.Count &&
            string.Join("|",actual.Select(Key))==Key(preview),
            "preview="+(preview==null?"null":Key(preview))+
            " actual="+string.Join("|",actual.Select(Key))+" "+reason);
        Check("P09 R5 uses existing Character/BodyPart, no synthetic actor",
            actual.All(t=>t.TargetCharacter!=null&&battle.Enemies.Contains(t.TargetCharacter)),
            "all hits target real enemies");

        var other=new ActionSlot{
            ActionId=978002,Owner=battle.Player,
            TargetCharacter=another,Skill=new WeightedProbe(1,AttackWeightTargetingModel.LegacyCharacterRandom),
            Phase=ActionPhase.COMBAT,SlotId="R4-OTHER-SLOT"
        };
        plans.Add(other);
        var kept=mgr.ClashBuilder.BuildAttackWeightPreview(battle,source,plans);
        R4BattlePlanningSession.NotifyMutation(battle,source.ActionId,
            R4PlanMutationKind.OtherActionAimChanged);
        other.TargetCharacter=normal;
        var keptAgain=mgr.ClashBuilder.BuildAttackWeightPreview(battle,source,plans);
        Check("P10 unrelated allied aim change keeps frozen target allocation",
            kept!=null&&keptAgain!=null&&
            kept.PlanningRevision==preview.PlanningRevision &&
            keptAgain.PlanningRevision==preview.PlanningRevision &&
            Key(keptAgain)==Key(preview),
            "previous revision="+preview.PlanningRevision+
            " later="+keptAgain?.PlanningRevision);

        source.TargetCharacter=another;
        var newPreview=mgr.ClashBuilder.BuildAttackWeightPreview(battle,source,plans);
        Check("P11 source retarget invalidates and recaptures primary",
            newPreview!=null&&newPreview.Success&&
            newPreview.PlanningRevision>preview.PlanningRevision&&
            newPreview.DistinctHitTargets[0].ActorRuntimeId==
                R4LiveSlotQuery.ActorRuntimeId(another),
            "new primary target actor="+R4LiveSlotQuery.ActorRuntimeId(another));
        var updatedAction=new BattleAction{Slot=source};
        Check("P12 new source preview is frozen and old main not reused",
            R4BattlePlanningSession.TryGetFrozen(battle,updatedAction,out var renewed) &&
            renewed.Primary.HitTarget.ActorRuntimeId==R4LiveSlotQuery.ActorRuntimeId(another),
            "changed source target");

        source.Skill=new WeightedProbe(2,AttackWeightTargetingModel.SlotWeighted);
        var newSkillPreview=mgr.ClashBuilder.BuildAttackWeightPreview(battle,source,plans);
        Check("P13 own skill change invalidates previous budget",
            newSkillPreview!=null&&newSkillPreview.Success&&
            newSkillPreview.PlanningRevision>newPreview.PlanningRevision &&
            newSkillPreview.EffectiveWeight==2,
            "weight="+newSkillPreview?.EffectiveWeight);
        R4BattlePlanningSession.NotifyMutation(battle,source.ActionId,
            R4PlanMutationKind.ExplicitReplan);
        Check("P14 explicit replan drops current frozen selection",
            !R4BattlePlanningSession.TryGetFrozen(battle,
                new BattleAction{Slot=source},out _),
            "explicit replan notification");
        var recaptured=mgr.ClashBuilder.BuildAttackWeightPreview(battle,source,plans);
        Check("P15 explicit replan captures new revision",
            recaptured!=null&&recaptured.PlanningRevision>newSkillPreview.PlanningRevision);
        source.Skill=new WeightedProbe(3,AttackWeightTargetingModel.LegacyCharacterRandom);
        Check("P16 default/legacy targeting never activates dotted preview",
            mgr.ClashBuilder.BuildAttackWeightPreview(battle,source,plans)==null);
        R4BattlePlanningSession.Clear(battle);
        Check("P17 battle cleanup drops action freeze",
            !R4BattlePlanningSession.TryGetFrozen(battle,updatedAction,out _));
        Check("P18 query remained RNG-free throughout",
            before==JsonUtility.ToJson(UnityEngine.Random.state));
        // R4-K policy registration invalidates this battle's allocation,
        // and the UI preview uses the same eligibility filter as execution.
        source.Skill=new WeightedProbe(3,AttackWeightTargetingModel.SlotWeighted);
        source.TargetCharacter=another;
        long forbiddenId=R4LiveSlotQuery.ActorRuntimeId(normal);
        R4BattlePlanningSession.RegisterTargetRule(battle,new ExcludeActor(forbiddenId));
        var policyPreview=mgr.ClashBuilder.BuildAttackWeightPreview(battle,source,plans);
        Check("P19 registered target visibility applies before budgeting",
            policyPreview!=null&&policyPreview.Success &&
            policyPreview.DistinctHitTargets.All(h=>h.ActorRuntimeId!=forbiddenId),
            "normal actor excluded");
        var policyAction=new BattleAction{Slot=source};
        bool policySelected=R5SlotWeightedTargetBridge.TryResolve(battle,policyAction,
            out var policyHits,out _);
        Check("P20 execution reuses filtered preview targets",
            policySelected && policyPreview!=null&&
            string.Join("|",policyHits.Select(Key))==Key(policyPreview),
            "no stale unfiltered execution");
        R4BattlePlanningSession.RegisterTargetRule(battle,
            new ExcludeActor(R4LiveSlotQuery.ActorRuntimeId(another)));
        var deniedMain=mgr.ClashBuilder.BuildAttackWeightPreview(battle,source,plans);
        Check("P21 policy cannot silently bypass invalid primary",
            deniedMain!=null&&!deniedMain.Success,
            "forced target missing => no allocation");
        R4BattlePlanningSession.RegisterTargetRule(battle,null);
        var restored=mgr.ClashBuilder.BuildAttackWeightPreview(battle,source,plans);
        Check("P22 policy removal restores deterministic preview",
            restored!=null&&restored.Success&&
            restored.DistinctHitTargets[0].ActorRuntimeId==
                R4LiveSlotQuery.ActorRuntimeId(another));
        R4BattlePlanningSession.Clear(battle);
        
        Debug.Log("[R4-Preview] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R4 preview parity failed "+fail);
    }
}
#endif
