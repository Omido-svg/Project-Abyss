#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R5PlanToSpecialistVerification20261030
{
    private sealed class Matcher : IAbilityTriggerMatcher
    {
        public bool Matches(in AbilityEventContext e) =>
            e.Kind==AbilityEventKind.RollUsing;
    }
    private sealed class Selector : ICombatTargetSelector
    {
        private readonly CombatTargetRef target;
        public Selector(CombatTargetRef target) { this.target=target; }
        public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext context) =>
            new[]{target};
    }
    private sealed class BadOperation : IEffectOperation
    {
        public void Plan(IReadOnlyAbilityContext c,
            IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
        {
            foreach(var t in targets)
                sink.Enqueue(new CombatCommandIntent("UNSUPPORTED-STRING",t,1));
        }
    }
    private sealed class UnknownBackend : ICombatCommandBackend
    {
        public int Calls;
        public bool CanApply(CombatCommandKind kind) => true;
        public CombatCommandReceipt Apply(CombatCommandEnvelope command)
        {
            Calls++;
            return new CombatCommandReceipt(CombatCommandOutcome.UnknownCommit,
                0,0,false,"Explicitly indeterminate; maybe already mutated.");
        }
    }

    [MenuItem("Game System Verification/R5 - Plan to Real Specialist (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)
            throw new InvalidOperationException("Play Mode only.");
        var manager=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var battle=manager?.BattleContext;
        var player=battle?.Player;
        if(player==null)throw new InvalidOperationException("Player actor not initialized.");
        int pass=0,fail=0;
        void Check(string name,bool yes,string note="")
        {
            if(yes){pass++;Debug.Log("[R5-EndToEnd] PASS "+name+" "+note);}
            else{fail++;Debug.LogError("[R5-EndToEnd] FAIL "+name+" "+note);}
        }

        var identity=new AbilityIdentity("R5-ENDTOEND",
            R4LiveSlotQuery.ActorRuntimeId(player),"R5-VIRTUAL",0);
        var instance=new AbilityInstance(new AbilityDefinitionRef("R5-ENDTOEND","TEST"),identity);
        var event1=new AbilityEventContext(AbilityEventKind.RollUsing,
            990100,990101,identity.OwnerActorRuntimeId,0,false,false);
        var target=new CombatTargetRef(identity.OwnerActorRuntimeId,null,null);
        var runner=new AbilityRunner();
        var op=new IEffectOperation[]{new ChangeResourceOperation(2)};
        var plan=runner.Plan(instance,event1,new Matcher(),null,new Selector(target),op);

        Check("E01 planned command does not mark applied",
            plan.Commands.Count==1 &&
            plan.Result.AppliedCommands==0 &&
            instance.RuntimeState.SuccessfulActivationCount==0);

        var cmd=R5PlannedCommandBridge.Materialize(instance,event1,
            plan,"R5-BATCH-1",phase:1);
        Check("E02 bridge preserves cause/action/typed command",
            cmd.Count==1 &&
            cmd[0].Kind==CombatCommandKind.ChangeResource &&
            cmd[0].ActionId==990100 && cmd[0].CauseEventId==990101 &&
            cmd[0].RequestedAmount==2 && cmd[0].CommandId=="R5-BATCH-1:0000");

        var backend=new R5CombatSpecialistBackend(battle);
        var service=new CombatCommandApplyService();
        const string resource="R5_ENDTOEND_TEST_ONLY";
        SkillResourceAccess.Set(player,resource,2);
        int before=SkillResourceAccess.Get(player,resource);
        backend.RegisterResourceChange(cmd[0],player,resource);
        var receipt=service.Apply(instance,event1,cmd,backend);
        int after=SkillResourceAccess.Get(player,resource);
        Check("E03 real resource changed by planned operation",
            after-before==2 && receipt.CommittedCount==1 &&
            receipt.Records[0].Receipt.AppliedAmount==2);
        Check("E04 ability memory counts only committed apply",
            instance.RuntimeState.SuccessfulActivationCount==1);

        var repeated=service.Apply(instance,event1,cmd,backend);
        Check("E05 duplicate batch cannot double-spend or apply",
            repeated.CommittedCount==0 &&
            repeated.Records[0].Receipt.Outcome==CombatCommandOutcome.SkippedDuplicate &&
            SkillResourceAccess.Get(player,resource)==after);

        var planSame=runner.Plan(instance,event1,new Matcher(),null,new Selector(target),op);
        Check("E06 exact same source event is not planned again",
            planSame.Commands.Count==0 && planSame.Result.AppliedCommands==0);

        var event2=new AbilityEventContext(AbilityEventKind.RollUsing,
            990100,990102,identity.OwnerActorRuntimeId,0,false,false);
        var planNext=runner.Plan(instance,event2,new Matcher(),null,new Selector(target),op);
        Check("E07 different cause from same action/roll can plan separately",
            planNext.Commands.Count==1);
        var newCmd=R5PlannedCommandBridge.Materialize(instance,event2,
            planNext,"R5-BATCH-2",phase:1);
        backend.RegisterResourceChange(newCmd[0],player,resource);
        var committedNext=service.Apply(instance,event2,newCmd,backend);
        Check("E08 distinct cause applies with independent activation key",
            committedNext.CommittedCount==1 &&
            SkillResourceAccess.Get(player,resource)==after+2 &&
            instance.RuntimeState.SuccessfulActivationCount==2);

        var unsupported=runner.Plan(new AbilityInstance(
            new AbilityDefinitionRef("UNKNOWN","GUID"),identity),
            event2,new Matcher(),null,new Selector(target),
            new IEffectOperation[]{new BadOperation()});
        bool invalidRejected=false;
        try{R5PlannedCommandBridge.Materialize(instance,event2,
            unsupported,"BAD",phase:1);}
        catch(InvalidOperationException){invalidRejected=true;}
        Check("E09 unknown string operation rejected before execution",invalidRejected);

        bool invalidOwner=false;
        try{
            var wrong=new AbilityEventContext(AbilityEventKind.RollUsing,
                990103,990104,999999,0,false,false);
            R5PlannedCommandBridge.Materialize(instance,wrong,planNext,"BADOWNER",phase:1);
        }catch(InvalidOperationException){invalidOwner=true;}
        Check("E10 mismatched owner provenance rejected",invalidOwner);

        var unsafeInstance=new AbilityInstance(
            new AbilityDefinitionRef("UNKNOWN-RECEIPT","GUID"),
            new AbilityIdentity("UNKNOWN-RECEIPT",identity.OwnerActorRuntimeId,"S",0));
        var unsafeCause=new AbilityEventContext(AbilityEventKind.RollUsing,
            990105,990106,identity.OwnerActorRuntimeId,0,false,false);
        var one=new CombatCommandEnvelope("u1",unsafeInstance.Identity,
            CombatCommandKind.ChangeResource,990105,990106,target,1,1,0,0,0);
        var two=new CombatCommandEnvelope("u2",unsafeInstance.Identity,
            CombatCommandKind.ChangeResource,990105,990106,target,1,1,0,0,1);
        var fake=new UnknownBackend();
        var unknown=service.Apply(unsafeInstance,unsafeCause,new[]{one,two},fake);
        Check("E11 returned UnknownCommit halts remaining batch",
            unknown.HasUnknownCommit && unknown.Records.Count==1 && fake.Calls==1);
        var late=new CombatCommandEnvelope("u3",unsafeInstance.Identity,
            CombatCommandKind.ChangeResource,990105,990106,target,1,1,0,0,2);
        var blocked=service.Apply(unsafeInstance,unsafeCause,new[]{late},fake);
        Check("E12 unknown cause quarantined against different command ID",
            fake.Calls==1 && blocked.CommittedCount==0);

        backend.ClearBindings();
        Debug.Log("[R5-EndToEnd] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R5 end-to-end failed "+fail);
    }
}
#endif
