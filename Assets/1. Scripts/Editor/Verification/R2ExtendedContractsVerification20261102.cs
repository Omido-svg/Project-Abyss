#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R2-K/W + core ability plan: independent instances, owner-gated read-only
/// keyword lookup, distinct part storage, immutable weight snapshots.
/// Uses a fixture snapshot source; never claims these values were present on
/// real Yujin status storage until the separate PlayMode verification runs.
/// </summary>
public static class R2ExtendedContractsVerification20261102
{
    private sealed class SnapshotSource : IR2KeywordSnapshotSource
    {
        private readonly Dictionary<(long,long,string,string),int> values =
            new Dictionary<(long,long,string,string),int>();
        public int Reads {get;private set;}
        public void Seed(long owner,long carrier,string keyword,string part,int amount) =>
            values[(owner,carrier,keyword,part??string.Empty)]=amount;
        public bool TryRead(in R2KeywordReadRequest r,out int value)
        {
            Reads++;
            return values.TryGetValue((r.GrantingActorRuntimeId,
                r.CarrierActorRuntimeId,r.KeywordId,r.CarrierPartId),out value);
        }
    }
    private sealed class Trigger : IAbilityTriggerMatcher
    {
        public bool Matches(in AbilityEventContext context) => context.Kind==AbilityEventKind.RollUsing;
    }
    private sealed class Conditions : ICombatCondition
    {
        private readonly long actor;public Conditions(long actor){this.actor=actor;}
        public bool Evaluate(IReadOnlyAbilityContext c,CombatTargetRef target) =>
            target.ActorRuntimeId==actor;
    }
    private sealed class Selector : ICombatTargetSelector
    {
        public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext context) =>
            new[]{new CombatTargetRef(11,"ARM",""),new CombatTargetRef(12,"LEG","")};
    }
    private sealed class Operation : IEffectOperation
    {
        public void Plan(IReadOnlyAbilityContext c,
            IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
        {
            foreach(var t in targets)
                sink.Enqueue(new CombatCommandIntent("ProbeDamage",t,1));
        }
    }

    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
        int pass=0,fail=0;
        void Check(string id,bool ok,string info)
        {
            if(ok){pass++;Debug.Log("[R2-KW] PASS "+id+" | "+info);}
            else{fail++;Debug.LogError("[R2-KW] FAIL "+id+" | "+info);}
        }

        var mark=new R2KeywordDefinition("yujin.mark","yujin","표식",
            R2KeywordStorageKind.PartStackLedger,R2KeywordCarrier.Part);
        var bleed=new R2KeywordDefinition("olaf.bleed","olaf","출혈",
            R2KeywordStorageKind.PartStackLedger,R2KeywordCarrier.Part);
        var features=new R2CharacterFeatureSet("yujin",new[]{mark},
            resourceRefs:new[]{"yujin.assassin_sense"});
        var olaf=new R2CharacterFeatureSet("olaf",new[]{bleed});
        var source=new SnapshotSource();
        source.Seed(101,501,"yujin.mark","arm-left",43);
        source.Seed(101,501,"yujin.mark","arm-right",10);
        source.Seed(102,501,"yujin.mark","arm-left",7);
        source.Seed(200,501,"olaf.bleed","arm-left",6);
        var contentIds=new Dictionary<long,string>{{101,"yujin"},{102,"yujin"},{200,"olaf"},{999,"olaf"}};
        var registry=new R2CharacterFeatureRegistry(source,
            id=>contentIds.TryGetValue(id,out var content) ? content : null);
        Check("K01 bind per-instance actor IDs even sharing CharacterContentId",
            registry.TryBind(101,features)&&registry.TryBind(102,features)&&
            registry.BoundActorCount==2,"same catalog, two runtime actors");
        Check("K02 bind Olaf separately",registry.TryBind(200,olaf),
            "character-specific content not universal");

        bool left=registry.TryRead(new R2KeywordReadRequest(101,501,"yujin.mark","arm-left"),out int l);
        bool right=registry.TryRead(new R2KeywordReadRequest(101,501,"yujin.mark","arm-right"),out int r);
        Check("K03 same enemy two parts retain independent 43/10",
            left&&right&&l==43&&r==10,"fixture: left="+l+" right="+r);
        Check("K04 same content ID, two runtime actors never merge",
            registry.TryRead(new R2KeywordReadRequest(102,501,"yujin.mark","arm-left"),out int l2)&&l2==7,
            "other Yujin actor left="+l2);

        int oldReads=source.Reads;
        Check("K05 invalid owner cannot grant foreign unique Mark",
            !registry.TryRead(new R2KeywordReadRequest(200,501,"yujin.mark","arm-left"),out _) &&
            source.Reads==oldReads,"rejected before reaching native storage");
        Check("K06 authored part keyword requires explicit part ID",
            !registry.TryRead(new R2KeywordReadRequest(101,501,"yujin.mark"),out _),
            "no implicit actor-wide merge");
        Check("K07 granting Olaf can attach bleed to enemy's part",
            registry.TryRead(new R2KeywordReadRequest(200,501,"olaf.bleed","arm-left"),out int b)&&b==6,
            "owner 200, victim 501, bleed=6");

        bool wrongOwnerRejected=false,duplicateRejected=false;
        try{new R2CharacterFeatureSet("olaf",new[]{mark});}
        catch(ArgumentException){wrongOwnerRejected=true;}
        try{new R2CharacterFeatureSet("yujin",new[]{mark,mark});}
        catch(ArgumentException){duplicateRejected=true;}
        Check("K08 cross-character definition and duplicate ID are blocked",
            wrongOwnerRejected&&duplicateRejected,
            "feature set owns static definitions only");
        Check("K11 wrong actual character content ID cannot bind Yujin feature set",
            !registry.TryBind(999,features),
            "actor#999 is Olaf in authoritative content lookup");
        
Check("K09 no second registration of the same actor",
            !registry.TryBind(101,features),"idempotent single binding");
        registry.ClearForBattleEnd();
        Check("K10 battle unbind makes stale keyword queries invalid",
            registry.BoundActorCount==0 &&
            !registry.TryRead(new R2KeywordReadRequest(101,501,"yujin.mark","arm-left"),out _),
            "old source remains untouched");

        var shared=new AbilityDefinitionRef("skill:r2","guid-shared");
        var abilityA=new AbilityInstance(shared,new AbilityIdentity("skill:r2",701,"slot-A",0));
        var abilityB=new AbilityInstance(shared,new AbilityIdentity("skill:r2",702,"slot-A",0));
        var context=new AbilityEventContext(AbilityEventKind.RollUsing,10001,33,701,0,true,false);
        var runner=new AbilityRunner();
        var planA=runner.Plan(abilityA,context,new Trigger(),null,new Conditions(11),
            new Selector(),new IEffectOperation[]{new Operation()});
        var planB=runner.Plan(abilityB,context,new Trigger(),null,new Conditions(12),
            new Selector(),new IEffectOperation[]{new Operation()});
        Check("A01 same definition, per-instance selectors produce separate commands",
            ReferenceEquals(abilityA.Definition,abilityB.Definition)&&
            planA.Commands.Count==1&&planA.Commands[0].Target.ActorRuntimeId==11&&
            planB.Commands.Count==1&&planB.Commands[0].Target.ActorRuntimeId==12,
            "no shared selected target");
        Check("A02 planning is never a successful apply/commit",
            !planA.Result.HasApplied && !planB.Result.HasApplied &&
            abilityA.RuntimeState.SuccessfulActivationCount==0 &&
            abilityB.RuntimeState.SuccessfulActivationCount==0,
            "plan does not mutate live combat");
        var activation=new ActivationKey(10001,AbilityEventKind.RollUsing,0,33);
        abilityA.RuntimeState.Commit(activation,new AbilityExecutionResult(1,1,""));
        Check("A03 committed action A does not lock action B",
            !abilityA.RuntimeState.CanActivate(activation) &&
            abilityB.RuntimeState.CanActivate(activation) &&
            abilityA.RuntimeState.SuccessfulActivationCount==1 &&
            abilityB.RuntimeState.SuccessfulActivationCount==0,
            "independent memory, stable cause");
        Check("A04 repeat plan rejects already committed activation",
            runner.Plan(abilityA,context,new Trigger(),new Conditions(11),
                new Selector(),new IEffectOperation[]{new Operation()})
                .Commands.Count==0,"replay not planned");

        var p=new WeightedTargetSlot(new CombatSlotRef(99,"M","arm",3),
            new HitTargetKey(99,"arm"),2,0,0,0);
        var s1=new WeightedTargetSlot(new CombatSlotRef(99,"S1","arm",3),
            new HitTargetKey(99,"arm"),1,0,0,1);
        var s2=new WeightedTargetSlot(new CombatSlotRef(99,"S2","leg",3),
            new HitTargetKey(99,"leg"),1,0,0,2);
        var mutableCandidates=new List<WeightedTargetSlot>{s1,s2};
        var modifiers=new List<R2WeightModifier>{
            new R2WeightModifier("plus-at-start",1,R2WeightModifierPhase.AtActionStart),
            new R2WeightModifier("plus-at-use",1,R2WeightModifierPhase.OnRollUsing)};
        var key=new R2WeightSelectionKey(6001,4,6,"policy:slot-v1");
        var planSnapshot=new R2AttackWeightSelectionSnapshot(key,2,p,
            mutableCandidates,modifiers,R2WeightModifierPhase.AtPlanning);
        var startSnapshot=new R2AttackWeightSelectionSnapshot(key,2,p,
            mutableCandidates,modifiers,R2WeightModifierPhase.AtActionStart);
        var useSnapshot=new R2AttackWeightSelectionSnapshot(key,2,p,
            mutableCandidates,modifiers,R2WeightModifierPhase.OnRollUsing);
        mutableCandidates.Clear();
        modifiers.Clear();
        Check("W01 immutable snapshot copies slots and modifiers",
            planSnapshot.Secondary.Count==2 && startSnapshot.Modifiers.Count==2,
            "source collections cleared without affecting capture");
        Check("W02 additive modifier applies only at explicit phase",
            planSnapshot.EffectiveWeight==2&&
            startSnapshot.EffectiveWeight==3&&
            useSnapshot.EffectiveWeight==4,
            "2 -> 3 -> 4; no SO field mutation");
        var selection=useSnapshot.Plan();
        Check("W03 4-budget keeps distinct slots and physical hits separate",
            selection.Success&&selection.Allocation.SelectedSlots.Count==3&&
            selection.Allocation.DistinctHitTargets.Count==2&&
            selection.Allocation.SelectedSlots[0].ActuallyConsumedWeight==2,
            "3 slots, 2 actual part hits");
        var keyNext=new R2WeightSelectionKey(6001,5,7,"policy:slot-v1");
        var keyPolicy=new R2WeightSelectionKey(6001,4,6,"policy:other");
        Check("W04 cache key invalidates on targeting version / plan revision / policy",
            !key.Equals(keyNext)&&!key.Equals(keyPolicy),
            "ActionId + revision + target version + policy");
        Check("W05 same base SO setting remains unchanged",
            new AttackWeightSettings().EffectiveWeight==1 &&
            new AttackWeightSettings().TargetingModel==
                AttackWeightTargetingModel.LegacyCharacterRandom,
            "old content opt-in");
        Debug.Log("[R2-KW] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R2 K/W contract failure="+fail);
    }
}
#endif
