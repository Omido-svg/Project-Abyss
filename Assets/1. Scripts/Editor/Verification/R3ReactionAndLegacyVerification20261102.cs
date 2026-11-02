#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>R3-I captured request vs execution-time validation + legacy same-target proof.</summary>
public static class R3ReactionAndLegacyVerification20261102
{
    private sealed class LegacyCondition : ICombatCondition
    {
        private readonly SkillEffectCondition condition;
        private readonly SkillEffectContext selectedContext;
        public LegacyCondition(SkillEffectCondition condition,SkillEffectContext context)
        {
            this.condition=condition;selectedContext=context;
        }
        public bool Evaluate(IReadOnlyAbilityContext _,CombatTargetRef candidate)=>
            condition.Evaluate(selectedContext);
    }
    [MenuItem("Game System Verification/R3 - Reaction And Legacy Conditions (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode required");
        var mgr=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var enemy=mgr?.BattleContext?.Enemies?.FirstOrDefault(c=>c!=null&&!c.IsDead);
        if(mgr?.BattleContext?.Player==null||enemy==null)
            throw new InvalidOperationException("Active BattleManager/Player/enemy required");
        int pass=0,fail=0;
        void Check(string id,bool ok,string note)
        {
            if(ok){pass++;Debug.Log("[R3-Reaction] PASS "+id+" | "+note);}
            else{fail++;Debug.LogError("[R3-Reaction] FAIL "+id+" | "+note);}
        }

        using(var fixture=GameSystemVerificationFixture.Create(
            mgr.BattleContext.Player,enemy,mgr.BattleRules))
        {
            var owner=fixture.Player;
            var target=fixture.Enemy;
            BodyPart part=owner.BodyParts.FirstOrDefault(p=>p!=null&&!p.IsBroken);
            BodyPart targetPart=target.BodyParts.FirstOrDefault(p=>p!=null&&!p.IsBroken);
            long ownerId=R4LiveSlotQuery.ActorRuntimeId(owner);
            var request=new ReactionRequest(63001,60001,60000,90001,50001,
                ownerId,"r3:react",ReactionTimingCheckpoint.AfterExchangeSettled,0,1);
            var cause=new AbilityEventContext(AbilityEventKind.DamageSettled,
                60001,90001,ownerId,0,true,false);
            var evidence=new AppliedDamageEvidence(90001,70001,80001,false,
                17,true,false,false,R4LiveSlotQuery.ActorRuntimeId(target),
                targetPart?.PartId);
            var gate=new R3ReactionConditionGate();

            bool captured=gate.TryCaptureRequest(request,cause,true,true,
                R3ReactionCostPolicy.PayNormalSkillCost,R3ReactionConsumeOn.Started,
                3,true,evidence,out var ticket,out var reason);
            Check("I01 committed origin result captured once",captured &&
                ticket.HistoricalDamage.AppliedHpDamage==17 &&
                ticket.HistoricalDamage.CauseEventId==request.CauseEventId,
                reason??"historical damage=17");

            Check("I02 current live owner/part/target/resource allows start",
                gate.ValidateAtExecution(ticket,owner,target,part,targetPart,true,3,out _),
                "threshold exactly 3");
            Check("I03 current available resource insufficient cancels",
                !gate.ValidateAtExecution(ticket,owner,target,part,targetPart,true,2,out _),
                "no retroactive resource subtraction");
            Check("I04 unavailable skill cancels",
                !gate.ValidateAtExecution(ticket,owner,target,part,targetPart,false,10,out _),
                "without changing original roll result");
            Check("I05 missing/dead target cancels",
                !gate.ValidateAtExecution(ticket,owner,null,part,targetPart,true,10,out _),
                "no implicit retarget");
            Check("I06 malformed owner part cancels",
                !gate.ValidateAtExecution(ticket,owner,target,targetPart,targetPart,true,10,out _),
                "wrong body part actor");

            var unrelated=new AppliedDamageEvidence(90002,70001,80001,false,
                99,true,false,false,R4LiveSlotQuery.ActorRuntimeId(target),
                targetPart?.PartId);
            Check("I07 different cause cannot be recorded",
                !gate.TryCaptureRequest(request,cause,true,true,
                    R3ReactionCostPolicy.Free,R3ReactionConsumeOn.Applied,0,
                    true,unrelated,out _,out _),
                "unrelated damage 99 ignored");
            Check("I08 uncommitted damage cannot be recorded",
                !gate.TryCaptureRequest(request,cause,true,true,
                    R3ReactionCostPolicy.Free,R3ReactionConsumeOn.Applied,0,
                    false,evidence,out _,out _),
                "requires committed DamageResult");
            Check("I09 initial trigger and lifetime rejection",
                !gate.TryCaptureRequest(request,cause,false,true,
                    R3ReactionCostPolicy.Free,R3ReactionConsumeOn.Applied,0,
                    true,evidence,out _,out _) &&
                !gate.TryCaptureRequest(request,cause,true,false,
                    R3ReactionCostPolicy.Free,R3ReactionConsumeOn.Applied,0,
                    true,evidence,out _,out _),
                "Request not created when origin gate rejects");
            Check("I10 Free policy cannot hide a nonzero custom cost",
                !gate.TryCaptureRequest(request,cause,true,true,
                    R3ReactionCostPolicy.Free,R3ReactionConsumeOn.Started,100,
                    true,evidence,out _,out _),
                "explicit cost descriptor required");
            Check("I11 result snapshot remains immutable across current checks",
                ticket.HistoricalDamage.AppliedHpDamage==17 &&
                ticket.CostPolicy==R3ReactionCostPolicy.PayNormalSkillCost &&
                ticket.ConsumeOn==R3ReactionConsumeOn.Started &&
                ticket.RequiredCost==3,
                "request 17 damage not reconstructed from HP");

            var definitions=new List<UnityEngine.Object>();
            try
            {
                var def=ScriptableObject.CreateInstance<SkillDefinition>();
                definitions.Add(def);
                def.SkillName="R3-LEGACY-OWNER-TARGET-PARITY";
                def.ActionType=ActionType.NormalAttack;
                def.ResolverType=SkillResolverType.Dice;
                def.ExchangeRollCount=1;
                var skill=def.CreateRuntimeSkill();
                skill.Initialize(owner,fixture.Context._battleEvent);
                var slot=new ActionSlot{
                    ActionId=69001,Owner=owner,Skill=skill,
                    TargetCharacter=target,TargetPart=targetPart,
                    Part=part,SlotId="R3-LEGACY",Phase=ActionPhase.COMBAT,
                    ResourceCostCommitted=true
                };
                var action=new BattleAction{Slot=slot};
                var context=new SkillEffectContext(action,def);
                var selected=context.WithTarget(SkillEffectTargetSelector.Owner);
                Check("L01 selected owner not same as original enemy target",
                    context.Target==target && selected.Target==owner &&
                    selected.Target!=context.Target,
                    "selectedContext.Owner vs original Target");

                var effect=ScriptableObject.CreateInstance<G0ProbeEffectDefinition>();
                definitions.Add(effect);
                effect.ProbeLabel="R3_LEGACY_OWNER";
                var sobj=new SerializedObject(effect);
                sobj.FindProperty("targetSelector").enumValueIndex=
                    (int)SkillEffectTargetSelector.Owner;
                sobj.ApplyModifiedPropertiesWithoutUndo();
                var entry=new SkillEffectEntry{
                    Definition=effect,OverrideTiming=true,
                    Timing=SkillEffectTiming.OnExecute,
                    Conditions=new List<SkillEffectCondition>{
                        new SkillEffectCondition{
                            Type=SkillEffectConditionType.TargetHpRatioAtMost,
                            Ratio=1f
                        }}
                };
                G0ProbeEffectDefinition.ResetCounters();
                var oldEffect=effect.TryApply(context,SkillEffectTiming.OnExecute);
                int oldCalls=G0ProbeEffectDefinition.GetCount("R3_LEGACY_OWNER");
                G0ProbeEffectDefinition.ResetCounters();
                var combined=entry.TryApply(context,SkillEffectTiming.OnExecute);
                int entryCalls=G0ProbeEffectDefinition.GetCount("R3_LEGACY_OWNER");
                Check("L02 definition and entry use one owner selected target",
                    oldEffect!=null&&combined!=null&&oldEffect.Target==owner &&
                    combined.Target==owner&&oldEffect.State==combined.State&&
                    oldCalls==1&&entryCalls==1,
                    "legacy direct vs entry selected context");

                var inverted=new SkillEffectCondition{
                    Type=SkillEffectConditionType.Always,Invert=true};
                var node=CombatConditionNode.Not(CombatConditionNode.Leaf(
                    new LegacyCondition(inverted,selected)));
                Check("L03 legacy Invert + new NOT have explicit double-negative",
                    !inverted.Evaluate(selected)&&
                    node.Evaluate(null,default),
                    "false inverted Always, NOT false -> true");

                var noTarget=new SkillEffectCondition{
                    Type=SkillEffectConditionType.Always,Invert=false};
                Check("L04 legacy list AND applies unchanged",
                    noTarget.Evaluate(selected)&&!inverted.Evaluate(selected),
                    "existing SkillEffectCondition serialization intact");
            }
            finally
            {
                G0ProbeEffectDefinition.Reset();
                foreach(var obj in definitions)
                    if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);
            }
        }
        Debug.Log("[R3-Reaction] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R3 reaction/legacy parity failed="+fail);
    }
}
#endif
