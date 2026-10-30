#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R3ConditionVerification20261030
{
    private sealed class Context : IReadOnlyAbilityContext
    {
        public AbilityEventContext Event { get; }
        public AbilityIdentity Ability { get; }
        public Context(AbilityEventContext e)
        {
            Event=e;
            Ability=new AbilityIdentity("R3-probe",e.OwnerActorRuntimeId,"SLOT",0);
        }
    }

    private sealed class DamageContextSnapshot : IReadOnlyDamageAbilityContext
    {
        public AbilityEventContext Event { get; }
        public AbilityIdentity Ability { get; }
        public bool HasCommittedDamageResult { get; }
        public AppliedDamageEvidence DamageEvidence { get; }
        public DamageContextSnapshot(AbilityEventContext e, bool committed, AppliedDamageEvidence d)
        {
            Event=e; Ability=new AbilityIdentity("R3-probe",e.OwnerActorRuntimeId,"SLOT",0);
            HasCommittedDamageResult=committed;
            DamageEvidence=d;
        }
    }

    private sealed class CountingCondition : ICombatCondition
    {
        public int Calls { get; private set; }
        private readonly bool outcome;
        public CountingCondition(bool v){outcome=v;}
        public bool Evaluate(IReadOnlyAbilityContext ctx, CombatTargetRef target)
        {Calls++;return outcome;}
    }

    private sealed class Matcher : IAbilityTriggerMatcher
    {
        public bool Matches(in AbilityEventContext context) => context.Kind == AbilityEventKind.RollUsing;
    }

    private sealed class Selector : ICombatTargetSelector
    {
        public int Calls;
        public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext c)
        {
            Calls++;
            return new[] { new CombatTargetRef(201,"HEAD","S1"), new CombatTargetRef(202,"ARM","S2") };
        }
    }

    private sealed class Operation : IEffectOperation
    {
        public void Plan(IReadOnlyAbilityContext c, IReadOnlyList<CombatTargetRef> targets, ICombatCommandSink sink)
        {
            foreach(var target in targets)
                sink.Enqueue(new CombatCommandIntent("TEST_ONLY_NO_APPLY",target,1));
        }
    }

    [MenuItem("Game System Verification/R3 - Condition Tree & Target Binding (Edit Mode)")]
    public static void RunFromMenu()
    {
        if(Application.isPlaying)
            throw new InvalidOperationException("R3 contract verification requires Edit Mode.");

        int pass=0, fail=0;
        void Check(string label, bool ok)
        {
            if(ok){pass++; Debug.Log("[R3] PASS "+label);}
            else {fail++; Debug.LogError("[R3] FAIL "+label);}
        }

        var e=new AbilityEventContext(AbilityEventKind.RollUsing,42,55,101,0,true,false);
        var ctx=new Context(e);
        var target=new CombatTargetRef(201,"HEAD","S1");
        var owner=CombatConditionNode.Leaf(new CombatActorIdCondition(101,true));
        var selected=CombatConditionNode.Leaf(new CombatActorIdCondition(201,false));
        var other=CombatConditionNode.Leaf(new CombatActorIdCondition(202,false));

        Check("R3-C01 nested AND/OR/NOT", CombatConditionEvaluator.Evaluate(
            CombatConditionNode.All(owner,CombatConditionNode.Any(other,
                CombatConditionNode.Not(CombatConditionNode.Not(selected)))),ctx,target));
        Check("R3-C02 empty ALL true", CombatConditionEvaluator.Evaluate(CombatConditionNode.All(),ctx,target));
        Check("R3-C03 empty ANY false", !CombatConditionEvaluator.Evaluate(CombatConditionNode.Any(),ctx,target));
        Check("R3-C04 null tree means no condition", CombatConditionEvaluator.Evaluate(null,ctx,target));
        Check("R3-C05 owner and candidate not mixed",
            owner.Evaluate(ctx,new CombatTargetRef(202,"ARM","S2")) &&
            selected.Evaluate(ctx,target) &&
            !selected.Evaluate(ctx,new CombatTargetRef(202,"ARM","S2")));
        Check("R3-C06 no candidate passes actor-ID target condition",
            !selected.Evaluate(ctx,new CombatTargetRef(0,null,null)));

        var skip=new CountingCondition(false);var after=new CountingCondition(true);
        var shouldNotCall=CombatConditionNode.All(CombatConditionNode.Leaf(skip),CombatConditionNode.Leaf(after));
        Check("R3-C07 pure AND short circuit",!shouldNotCall.Evaluate(ctx,target)&&skip.Calls==1&&after.Calls==0);
        var start=new CountingCondition(true);var notCalled=new CountingCondition(false);
        var or=CombatConditionNode.Any(CombatConditionNode.Leaf(start),CombatConditionNode.Leaf(notCalled));
        Check("R3-C08 pure OR short circuit",or.Evaluate(ctx,target)&&start.Calls==1&&notCalled.Calls==0);

        var listAdapter=new CombatConditionListAdapter(new ICombatCondition[]{new CombatActorIdCondition(101,true),new CombatActorIdCondition(201,false)});
        Check("R3-C09 legacy AND adapter",listAdapter.Evaluate(ctx,target)&&!listAdapter.Evaluate(ctx,new CombatTargetRef(202,null,null)));

        var instance=new AbilityInstance(new AbilityDefinitionRef("R3-probe","GUID"),ctx.Ability);
        var runner=new AbilityRunner();
        var sel=new Selector();
        var rejected=runner.Plan(instance,e,new Matcher(),CombatConditionNode.Any(),
            selected,sel,new IEffectOperation[]{new Operation()});
        Check("R3-C10 PreTarget false prevents selection & commands",
            sel.Calls==0 && rejected.Commands.Count==0 && rejected.Result.AppliedCommands==0);
        var accepted=runner.Plan(instance,e,new Matcher(),owner,selected,sel,
            new IEffectOperation[]{new Operation()});
        Check("R3-C11 candidate filtering selects only matching actor",
            sel.Calls==1 && accepted.Commands.Count==1 &&
            accepted.Commands[0].Target.ActorRuntimeId==201 &&
            accepted.Result.PlannedCommands==1 && accepted.Result.AppliedCommands==0 &&
            instance.RuntimeState.SuccessfulActivationCount==0);

        var settled=new AbilityEventContext(AbilityEventKind.DamageSettled,42,77,101,0,true,false);
        var evidence=new AppliedDamageEvidence(77,9001,9002,false,12,true,false,false);
        var valid=new DamageContextSnapshot(settled,true,evidence);
        var damageRule=new CombatDamageResultCondition(DamageEvidenceTest.AppliedHpAtLeast,10);
        Check("R3-C12 matching original damage provenance",damageRule.Evaluate(valid,target));
        Check("R3-C13 missing committed DamageResult rejected",
            !damageRule.Evaluate(new DamageContextSnapshot(settled,false,evidence),target));
        Check("R3-C14 unrelated cause cannot satisfy original AfterDamage",
            !damageRule.Evaluate(new DamageContextSnapshot(settled,true,new AppliedDamageEvidence(78,9001,9002,false,30,false,false,false)),target));
        Check("R3-C15 additional hit cannot inherit original AfterDamage",
            !damageRule.Evaluate(new DamageContextSnapshot(settled,true,new AppliedDamageEvidence(77,9001,9003,true,30,false,false,false)),target));
        Check("R3-C16 no damage Result on RollUsing event",
            !damageRule.Evaluate(new DamageContextSnapshot(e,true,evidence),target));
        Check("R3-C17 all evaluations leave runtime memory unchanged",
            instance.RuntimeState.SuccessfulActivationCount==0);

        Debug.Log("[R3] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R3 verification failed: "+fail);
    }
}
#endif
