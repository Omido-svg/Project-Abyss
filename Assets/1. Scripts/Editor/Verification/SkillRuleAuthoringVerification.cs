#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;
using ProjectAbyss.Examples;

/// <summary>Checks the authoring boundary against the pre-facade graph and real Unity persistence.</summary>
public static class SkillRuleAuthoringVerification
{
    public static string LastReport { get; private set; }

    [MenuItem("Tools/Project Abyss/Verification/Skill Rule Authoring")]
    public static void RunVerification()
    {
        int passed=0,failed=0;
        var lines=new List<string>();
        var owned=new List<UnityEngine.Object>();
        var registry=new AbilityRuleRegistry();AbilityRuleModules.Configure(registry);
        void Check(string id,bool ok){lines.Add((ok?"PASS ":"FAIL ")+id);if(ok)passed++;else failed++;}
        void Case(string id,Action run){try{run();}catch(Exception e){Check(id+": "+e.GetBaseException().Message,false);}}
        bool Rejects(Action run){try{run();return false;}catch(ArgumentException){return true;}}
        var source=EngravedContractExample.CreateSource();owned.Add(source);
        var settings=ScriptableObject.CreateInstance<EngravedContractSettings>();owned.Add(settings);
        try
        {
            Case("legacy graph equivalence",()=>
            {
                foreach(var values in new[]{new[]{3,6,1},new[]{9,1,7},new[]{1,20,4}})
                {
                    var expected=LegacyGraph(source,settings,out var expectedReservation,values[0],values[1],values[2]);
                    var actual=EngravedContractRules.Create(source,"fixture-guid",settings,out var actualReservation,values[0],values[1],values[2]);
                    owned.Add(expected);owned.Add(expectedReservation);owned.Add(actual);owned.Add(actualReservation);
                    Check("same rule count, order and complete serialized fields / "+string.Join(",",values),
                        expected.Rules.Length==actual.Rules.Length&&expected.Rules.Zip(actual.Rules,(a,b)=>
                            JsonUtility.ToJson(a).Replace("\"instanceID\":"+expectedReservation.GetInstanceID(),
                                "\"instanceID\":"+actualReservation.GetInstanceID())==JsonUtility.ToJson(b)).All(equal=>equal));
                    Check("same compiled operation settings and valid graph / "+string.Join(",",values),registry.Validate(actual).Count==0&&
                        ((EngravedContractSpec)new AbilityOperationSpec(actual.Rules[0].Operations[0]).Settings).Marked==settings.MarkWithStartMark&&
                        ((AbilityScheduleSpec)new AbilityOperationSpec(actual.Rules[2].Operations[0]).Settings).Snapshot==AbilitySnapshotPolicy.LockAtScheduleCreation);
                }
            });
            Case("canonical authoring",()=>
            {
                var definition=SkillRules.CreateDraft(source,"fixture-guid");owned.Add(definition);
                definition.Rules=new[]{
                    SkillRules.OnUse("use",AbilityEffects.Energy(1)).To(AbilityTargets.Self),
                    SkillRules.OnExchangeWin("win",AbilityEffects.Block(2)),
                    SkillRules.OnExchangeLoss("loss",AbilityEffects.Block(1)),
                    SkillRules.OnClashCompleted("clash",AbilityEffects.Energy(1)),
                    SkillRules.OnAttackResolutionStarting("start",AbilityEffects.Block(1)),
                    SkillRules.OnPreparationActivated("preparation",AbilityEffects.Energy(1))};
                Check("all six official card triggers remain valid",registry.Validate(definition).Count==0);
                Check("draft identity preserved and never automatically enabled",!definition.Enabled&&definition.SourceKind==AbilityRuleSourceKind.Skill&&
                    definition.StableId==source.SkillId&&definition.SourceAssetName==source.name);
                Check("internal checkpoint cannot accidentally become an official card trigger",Rejects(()=>SkillRules.Card("invalid",AbilityEventKind.MarkBurst,AbilityEffects.Block(1))));
                definition.Rules=new[]{SkillRules.OnUse("unfinished")};
                Check("unfinished draft is still rejected by the original validator",registry.Validate(definition).Contains("OPERATIONS_REQUIRED"));
                Check("negative cooldown rejected before creating gameplay data",Rejects(()=>SkillRules.OnUse("bad").WithCooldown(-1)));
                Check("empty condition group rejected instead of changing its meaning",Rejects(()=>AbilityConditions.All()));
            });
            Case("custom and advanced authoring",()=>
            {
                registry.RegisterOperation("verify.authoring.effect",new ProbeHandler());
                registry.RegisterSelector("verify.authoring.target",view=>Array.Empty<CombatTargetRef>());
                registry.RegisterPredicate("verify.authoring.condition",data=>new ProbeCondition());
                var definition=SkillRules.CreateDraft(source,"fixture-guid");owned.Add(definition);
                var operation=AbilityEffects.Custom("verify.authoring.effect",amount:17,key:"custom-key");
                operation.StateScope=AbilityOwnerScope.WeaponInstance;operation.Duration=4;operation.SecondaryAmount=9;
                var rule=SkillRules.OnInternalEvent("custom",AbilityEventKind.CounterCompleted,operation)
                    .To("verify.authoring.target")
                    .When(AbilityConditions.All(AbilityConditions.Custom("verify.authoring.condition"),
                        AbilityConditions.Not(AbilityConditions.RollFailed())))
                    .AtSnapshot(AbilitySnapshotPolicy.LockAtReactionStart).OncePerTurn().WithCooldown(2);
                rule.Phase=7;rule.Priority=-3;rule.Policy.OwnerScope=AbilityOwnerScope.CounterExecution;
                rule.Policy.CausePolicy=AbilityCausePolicy.DerivedEvent;rule.Lifetime.OncePerCounterExecution=true;
                definition.Rules=new[]{rule};
                Check("custom handler, predicate and selector need no engine changes",registry.Validate(definition).Count==0);
                Check("facade returns the original data type with all advanced fields accessible",rule.GetType()==typeof(AbilityRuleData)&&
                    rule.Operations[0]==operation&&rule.Phase==7&&rule.Priority==-3&&rule.Lifetime.FullCooldownTurns==2&&
                    new AbilityOperationSpec(operation).SecondaryAmount==9&&operation.StateScope==AbilityOwnerScope.WeaponInstance);
                Check("custom condition tree uses the original compiler",registry.Compile(rule.Condition)!=null);
                rule.Operations[0]=AbilityEffects.Custom("verify.authoring.unknown");
                Check("unknown operations still fail existing validation",registry.Validate(definition).Count>0);
            });
            Case("after-hit semantics",()=>
            {
                var definition=SkillRules.CreateDraft(source,"fixture-guid");owned.Add(definition);
                definition.Rules=new[]{
                    SkillRules.AfterUsingHit("use",AbilityConditions.OpenedWindow(),AbilityEffects.Energy(1)),
                    SkillRules.AfterWinningHit("win",AbilityConditions.All(AbilityConditions.AppliedHpAtLeast(6),
                        AbilityConditions.PartWeakenedOrBroken()),AbilityEffects.Block(1))};
                Check("both original use and original win retain committed-result contracts",registry.Validate(definition).Count==0&&
                    definition.Rules[0].Policy.DamageConditionOrigin==AbilityDamageConditionOrigin.RollUsing&&
                    definition.Rules[1].Policy.DamageConditionOrigin==AbilityDamageConditionOrigin.ExchangeWon&&
                    definition.Rules.All(r=>!r.Policy.CardAuthored&&r.RequiresDuelGate&&
                        r.Policy.Snapshot==AbilitySnapshotPolicy.AfterCommittedDamage&&r.Policy.ConditionPhase==CombatConditionPhase.PostDamageResult));
                var description=AbilityRuleSummary.Describe(definition.Rules[1]);
                Check("Inspector explains winning original hit, actual HP and transition",description.Contains("승리한 원래 타격")&&
                    description.Contains("실제 HP 피해 ≥ 6")&&description.Contains("이번 타격으로 약화"));
            });
            Case("asset persistence",()=>
            {
                string folder="Assets/1. Scripts/Editor/Verification/__SkillAuthoring_"+Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets/1. Scripts/Editor/Verification",Path.GetFileName(folder));
                try
                {
                    var savedSource=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(savedSource,folder+"/Skill.asset");
                    var savedSettings=UnityEngine.Object.Instantiate(settings);AssetDatabase.CreateAsset(savedSettings,folder+"/Settings.asset");
                    var definition=EngravedContractRules.Create(savedSource,AssetDatabase.AssetPathToGUID(folder+"/Skill.asset"),
                        savedSettings,out var reservation);
                    AbilityRuleSetEditor.SaveNewDraft(definition,folder+"/Rules.asset");
                    Check("save helper persists reservation in the same asset",AssetDatabase.IsSubAsset(reservation)&&
                        AssetDatabase.GetAssetPath(reservation)==folder+"/Rules.asset");
                    Resources.UnloadAsset(reservation);Resources.UnloadAsset(definition);
                    AssetDatabase.ImportAsset(folder+"/Rules.asset",ImportAssetOptions.ForceUpdate);
                    var loaded=AssetDatabase.LoadAssetAtPath<AbilityRuleSetDefinition>(folder+"/Rules.asset");
                    var saved=(AbilityScheduleDefinition)loaded.Rules[2].Operations[0].Settings;
                    Check("reload preserves complete schedule link and delivery policy",saved!=null&&saved.Definition==loaded&&
                        saved.RuleId==loaded.Rules[3].RuleId&&loaded.Rules[3].ScheduledOnly&&
                        loaded.Rules[3].Policy.Snapshot==AbilitySnapshotPolicy.LockAtScheduleCreation);
                    Check("saved draft passes actual source GUID and schema validation",AbilityRuleSetEditor.ValidateAsset(loaded,registry).Length==0);
                    Check("saved graph still has a nested HP AND transition condition",loaded.Rules[2].Condition.Kind==CombatConditionNodeKind.All&&
                        loaded.Rules[2].Condition.Children[1].Kind==CombatConditionNodeKind.Any&&loaded.Rules[2].Condition.Children[1].Children.Length==2);
                    var other=SkillRules.CreateDraft(savedSource,AssetDatabase.AssetPathToGUID(folder+"/Skill.asset"));owned.Add(other);
                    Check("save helper refuses to overwrite an existing asset",Rejects(()=>AbilityRuleSetEditor.SaveNewDraft(other,folder+"/Rules.asset"))&&
                        AssetDatabase.LoadAssetAtPath<AbilityRuleSetDefinition>(folder+"/Rules.asset")==loaded);
                    var reward=SkillRules.NextTurnReward(other,"missing",AbilityEffects.Energy(1));owned.Add(reward.Reservation);
                    other.Rules=new[]{SkillRules.OnUse("reserve",reward.Schedule)};
                    Check("forgotten delivery rejected before saving a dangling reference",Rejects(()=>AbilityRuleSetEditor.SaveNewDraft(other,folder+"/Invalid.asset"))&&
                        !File.Exists(folder+"/Invalid.asset"));
                }
                finally{AssetDatabase.DeleteAsset(folder);}
            });
        }
        finally{foreach(var item in owned.AsEnumerable().Reverse())if(item!=null)UnityEngine.Object.DestroyImmediate(item);}
        LastReport="PASS="+passed+" FAIL="+failed+"\n"+string.Join("\n",lines);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SkillRuleAuthoringVerification.txt",LastReport);
        if(failed>0)Debug.LogError(LastReport);else Debug.Log(LastReport);
    }

    private sealed class ProbeCondition : ICombatCondition
    {
        public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef target)=>true;
    }
    private sealed class ProbeHandler : TargetedAbilityRuleHandler
    {
        public override CombatCommandReceipt Commit(AbilityRuleCommitContext context,AbilityRuleCommand command)=>
            CombatCommandReceipt.Reject("Authoring-validation probe is never executed");
    }

    // Frozen pre-facade recipe: compare every serialized rule field, including defaults and array order.
    // Existing EngravedContract verification separately executes the new graph through real combat services.
    private static AbilityRuleSetDefinition LegacyGraph(SkillDefinition source,EngravedContractSettings settings,
        out AbilityScheduleDefinition reservation,int block,int hp,int energy)
    {
        var definition=ScriptableObject.CreateInstance<AbilityRuleSetDefinition>();
        definition.Enabled=false;definition.SourceKind=AbilityRuleSourceKind.Skill;definition.StableId=source.SkillId;
        definition.SourceAssetGuid="fixture-guid";definition.SourceAssetName=source.name;
        reservation=ScriptableObject.CreateInstance<AbilityScheduleDefinition>();reservation.Definition=definition;
        reservation.RuleId=EngravedContractRules.DeliveryRule;
        var reserve=AbilityRuleAuthoringTemplates.AfterDamageEvaluation(EngravedContractRules.ReserveRule,AbilityDamageConditionOrigin.ExchangeWon,
            new AbilityOperationData{TypeId="schedule.next_turn",Settings=reservation});
        reserve.Condition=new AbilityConditionData{Kind=CombatConditionNodeKind.All,Children=new[]{
            new AbilityConditionData{Predicate=new AbilityPredicateData{TypeId="damage.hp_at_least",Value=hp}},reserve.Condition}};
        reserve.Lifetime.OncePerSkillExecution=true;reserve.Lifetime.ConsumeOnSchedule=true;
        definition.Rules=new[]{
            new AbilityRuleData{RuleId=EngravedContractRules.MarkRule,Trigger=AbilityEventKind.ExchangeWon,RequiresDuelGate=true,TargetSelectorId="actual",
                Policy=new AbilityRuleMetadata{Snapshot=AbilitySnapshotPolicy.LockAtActionStart},
                Operations=new[]{new AbilityOperationData{TypeId=EngravedContractModule.MarkOperation,Settings=settings}}},
            new AbilityRuleData{RuleId=EngravedContractRules.BurstRule,Trigger=AbilityEventKind.MarkBurst,RequiresDuelGate=true,TargetSelectorId="self",
                Policy=new AbilityRuleMetadata{CardAuthored=false,Snapshot=AbilitySnapshotPolicy.LiveAtTrigger},
                Condition=new AbilityConditionData{Predicate=new AbilityPredicateData{TypeId="cause.direct",Key=source.SkillId}},
                Lifetime=new AbilityRuleLifetime{OncePerSkillExecution=true},Operations=new[]{new AbilityOperationData{TypeId="resource.block",Amount=block}}},
            reserve,
            new AbilityRuleData{RuleId=EngravedContractRules.DeliveryRule,Trigger=AbilityEventKind.TurnStarting,ScheduledOnly=true,RequiresDuelGate=true,TargetSelectorId="self",
                Policy=new AbilityRuleMetadata{CardAuthored=false,Snapshot=AbilitySnapshotPolicy.LockAtScheduleCreation},
                Operations=new[]{new AbilityOperationData{TypeId="resource.energy",Amount=energy}}}
        };
        return definition;
    }
}
#endif
