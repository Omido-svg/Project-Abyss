#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R5CommandApplyVerification20261030
{
    private sealed class FakeSpecialist : ICombatCommandBackend
    {
        public readonly List<string> Calls = new List<string>();
        public int SimulatedHp = 100;
        public bool Unsupported;
        public string ThrowOn;
        public Func<CombatCommandEnvelope,CombatCommandReceipt> Receipt;
        public bool CanApply(CombatCommandKind kind) => !Unsupported &&
            kind==CombatCommandKind.DealDamage;
        public CombatCommandReceipt Apply(CombatCommandEnvelope cmd)
        {
            Calls.Add(cmd.CommandId);
            if(cmd.CommandId==ThrowOn)throw new InvalidOperationException("Unknown specialist commit");
            if(Receipt!=null)return Receipt(cmd);
            int value=Math.Min(cmd.RequestedAmount,SimulatedHp);
            SimulatedHp-=value;
            return new CombatCommandReceipt(CombatCommandOutcome.Applied,value,0,value>0,"fake test");
        }
    }

    private static CombatCommandEnvelope Cmd(string id,AbilityIdentity identity,
        long action=900,long cause=111,int phase=1,int priority=0,int registration=0,
        long sequence=0,long target=202,int amount=7) =>
        new CombatCommandEnvelope(id,identity,CombatCommandKind.DealDamage,
            action,cause,new CombatTargetRef(target,"HEAD","S1"),
            amount,phase,priority,registration,sequence);

    private static AbilityInstance Instance(long actor=101) =>
        new AbilityInstance(new AbilityDefinitionRef("TEST","TEST-GUID"),
            new AbilityIdentity("TEST",actor,"S1",0));

    private static AbilityEventContext Event(long action=900,long cause=111) =>
        new AbilityEventContext(AbilityEventKind.RollUsing,action,cause,101,0,false,false);

    [MenuItem("Game System Verification/R5 - Command Apply Contract (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only");
        int pass=0,fail=0;
        void Check(string id,bool ok)
        {
            if(ok){pass++;Debug.Log("[R5-Contract] PASS "+id);}
            else{fail++;Debug.LogError("[R5-Contract] FAIL "+id);}
        }

        var instance=Instance();
        var svc=new CombatCommandApplyService();
        var backend=new FakeSpecialist();
        var eventA=Event();
        var batch=svc.Apply(instance,eventA,
            new[]{Cmd("damage-1",instance.Identity)},backend);
        Check("A01 specialist executes one damage command",
            backend.SimulatedHp==93 && backend.Calls.Count==1);
        Check("A02 backend committed result, not plan-only flag",
            batch.CommittedCount==1 && batch.Records[0].Receipt.AppliedAmount==7 &&
            instance.RuntimeState.SuccessfulActivationCount==1);
        var again=svc.Apply(instance,eventA,
            new[]{Cmd("damage-1",instance.Identity)},backend);
        Check("A03 duplicate command ID does not damage twice",
            backend.Calls.Count==1 && backend.SimulatedHp==93 &&
            again.Records[0].Receipt.Outcome==CombatCommandOutcome.SkippedDuplicate);

        var wait=Instance(300);
        var deferredBackend=new FakeSpecialist
        {
            Receipt=cmd=>new CombatCommandReceipt(CombatCommandOutcome.Deferred,
                0,0,false,"queue not yet drained")
        };
        var deferredSvc=new CombatCommandApplyService();
        var pending=deferredSvc.Apply(wait,eventA,
            new[]{Cmd("deferred",wait.Identity)},deferredBackend);
        Check("A04 planned/deferred is not successfully committed",
            pending.CommittedCount==0 && wait.RuntimeState.SuccessfulActivationCount==0);

        var orderedInst=Instance(301);
        var orderedBackend=new FakeSpecialist();
        var orderedSvc=new CombatCommandApplyService();
        var unordered=new[]{
            Cmd("z",orderedInst.Identity,phase:2,priority:0,sequence:1),
            Cmd("b",orderedInst.Identity,phase:1,priority:1,sequence:1),
            Cmd("a",orderedInst.Identity,phase:1,priority:0,sequence:10),
            Cmd("c",orderedInst.Identity,phase:1,priority:0,sequence:11)
        };
        var result=orderedSvc.Apply(orderedInst,eventA,unordered,orderedBackend);
        Check("A05 stable phase/priority/sequence order",
            string.Join(",",orderedBackend.Calls)=="a,c,b,z");
        Check("A06 multi-command activation commits ONCE",
            result.CommittedCount==4 && orderedInst.RuntimeState.SuccessfulActivationCount==1);
        Check("A07 one backend application per unique command",
            orderedBackend.SimulatedHp==72 && orderedBackend.Calls.Count==4);

        var invalidInst=Instance(302);
        var invalidSvc=new CombatCommandApplyService();
        var invalidBackend=new FakeSpecialist();
        var invalidResult=invalidSvc.Apply(invalidInst,eventA,new[]{
            Cmd("bad-target",invalidInst.Identity,target:0),
            Cmd("bad-cause",invalidInst.Identity,cause:999),
            Cmd("bad-id",invalidInst.Identity,action:901),
            Cmd("valid",invalidInst.Identity)
        },invalidBackend);
        Check("A08 bad cause/target/action rejected before specialist",
            invalidBackend.Calls.Count==1 && invalidResult.RejectedCount==3);
        Check("A09 valid command still applies after rejected commands",
            invalidResult.CommittedCount==1 && invalidInst.RuntimeState.SuccessfulActivationCount==1);

        var unsupportedInst=Instance(303);
        var unsupportedBackend=new FakeSpecialist{Unsupported=true};
        var noBackend=new CombatCommandApplyService().Apply(unsupportedInst,eventA,
            new[]{Cmd("unsupported",unsupportedInst.Identity)},unsupportedBackend);
        Check("A10 missing specialist cannot pretend success",
            noBackend.CommittedCount==0 && unsupportedBackend.Calls.Count==0 &&
            unsupportedInst.RuntimeState.SuccessfulActivationCount==0);

        var absorbedInst=Instance(304);
        var absorbedSvc=new CombatCommandApplyService();
        var absorbedBackend=new FakeSpecialist
        {
            Receipt=cmd=>new CombatCommandReceipt(CombatCommandOutcome.Absorbed,
                0,7,false,"no actual state delta")
        };
        var absorbed=absorbedSvc.Apply(absorbedInst,eventA,
            new[]{Cmd("absorbed",absorbedInst.Identity)},absorbedBackend);
        Check("A11 absorbed label alone is not applied effect",
            absorbed.CommittedCount==0 && absorbedInst.RuntimeState.SuccessfulActivationCount==0);

        var brokeInst=Instance(305);
        var brokeSvc=new CombatCommandApplyService();
        var brokeBackend=new FakeSpecialist{ThrowOn="unknown"};
        var firstUnknown=brokeSvc.Apply(brokeInst,eventA,new[]{
            Cmd("unknown",brokeInst.Identity,sequence:0),
            Cmd("after-unknown",brokeInst.Identity,sequence:1)
        },brokeBackend);
        Check("A12 exception gives indeterminate result and halts batch",
            firstUnknown.HasUnknownCommit && brokeBackend.Calls.SequenceEqual(new[]{"unknown"}));
        var secondUnknown=brokeSvc.Apply(brokeInst,eventA,
            new[]{Cmd("new-id-same-cause",brokeInst.Identity)},brokeBackend);
        Check("A13 unsafe same-origin retry remains blocked",
            secondUnknown.CommittedCount==0 && brokeBackend.Calls.Count==1 &&
            brokeInst.RuntimeState.SuccessfulActivationCount==0);

        var limitedInst=Instance(306);
        var limitedSvc=new CombatCommandApplyService{MaxCommandsPerBatch=1};
        var limitedBackend=new FakeSpecialist();
        bool limitRejected=false;
        try
        {
            limitedSvc.Apply(limitedInst,eventA,new[]{
                Cmd("limit1",limitedInst.Identity),
                Cmd("limit2",limitedInst.Identity)
            },limitedBackend);
        }
        catch(InvalidOperationException){limitRejected=true;}
        Check("A14 excessive command batch rejected before any apply",
            limitRejected && limitedBackend.Calls.Count==0);

        var resetInst=Instance(307);
        var resetSvc=new CombatCommandApplyService();
        var resetBackend=new FakeSpecialist();
        resetSvc.Apply(resetInst,eventA,new[]{Cmd("will-reset",resetInst.Identity)},resetBackend);
        resetSvc.ResetForBattle();
        Check("A15 end-of-battle clears applied-command ID ledger",
            resetSvc.UniqueAttemptCount==0);

        Debug.Log("[R5-Contract] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R5 command contract failed "+fail);
    }
}
#endif
