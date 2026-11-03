#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>R2-I live battle-action identity and legacy single-path parity.</summary>
public static class R2TriggeredActionParityVerification20261102
{
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required.");
        var manager=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var enemy=manager?.BattleContext?.Enemies?.FirstOrDefault(e=>e!=null&&!e.IsDead);
        if(manager?.BattleContext?.Player==null || enemy==null)
            throw new InvalidOperationException("BattleManager/Player/enemy not initialized.");
        int pass=0,fail=0;
        void Check(string id,bool ok,string note)
        {
            if(ok){pass++;Debug.Log("[R2-I] PASS "+id+" "+note);}
            else{fail++;Debug.LogError("[R2-I] FAIL "+id+" "+note);}
        }
        var created=new List<UnityEngine.Object>();
        using(var fixture=GameSystemVerificationFixture.Create(
            manager.BattleContext.Player,enemy,manager.BattleRules))
        {
            try
            {
                var owner=fixture.Player;
                var target=fixture.Enemy;
                long ownerId=R4LiveSlotQuery.ActorRuntimeId(owner);
                var def=ScriptableObject.CreateInstance<SkillDefinition>();
                created.Add(def);
                def.name="R2_REACTION_TEST_ONLY";
                var serialized = new SerializedObject(def);
                serialized.FindProperty("skillId").stringValue = "r2.reaction.sample";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                def.SkillName="R2 reaction probe";
                def.ActionType=ActionType.NormalAttack;
                def.Color=SkillColor.Red;
                def.ResolverType=SkillResolverType.Dice;
                def.ExchangeRollCount=1;
                def.Rolls=new List<SkillRollData>{
                    new SkillRollData{
                        Index=0,Type=CombatRollType.Attack,
                        DiceMode=DicePowerMode.AbsoluteRange,MinPower=1,MaxPower=1
                    }};
                var requestA=new ReactionRequest(
                    5001,9001,9000,7001,8800,ownerId,def.SkillId,
                    ReactionTimingCheckpoint.AfterExchangeSettled,0,1);
                var requestB=new ReactionRequest(
                    5002,9001,9000,7001,8800,ownerId,def.SkillId,
                    ReactionTimingCheckpoint.AfterExchangeSettled,0,2);
                var factory=new R2TriggeredActionFactory();
                BodyPart ownPart=owner.BodyParts?.FirstOrDefault(p=>p!=null&&!p.IsBroken);
                BodyPart targetPart=target.BodyParts?.FirstOrDefault(p=>p!=null&&!p.IsBroken);
                bool createdA=factory.TryCreate(requestA,9101,owner,target,def,
                    ownPart,targetPart,out var a,out var errA);
                bool createdB=factory.TryCreate(requestB,9102,owner,target,def,
                    ownPart,targetPart,out var b,out var errB);
                Check("I01 two requests create distinct live BattleActions",
                    createdA&&createdB&&a!=null&&b!=null&&a.Action!=b.Action &&
                    a.ActionId!=b.ActionId && a.Action.ActionId==9101 && b.Action.ActionId==9102,
                    "A="+errA+" B="+errB);
                Check("I02 common parent/root/cause/chain remain immutable",
                    createdA&&createdB&&a.ParentActionId==b.ParentActionId&&
                    a.RootActionId==b.RootActionId&&a.CauseEventId==b.CauseEventId&&
                    a.ChainId==b.ChainId&&a.RequestId!=b.RequestId,
                    "same origin; request identity distinct");
                Check("I03 shared SkillDefinition, separate runtime Skill and AbilityInstance",
                    createdA&&createdB&&a.Action.Skill!=b.Action.Skill&&
                    a.Ability!=b.Ability&&
                    a.Action.Skill.Definition==def&&b.Action.Skill.Definition==def&&
                    a.Ability.Definition!=b.Ability.Definition&&
                    !a.Ability.Identity.Equals(b.Ability.Identity),
                    "no mutable state shared between executions");
                var activation=new ActivationKey(9101,AbilityEventKind.RollUsing,0,7001);
                a.Ability.RuntimeState.Commit(activation,
                    new AbilityExecutionResult(1,1,null));
                Check("I04 activation history isolated per reaction",
                    !a.Ability.RuntimeState.CanActivate(activation)&&
                    b.Ability.RuntimeState.CanActivate(activation)&&
                    b.Ability.RuntimeState.SuccessfulActivationCount==0,
                    "distinct ability memory");
                Check("I05 newly created action never forges planning cost",
                    !a.Action.Slot.ResourceCostCommitted &&
                    !b.Action.Slot.ResourceCostCommitted,
                    "scheduler/commit stage still owned by R6");
                Check("I06 duplicate action ID rejected",
                    !factory.TryCreate(requestB,9101,owner,target,def,ownPart,targetPart,
                        out _,out _),
                    "no reuse of committed IDs");
                Check("I07 child action cannot masquerade as parent/root",
                    !factory.TryCreate(requestA,9001,owner,target,def,ownPart,targetPart,
                        out _,out _) &&
                    !factory.TryCreate(requestA,9000,owner,target,def,ownPart,targetPart,
                        out _,out _),
                    "lineage invariant");
                var mismatchedOwner=new ReactionRequest(
                    5003,9001,9000,7001,8800,ownerId+99999,def.SkillId,
                    ReactionTimingCheckpoint.AfterExchangeSettled,0,3);
                Check("I08 wrong requesting actor rejected before execution",
                    !factory.TryCreate(mismatchedOwner,9103,owner,target,def,
                        ownPart,targetPart,out _,out _),
                    "no actor impersonation");
                var wrongSkill=new ReactionRequest(
                    5004,9001,9000,7001,8800,ownerId,"incorrect.skill",
                    ReactionTimingCheckpoint.AfterExchangeSettled,0,4);
                Check("I09 skill identity mismatch rejected",
                    !factory.TryCreate(wrongSkill,9104,owner,target,def,
                        ownPart,targetPart,out _,out _),"no arbitrary skill substitution");
var unsupported=new ReactionRequest(
                    5005,9001,9000,7001,8800,ownerId,def.SkillId,
                    ReactionTimingCheckpoint.AfterDamageResultCommitted,0,5);
                Check("I13 partial damage checkpoint is not executable at R2",
                    !factory.TryCreate(unsupported,9105,owner,target,def,
                        ownPart,targetPart,out _,out var policyProblem) &&
                    !string.IsNullOrEmpty(policyProblem),
                    "checkpoint remains R5/R6 policy, not card trigger");
                                Check("I10 factory has NOT dealt damage, scheduled or emitted events",
                    factory.CreatedCount==2 &&
                    a.Action.HasRolled==false&&b.Action.HasRolled==false,
                    "draft only; no double execution");
                factory.ClearForBattleEnd();
                Check("I11 factory battle cleanup releases identity allocations",
                    factory.CreatedCount==0,"lifetime reset");

                // Existing legacy compatibility adapter is NOT installed into
                // dispatcher simultaneously. Compare exactly ONE effect call
                // through each path in isolation, not two calls in one action.
                var effect=ScriptableObject.CreateInstance<G0ProbeEffectDefinition>();
                created.Add(effect);
                effect.ProbeLabel="R2_LEGACY_PARITY";
                var entry=new SkillEffectEntry{
                    Definition=effect,OverrideTiming=true,
                    Timing=SkillEffectTiming.OnExecute,
                    Conditions=new List<SkillEffectCondition>()
                };
                var context=new SkillEffectContext(a.Action,def);
                G0ProbeEffectDefinition.ResetCounters();
                var oldResult=entry.TryApply(context,SkillEffectTiming.OnExecute);
                int oldCalls=G0ProbeEffectDefinition.GetCount("R2_LEGACY_PARITY");
                G0ProbeEffectDefinition.ResetCounters();
                var adapter=new LegacySkillEffectEntryAdapter(entry);
                var compatible=adapter.Execute(context,SkillEffectTiming.OnExecute);
                int compatCalls=G0ProbeEffectDefinition.GetCount("R2_LEGACY_PARITY");
                Check("I12 isolated legacy effect execution parity",
                    oldResult!=null&&compatible!=null&&oldResult.State==compatible.State&&
                    oldResult.Timing==compatible.Timing&&oldCalls==1&&compatCalls==1,
                    "old/new state="+oldResult?.State+"/"+compatible?.State+
                    " effect calls="+oldCalls+"/"+compatCalls);
            }
            finally
            {
                G0ProbeEffectDefinition.Reset();
                foreach(var obj in created)
                    if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);
            }
        }
        Debug.Log("[R2-I] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R2-I verification failed="+fail);
    }
}
#endif
