#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>R3 advanced pure snapshot + phase + candidate/result binding gates.</summary>
public static class AdvancedConditionVerification
{
    private sealed class View : IConditionSnapshotContext, IReadOnlyDamageAbilityContext
    {
        public AbilityEventContext Event {get;}
        public AbilityIdentity Ability {get;}
        public ConditionSnapshot Snapshot {get;}
        public bool HasCommittedDamageResult {get;}
        public AppliedDamageEvidence DamageEvidence {get;}
        public View(AbilityEventContext e,ConditionSnapshot snap,
            bool committed=false,AppliedDamageEvidence evidence=default)
        {
            Event=e;Snapshot=snap;HasCommittedDamageResult=committed;
            DamageEvidence=evidence;
            Ability=new AbilityIdentity("r3:probe",e.OwnerActorRuntimeId,"SLOT",0);
        }
    }
    private sealed class Matcher : IAbilityTriggerMatcher
    {
        private readonly AbilityEventKind kind;
        public Matcher(AbilityEventKind kind){this.kind=kind;}
        public bool Matches(in AbilityEventContext e)=>e.Kind==kind;
    }
    private sealed class Selector : ICombatTargetSelector
    {
        private readonly CombatTargetRef[] targets;
        public int Calls {get;private set;}
        public Selector(params CombatTargetRef[] targets){this.targets=targets;}
        public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext ctx)
        {
            Calls++;return targets;
        }
    }
    private sealed class Operation : IEffectOperation
    {
        public int Calls {get;private set;}
        public void Plan(IReadOnlyAbilityContext c,
            IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
        {
            Calls++;
            foreach(var t in targets)
                sink.Enqueue(new CombatCommandIntent("R3_READ_ONLY_PROBE",t,1));
        }
    }
    private sealed class Count : ICombatCondition
    {
        public int Calls;private readonly bool value;
        public Count(bool v){value=v;}
        public bool Evaluate(IReadOnlyAbilityContext c,CombatTargetRef t)
        {
            Calls++;return value;
        }
    }
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
        int pass=0,fail=0;
        void Check(string id,bool ok,string detail)
        {
            if(ok){pass++;Debug.Log("[R3-Advanced] PASS "+id+" | "+detail);}
            else{fail++;Debug.LogError("[R3-Advanced] FAIL "+id+" | "+detail);}
        }

        var owner=new SubjectRef(101);
        var original=new SubjectRef(201,"LEFT");
        var actual=new SubjectRef(302,"RIGHT");
        var source=new SubjectRef(101);
        var facts=new Dictionary<FactKey,int>{
            [new FactKey(101,ConditionScope.Actor,ConditionFact.ResourceAmount,"olaf.madness")]=3,
            [new FactKey(101,ConditionScope.Actor,ConditionFact.CharacterContent,"olaf")]=1,
            [new FactKey(101,ConditionScope.Actor,ConditionFact.GameplayTag,"player")]=1,
            [new FactKey(201,ConditionScope.Part,ConditionFact.KeywordStack,"olaf.bleed","LEFT")]=3,
            [new FactKey(201,ConditionScope.Part,ConditionFact.KeywordStack,"yujin.mark","LEFT")]=43,
            [new FactKey(201,ConditionScope.Part,ConditionFact.KeywordStack,"yujin.mark","RIGHT")]=10,
            [new FactKey(201,ConditionScope.Actor,ConditionFact.TimedStatusPresent,"olaf.fear")]=1,
            [new FactKey(201,ConditionScope.Actor,ConditionFact.StatusNumericTotal,"fear")]=5,
            [new FactKey(201,ConditionScope.Actor,ConditionFact.StatusEffectiveAxis,"rollshift")]=2,
            [new FactKey(201,ConditionScope.Actor,ConditionFact.KeywordPresent,"olaf.fear")]=1,
            [new FactKey(302,ConditionScope.Part,ConditionFact.PartStatusPresent,"broken","RIGHT")]=1,
            [new FactKey(302,ConditionScope.Part,ConditionFact.KeywordStack,"yujin.mark","RIGHT")]=44
        };
        var snapshot=new ConditionSnapshot(owner,original,actual,source,9002,facts);
        var e=new AbilityEventContext(AbilityEventKind.RollUsing,3001,77,101,0,true,false);
        var ctx=new View(e,snapshot);
        var candidate=new CombatTargetRef(201,"LEFT","S1");
        var other=new CombatTargetRef(202,"RIGHT","S2");

        FactCondition Fact(ConditionSubject subject,ConditionScope scope,
            ConditionFact kind,string name,int minimum=1,
            FactComparison compare=FactComparison.AtLeast)=>
            new FactCondition(subject,scope,kind,name,compare,minimum);

        var resource=Fact(ConditionSubject.AbilityOwner,ConditionScope.Actor,
            ConditionFact.ResourceAmount,"olaf.madness",3);
        var bleed=Fact(ConditionSubject.SelectedTarget,ConditionScope.Part,
            ConditionFact.KeywordStack,"olaf.bleed",3);
        var fear=Fact(ConditionSubject.OriginalTarget,ConditionScope.Actor,
            ConditionFact.TimedStatusPresent,"olaf.fear");
        var broken=Fact(ConditionSubject.SelectedTarget,ConditionScope.Part,
            ConditionFact.PartStatusPresent,"broken");

        var complex=CombatConditionNode.All(
            CombatConditionNode.Leaf(resource),
            CombatConditionNode.Any(
                CombatConditionNode.Leaf(bleed),
                CombatConditionNode.Leaf(fear)),
            CombatConditionNode.Not(CombatConditionNode.Leaf(broken)));

        Check("C01 3-level All/Any/Not on explicit subjects",
            complex.Evaluate(ctx,candidate),"owner resource 3 + left bleed 3 + not broken");
        Check("C02 selected target changes, original target does not",
            !bleed.Evaluate(ctx,other) && fear.Evaluate(ctx,other),
            "candidate 202 != original 201");
        Check("C03 targetless part predicate never yields default true",
            !bleed.Evaluate(ctx,default)&&
            !Fact(ConditionSubject.SelectedTarget,ConditionScope.Part,
                ConditionFact.KeywordStack,"yujin.mark",0).Evaluate(ctx,default),
            "missing actor/part fails even threshold=0");

        int size=facts.Count;
        facts.Clear();
        Check("C04 caller dictionary mutation cannot rewrite snapshot history",
            complex.Evaluate(ctx,candidate) && size>0,
            "snapshot deep copies numeric observations");

        Check("C05 owner character content and gameplay tags distinct",
            Fact(ConditionSubject.AbilityOwner,ConditionScope.Actor,
                ConditionFact.CharacterContent,"olaf").Evaluate(ctx,candidate)&&
            Fact(ConditionSubject.AbilityOwner,ConditionScope.Actor,
                ConditionFact.GameplayTag,"player").Evaluate(ctx,candidate)&&
            !Fact(ConditionSubject.AbilityOwner,ConditionScope.Actor,
                ConditionFact.CharacterContent,"player").Evaluate(ctx,candidate),
            "OwnerCharacterIs != ActorHasGameplayTag");
        Check("C06 status presence, numeric N total, effective axis independent",
            fear.Evaluate(ctx,candidate) &&
            Fact(ConditionSubject.OriginalTarget,ConditionScope.Actor,
                ConditionFact.StatusNumericTotal,"fear",5).Evaluate(ctx,candidate) &&
            !Fact(ConditionSubject.OriginalTarget,ConditionScope.Actor,
                ConditionFact.StatusEffectiveAxis,"rollshift",3).Evaluate(ctx,candidate),
            "presence=1 N=5 effectiveRollShift=2");

        Check("C07 character keyword ID not automatically a gameplay tag",
            Fact(ConditionSubject.OriginalTarget,ConditionScope.Actor,
                ConditionFact.KeywordPresent,"olaf.fear").Evaluate(ctx,candidate) &&
            !Fact(ConditionSubject.OriginalTarget,ConditionScope.Actor,
                ConditionFact.GameplayTag,"olaf.fear").Evaluate(ctx,candidate),
            "keyword != tag, even with same display name");
        Check("C08 selected Part mark 43 vs distinct right 10",
            Fact(ConditionSubject.SelectedTarget,ConditionScope.Part,
                ConditionFact.KeywordStack,"yujin.mark",43).Evaluate(ctx,candidate) &&
            !Fact(ConditionSubject.SelectedTarget,ConditionScope.Part,
                ConditionFact.KeywordStack,"yujin.mark",44).Evaluate(ctx,candidate) &&
            Fact(ConditionSubject.SelectedTarget,ConditionScope.Part,
                ConditionFact.KeywordStack,"yujin.mark",10).Evaluate(ctx,
                    new CombatTargetRef(201,"RIGHT","S1")),
            "part stack, not N/T accumulation");
        Check("C09 actual damage target != original selected",
            Fact(ConditionSubject.ActualDamageTarget,ConditionScope.Part,
                ConditionFact.PartStatusPresent,"broken").Evaluate(ctx,candidate)&&
            !Fact(ConditionSubject.OriginalTarget,ConditionScope.Part,
                ConditionFact.PartStatusPresent,"broken").Evaluate(ctx,candidate),
            "actual actor 302/right");

        var seenFalse=new Count(false);var after=new Count(true);
        var shortCircuit=CombatConditionNode.All(CombatConditionNode.Leaf(seenFalse),
            CombatConditionNode.Leaf(after));
        Check("C10 complex All stops after first false",
            !shortCircuit.Evaluate(ctx,candidate)&&seenFalse.Calls==1&&after.Calls==0,
            "no superfluous reads");
        var pre=new Selector(candidate,other);
        var op=new Operation();
        var runner=new AbilityRunner();
        var instance=new AbilityInstance(new AbilityDefinitionRef("r3:probe","guid"),
            new AbilityIdentity("r3:probe",101,"SLOT",0));
        var blocked=runner.PlanWithSnapshot(instance,e,
            new Matcher(AbilityEventKind.RollUsing),
            CombatConditionNode.Not(CombatConditionNode.Leaf(resource)),
            complex,pre,new[]{(IEffectOperation)op},snapshot);
        Check("C11 PreTarget false means zero target queries",
            blocked.Commands.Count==0 && pre.Calls==0 && op.Calls==0,
            "pre-filter strictly precedes selection");
        var selectedOnly=CombatConditionNode.All(
            CombatConditionNode.Leaf(resource),CombatConditionNode.Leaf(bleed),
            CombatConditionNode.Not(CombatConditionNode.Leaf(broken)));
        var accepted=runner.PlanWithSnapshot(instance,e,
            new Matcher(AbilityEventKind.RollUsing),resource,selectedOnly,
            pre,new[]{(IEffectOperation)op},snapshot);
        Check("C12 candidate condition binds only left matching target",
            accepted.Commands.Count==1 &&
            accepted.Commands[0].Target.ActorRuntimeId==201 &&
            accepted.Result.AppliedCommands==0 && op.Calls==1,
            "Plan != Applied");

        var rejected=runner.PlanWithSnapshot(instance,e,
            new Matcher(AbilityEventKind.RollUsing),resource,
            CombatConditionNode.Leaf(Fact(ConditionSubject.SelectedTarget,
                ConditionScope.Part,ConditionFact.KeywordStack,"nonexistent",1)),
            pre,new[]{(IEffectOperation)op},snapshot);
        Check("C13 no eligible targets does not submit targetless commands",
            rejected.Commands.Count==0&&op.Calls==1 &&
            !string.IsNullOrEmpty(rejected.Result.FailureReason),
            "no implicit fallback target");

        var useResult=runner.PlanWithSnapshot(instance,e,
            new Matcher(AbilityEventKind.RollUsing),resource,bleed,
            pre,new[]{(IEffectOperation)op},null);
        Check("C14 missing phase snapshot rejected",useResult.Commands.Count==0,
            "no silent target reinterpretation");

        var settled=new AbilityEventContext(AbilityEventKind.DamageSettled,
            3001,77,101,0,true,false);
        var evidence=new AppliedDamageEvidence(77,9001,9002,false,
            12,true,false,false,302,"RIGHT");
        var actualSelector=new Selector(new CombatTargetRef(302,"RIGHT","D1"));
        var afterRule=CombatConditionNode.All(
            CombatConditionNode.Leaf(new DamageOriginCondition(77,9002)),
            CombatConditionNode.Leaf(new CombatDamageResultCondition(
                DamageEvidenceTest.AppliedHpAtLeast,10)),
            CombatConditionNode.Leaf(Fact(
                ConditionSubject.ActualDamageTarget,ConditionScope.Part,
                ConditionFact.KeywordStack,"yujin.mark",44)));
        var appliedPlan=runner.PlanCommittedDamage(instance,settled,
            new Matcher(AbilityEventKind.DamageSettled),null,afterRule,
            actualSelector,new[]{(IEffectOperation)op},snapshot,evidence);
        Check("C15 committed result conditions use actual damage part",
            appliedPlan.Commands.Count==1 &&
            appliedPlan.Commands[0].Target.ActorRuntimeId==302,
            "original 201, real damage 302, hit 9002");
        Check("C16 unrelated cause result rejected",
            runner.PlanCommittedDamage(instance,settled,
                new Matcher(AbilityEventKind.DamageSettled),null,afterRule,
                actualSelector,new[]{(IEffectOperation)op},snapshot,
                new AppliedDamageEvidence(78,9001,9002,false,99,false,false,false,302,"RIGHT"))
                .Commands.Count==0,
            "no current-HP fallback");
        Check("C17 wrong target part evidence rejected",
            runner.PlanCommittedDamage(instance,settled,
                new Matcher(AbilityEventKind.DamageSettled),null,afterRule,
                actualSelector,new[]{(IEffectOperation)op},snapshot,
                new AppliedDamageEvidence(77,9001,9002,false,99,false,false,false,302,"LEFT"))
                .Commands.Count==0,
            "damage target cannot move after commit");
        Check("C18 additional hit cannot wake original hit condition",
            runner.PlanCommittedDamage(instance,settled,
                new Matcher(AbilityEventKind.DamageSettled),null,afterRule,
                actualSelector,new[]{(IEffectOperation)op},snapshot,
                new AppliedDamageEvidence(77,9001,9002,true,99,false,false,false,302,"RIGHT"))
                .Commands.Count==0,
            "R3 originalHitOnly");
        Check("C19 no damage evidence on RollUsing",
            runner.PlanCommittedDamage(instance,e,
                new Matcher(AbilityEventKind.DamageSettled),null,afterRule,
                actualSelector,new[]{(IEffectOperation)op},snapshot,evidence)
                .Commands.Count==0,"strict postcommit gate");
        Check("C20 zero/unknown origin hit never qualifies",
            runner.PlanCommittedDamage(instance,settled,
                new Matcher(AbilityEventKind.DamageSettled),null,afterRule,
                actualSelector,new[]{(IEffectOperation)op},snapshot,
                new AppliedDamageEvidence(77,9001,0,false,99,false,false,false,302,"RIGHT"))
                .Commands.Count==0,"origin hit required");
        Check("C21 no planning-only result increments memory",
            instance.RuntimeState.SuccessfulActivationCount==0 &&
            accepted.Result.PlannedCommands==1 &&
            appliedPlan.Result.AppliedCommands==0,
            "no mutations or DomainEvent in condition path");
        Debug.Log("[R3-Advanced] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R3 advanced contract failed="+fail);
    }
}
#endif
