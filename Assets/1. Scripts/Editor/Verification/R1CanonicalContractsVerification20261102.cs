#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R1 contract gate: editor-only verification of canonical card triggers,
/// exchange win/loss provenance, checkpoint vs event separation, and
/// selected-slot semantics. Does not change authored assets.
/// </summary>
public static class R1CanonicalContractsVerification20261102
{
    [Serializable] private sealed class Result
    {
        public string GeneratedUtc;
        public int Pass,Fail;
        public string[] Failures;
    }

    [MenuItem("Game System Verification/R1 - Canonical I W Contracts (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("R1 contract gate requires Edit Mode.");
        int pass=0;var errors=new List<string>();
        void Check(string id,bool ok,string details)
        {
            if(ok){pass++;Debug.Log("[R1-Contracts] PASS "+id+" | "+details);}
            else{errors.Add(id+" "+details);Debug.LogError("[R1-Contracts] FAIL "+id+" | "+details);}
        }

        var canonical=new[]{
            SkillEffectTiming.OnAttackResolutionStarting,
            SkillEffectTiming.OnExecute,SkillEffectTiming.OnExchangeWin,
            SkillEffectTiming.OnExchangeLose,SkillEffectTiming.OnClashEnd,
            SkillEffectTiming.OnPreparationActivated
        };
        Check("C01 exactly six canonical card triggers",
            canonical.SequenceEqual(SkillEffectTimingCatalog.AuthoringTimings) &&
            canonical.All(R1CanonicalEventPolicy.IsCanonicalCardTrigger),
            "6 expected; no event checkpoint in authoring");

        Check("C02 removed matched authoring, legacy enum numbers stable",
            (int)SkillEffectTiming.OnDuelMatched==31 &&
            (int)SkillEffectTiming.OnBattleStart==16 &&
            (int)SkillEffectTiming.OnAttackResolutionStarting==35 &&
            (int)SkillEffectTiming.OnPreparationActivated==36 &&
            !R1CanonicalEventPolicy.IsCanonicalCardTrigger(SkillEffectTiming.OnDuelMatched) &&
            !R1CanonicalEventPolicy.IsCanonicalCardTrigger(SkillEffectTiming.AfterDamage),
            "31,16,35,36 compatibility");

        Check("C03 global start does not become attack start",
            SkillEffectTiming.OnBattleStart!=SkillEffectTiming.OnAttackResolutionStarting &&
            !SkillEffectTimingCatalog.IsAuthoringTiming(SkillEffectTiming.OnBattleStart),
            "global OnBattleStart is distinct");

        var first=ScriptableObject.CreateInstance<SkillDefinition>();
        var second=ScriptableObject.CreateInstance<SkillDefinition>();
        try
        {
            first.ActionType=ActionType.Duel;
            second.ActionType=ActionType.Duel;
            var duel=new BattleAction{Slot=new ActionSlot{ActionId=60101,Skill=first.CreateRuntimeSkill()}};
            var duelOpponent=new BattleAction{Slot=new ActionSlot{ActionId=60102,Skill=second.CreateRuntimeSkill()}};
            duel.SetDuelEffectsEligible(true);
            duelOpponent.SetDuelEffectsEligible(true);
            Check("C04 matched duel can win/loss",
                R1CanonicalEventPolicy.AllowsExchangeOutcome(duel,duelOpponent,true,false),
                "dual eligible original duel");

            Check("C05 non-opposed call cannot fabricate victory",
                !R1CanonicalEventPolicy.AllowsExchangeOutcome(duel,duelOpponent,false,false) &&
                !R1CanonicalEventPolicy.AllowsExchangeOutcome(duel,null,true,false),
                "isClash false and opponent null");

            Check("C06 leftover one-sided roll cannot win/loss",
                !R1CanonicalEventPolicy.AllowsExchangeOutcome(duel,duelOpponent,true,true),
                "original duel eligibility remains true");

            duel.SetDuelEffectsEligible(false);
            Check("C07 unqualified duel is fully gated",
                !R1CanonicalEventPolicy.AllowsExchangeOutcome(duel,duelOpponent,true,false),
                "duel eligibility false");
            duel.SetDuelEffectsEligible(true);
            second.ActionType=ActionType.NormalAttack;
            duelOpponent=new BattleAction{Slot=new ActionSlot{ActionId=60103,Skill=second.CreateRuntimeSkill()}};
            Check("C08 duel vs normal cannot publish duel outcome",
                !R1CanonicalEventPolicy.AllowsExchangeOutcome(duel,duelOpponent,true,false),
                "opponent NormalAttack");
            Check("C09 ordinary opposed normal attack is eligible",
                R1CanonicalEventPolicy.AllowsExchangeOutcome(duelOpponent,duel,true,false),
                "normal paired exchange; no duel card gate");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }

        var checkpoints=(ReactionTimingCheckpoint[])Enum.GetValues(typeof(ReactionTimingCheckpoint));
        Check("C10 reaction checkpoints are post-commit not card triggers",
            checkpoints.Length==4 &&
            checkpoints.All(R1CanonicalEventPolicy.IsPostCommitCheckpoint) &&
            checkpoints.Contains(ReactionTimingCheckpoint.AfterExchangeSettled) &&
            checkpoints.Contains(ReactionTimingCheckpoint.AfterClashCompleted) &&
            checkpoints.Contains(ReactionTimingCheckpoint.AfterActionCompleted) &&
            checkpoints.Contains(ReactionTimingCheckpoint.AfterDamageResultCommitted),
            "4 independent scheduler events");

        var queue=new BattleReactionScheduler();
        var allowed=new ReactionRequest(6101,5101,5101,7101,5101,111,
            "r1-check",ReactionTimingCheckpoint.AfterExchangeSettled,0,1);
        var deferred=new ReactionRequest(6102,5101,5101,7102,5101,111,
            "r1-check",ReactionTimingCheckpoint.AfterDamageResultCommitted,0,2);
        var accepted=queue.Queue(allowed);
        var rejected=queue.Queue(deferred);
        bool dequeued=queue.TryDequeue(ReactionTimingCheckpoint.AfterExchangeSettled,out var actual);
        Check("C11 checkpoint intent only; unsupported execution stays rejected",
            accepted.Accepted && !rejected.Accepted && dequeued && actual.RequestId==6101,
            "AfterDamageResultCommitted declared but not enabled in queue");

        var m=new WeightedTargetSlot(
            new CombatSlotRef(1,"main","arm",0),new HitTargetKey(1,"arm"),2,0,0,0);
        var s=new WeightedTargetSlot(
            new CombatSlotRef(2,"sub-A","",0),new HitTargetKey(2),2,0,0,1);
        var duplicate=new WeightedTargetSlot(
            new CombatSlotRef(2,"sub-B","",0),new HitTargetKey(2),1,1,0,2);
        var allocation=SlotWeightedTargetPlanner.Plan(65501,1,3,m,new[]{s,duplicate});
        Check("C12 budget 3 consumes cost 2 + partial 1",
            allocation.Success && allocation.Allocation.SelectedSlots.Count==2 &&
            allocation.Allocation.SelectedSlots[0].ActuallyConsumedWeight==2 &&
            allocation.Allocation.SelectedSlots[1].ActuallyConsumedWeight==1 &&
            allocation.UnspentBudget==0,
            "partial final slot selection");
        var dedup=SlotWeightedTargetPlanner.Plan(65502,1,5,m,new[]{s,duplicate});
        Check("C13 multiple selected slots, one physical hit for normal enemy",
            dedup.Success && dedup.Allocation.SelectedSlots.Count==3 &&
            dedup.Allocation.DistinctHitTargets.Count==2 &&
            dedup.Allocation.DistinctHitTargets[0].Equals(new HitTargetKey(1,"arm")),
            "slot budget consumed before per-hit dedup");

        var sameActorOtherPart=new WeightedTargetSlot(
            new CombatSlotRef(1,"sub-other","leg",0),new HitTargetKey(1,"leg"),1,0,0,1);
        var separate=SlotWeightedTargetPlanner.Plan(65503,1,3,m,new[]{sameActorOtherPart});
        Check("C14 distinct boss parts remain independent hit targets",
            separate.Success && separate.Allocation.DistinctHitTargets.Count==2,
            "Actor#1 arm and leg");

        var legacy=new AttackWeightSettings();
        Check("C15 old authored attack weight remains opt-in",
            legacy.TargetingModel==AttackWeightTargetingModel.LegacyCharacterRandom &&
            legacy.EffectiveWeight==1,
            "no forced migration, no asset writes");

        string dest=Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "R1_Trigger_Contract_20261102");
        Directory.CreateDirectory(dest);
        var report=new Result{GeneratedUtc=DateTime.UtcNow.ToString("o"),
            Pass=pass,Fail=errors.Count,Failures=errors.ToArray()};
        File.WriteAllText(Path.Combine(dest,"R1_CONTRACT_GATE.json"),JsonUtility.ToJson(report,true));
        Debug.Log("[R1-Contracts] RESULT PASS="+pass+" FAIL="+errors.Count);
        if(errors.Count>0)throw new InvalidOperationException("R1 contract failed: "+string.Join(" | ",errors));
    }
}
#endif
