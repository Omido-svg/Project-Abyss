#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectAbyss.Abilities;
using ProjectAbyss.Examples;

public static class EngravedContractExample
{
    public static string LastReport { get; private set; }
    public static string LastTrace { get; private set; }

    public static SkillDefinition CreateSource()
    {
        var source=ScriptableObject.CreateInstance<SkillDefinition>();
        source.name="EngravedContractSkill";source.SkillName="각인의 계약 (교육용)";
        source.ActionType=ActionType.Duel;source.Color=SkillColor.Red;source.ExchangeRollCount=3;
        source.OverrideEnergyCost=true;source.EnergyCost=2;source.BasePowerRule=SkillBasePowerRule.CanonicalCurve;
        var serialized=new SerializedObject(source);serialized.FindProperty("skillId").stringValue=EngravedContractRules.StableId;
        serialized.ApplyModifiedPropertiesWithoutUndo();return source;
    }

    [MenuItem("Tools/Project Abyss/Examples/Create Engraved Contract Assets")]
    public static void CreateAssets()
    {
        string path=EditorUtility.SaveFilePanelInProject("각인의 계약: 비활성 교육용 자산", "EngravedContractRules", "asset",
            "스킬·표식 설정·규칙과 예약 설정을 생성합니다. 규칙은 비활성 상태로 생성됩니다.");
        if(string.IsNullOrEmpty(path))return;
        var created=new List<string>();
        try
        {
            string folder=Path.GetDirectoryName(path).Replace('\\','/');
            var source=CreateSource();string skillPath=AssetDatabase.GenerateUniqueAssetPath(folder+"/EngravedContractSkill.asset");
            AssetDatabase.CreateAsset(source,skillPath);created.Add(skillPath);
            var settings=ScriptableObject.CreateInstance<EngravedContractSettings>();
            string settingsPath=AssetDatabase.GenerateUniqueAssetPath(folder+"/EngravedContractSettings.asset");
            AssetDatabase.CreateAsset(settings,settingsPath);created.Add(settingsPath);
            var rules=EngravedContractRules.Create(source,AssetDatabase.AssetPathToGUID(skillPath),settings,out var reservation);
            AbilityRuleSetEditor.SaveNewDraft(rules,path);created.Add(path);Selection.activeObject=rules;
            Debug.Log("[각인의 계약] 비활성 예시 자산 생성. 확정 수치가 아닌 교육용 값입니다: "+path);
        }
        catch
        {
            foreach(var item in created.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(item);
            throw;
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly BattleContext Battle;
        public readonly List<UnityEngine.Object> Objects=new();
        public readonly Yujin Player,Enemy;
        public readonly SkillDefinition Source;
        public readonly EngravedContractSettings Settings;
        public AbilityRuleSetDefinition Rules;
        public BodyPart Part=>Enemy.GetBodyPart(PartType.LEFT_HAND);
        private readonly UnityEngine.SceneManagement.Scene scene;
        private readonly UnityEngine.Random.State random;
        private long nextId=700000;
        public Fixture()
        {
            random=UnityEngine.Random.state;UnityEngine.Random.InitState(20261117);scene=EditorSceneManager.NewPreviewScene();
            Battle=new BattleContext{SuppressPresentation=true,RunProgression=new RunProgressionState{Seed=20261117}};
            Battle.Services=new BattleRuntimeServices();Battle.EffectResolver=new BattleEffectResolver(Battle);
            Battle.Services.MomentumManager=new MomentumManager(Battle);Battle.Services.DamageManager=new DamageManager(Battle,Battle.Services.MomentumManager);
            Battle.Services.ActionManager=new ActionManager();Player=Spawn();Enemy=Spawn();Battle.Player=Player;Battle.Enemies.Add(Enemy);
            Source=CreateSource();Objects.Add(Source);Settings=ScriptableObject.CreateInstance<EngravedContractSettings>();Objects.Add(Settings);
        }
        private Yujin Spawn()
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2. Data/TestEncounters/Prefabs/Yujin_Test.prefab"));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);Objects.Add(go);
            var actor=go.GetComponent<Yujin>();var data=UnityEngine.Object.Instantiate(actor.Data);Objects.Add(data);
            actor.ConfigureAuthoringCore(data,actor.Data.CombatLoadout);actor.Initialize(Battle);return actor;
        }
        public void Register()
        {
            Rules=EngravedContractRules.Create(Source,"example-fixture-guid",Settings,out var reservation);
            Objects.Add(reservation);Objects.Add(Rules);
            // Explicit registration only in this isolated fixture. Auto-loading respects Enabled=false.
            Battle.Abilities.Register(Rules);
        }
        public BattleAction Action(bool enemy=false,bool opponentDuel=true)
        {
            Character owner=enemy?Enemy:Player,target=enemy?Player:Enemy;SkillDefinition definition=Source;
            if(enemy)
            {
                definition=ScriptableObject.CreateInstance<SkillDefinition>();Objects.Add(definition);
                definition.name="ExampleOpponent";definition.ActionType=opponentDuel?ActionType.Duel:ActionType.NormalAttack;
                definition.Color=SkillColor.Red;definition.ExchangeRollCount=3;definition.OverrideEnergyCost=true;definition.EnergyCost=0;
            }
            Skill skill=definition.ActionType==ActionType.Duel?(Skill)new DataDuelSkill(definition):new DataNormalSkill(definition);
            skill.Initialize(owner,Battle._battleEvent);
            var action=new BattleAction{Slot=new ActionSlot{ActionId=++nextId,Owner=owner,Part=owner.GetBodyPart(PartType.LEFT_HAND),Skill=skill,
                TargetCharacter=target,TargetPart=target.GetBodyPart(PartType.LEFT_HAND),SlotId="example-"+nextId}};
            action.SetDuelEffectsEligible(true);return action;
        }
        public void Win(BattleAction action,BattleAction opponent,int roll)
        {
            action.CurrentRollIndex=roll;action.Skill.NotifyRollUsing(action,opponent,roll,true,false);
            action.Skill.NotifyRollOutcome(action,opponent,roll,true,true,false);
        }
        public DamageContext Damage(BattleAction action,int power=10)
        {action.RolledPower=power;return Battle.Services.DamageManager.ApplyDamageContext(DamageRequest.FromAction(action,DamageType.SkillPart,false,true,true));}
        public int Committed(string id)=>Battle.Abilities.Trace.Count(t=>t.Request.CommandId.Contains(":"+id+":")&&t.Receipt.EffectCommitted);
        public int Scheduled=>Battle.Abilities.Trace.Count(t=>t.Receipt.Outcome==CombatCommandOutcome.Scheduled);
        public void Dispose()
        {
            Battle.DisposeAbilities();foreach(var item in Objects.AsEnumerable().Reverse())if(item!=null)UnityEngine.Object.DestroyImmediate(item);
            EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Random.state=random;
        }
    }

    [MenuItem("Tools/Project Abyss/Examples/Verify Engraved Contract")]
    public static void RunVerification()
    {
        int passed=0,failed=0;var lines=new List<string>();var trace=new List<string>();
        void Check(string id,bool ok){lines.Add((ok?"PASS ":"FAIL ")+id);if(ok)passed++;else failed++;}
        void Case(string name,Action body){try{body();}catch(Exception e){Check(name+": "+e.GetBaseException().Message,false);}}
        Case("authoring",()=>
        {
            using var f=new Fixture();f.Register();
            Check("rule graph validates",f.Battle.Abilities.Registry.Validate(f.Rules).Count==0);
            Check("three rolls red duel with example cost two",f.Source.ActionType==ActionType.Duel&&f.Source.Color==SkillColor.Red&&f.Source.ExchangeRollCount==3&&f.Source.EnergyCost==2);
            Check("assets are disabled by default",!f.Rules.Enabled);
            Check("custom operation automatically registered",f.Battle.Abilities.Registry.Operation(EngravedContractModule.MarkOperation)!=null);
        });
        Case("serialized authoring",()=>
        {
            using var f=new Fixture();string folder="Assets/__EngravedContractVerification_"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
            try
            {
                var source=UnityEngine.Object.Instantiate(f.Source);AssetDatabase.CreateAsset(source,folder+"/Skill.asset");
                var settings=UnityEngine.Object.Instantiate(f.Settings);AssetDatabase.CreateAsset(settings,folder+"/Settings.asset");
                var rules=EngravedContractRules.Create(source,AssetDatabase.AssetPathToGUID(folder+"/Skill.asset"),settings,out var reservation);
                var nested=new AbilityConditionData();
                for(int i=0;i<14;i++)nested=new AbilityConditionData{Kind=CombatConditionNodeKind.Not,Children=new[]{nested}};
                rules.Rules[0].Condition=nested;
                AssetDatabase.CreateAsset(rules,folder+"/Rules.asset");AssetDatabase.AddObjectToAsset(reservation,rules);EditorUtility.SetDirty(reservation);AssetDatabase.SaveAssetIfDirty(reservation);EditorUtility.SetDirty(rules);AssetDatabase.SaveAssetIfDirty(rules);
                Check("settings have a Unity script binding",MonoScript.FromScriptableObject(settings)?.GetClass()==typeof(EngravedContractSettings));
                Resources.UnloadAsset(reservation);Resources.UnloadAsset(rules);AssetDatabase.ImportAsset(folder+"/Rules.asset",ImportAssetOptions.ForceUpdate);var loaded=AssetDatabase.LoadAssetAtPath<AbilityRuleSetDefinition>(folder+"/Rules.asset");
                var saved=(AbilityScheduleDefinition)loaded.Rules.Single(r=>r.RuleId==EngravedContractRules.ReserveRule).Operations[0].Settings;
                Check("reservation subasset survives reload and points at its graph",saved!=null&&saved.Definition==loaded&&saved.RuleId==EngravedContractRules.DeliveryRule);
                Check("deep condition tree survives serialization without flattening",f.Battle.Abilities.Registry.Compile(loaded.Rules[0].Condition).Evaluate(null,default));
                var issues=AbilityRuleSetEditor.ValidateAsset(loaded,f.Battle.Abilities.Registry);
                Check("serialized example passes source and schema validation / "+string.Join(";",issues),issues.Length==0);
            }
            finally{AssetDatabase.DeleteAsset(folder);}
        });
        Case("start snapshot",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();var opponent=f.Action(true);f.Battle._battleEvent.RaiseActionStart(action);
            f.Settings.MarkWithoutStartMark=30;
            for(int i=0;i<3;i++)f.Win(action,opponent,i);
            Check("unmarked start branch stays four on later rolls",f.Player.YujinMechanic.GetMark(f.Enemy,f.Part)==12);
            Check("settings mutation cannot change compiled plan",f.Battle.Abilities.Trace.Where(t=>t.Request.CommandId.Contains(":"+EngravedContractRules.MarkRule+":")).All(t=>t.Request.RequestedAmount==4));
            int before=f.Player.YujinMechanic.GetMark(f.Enemy,f.Part);f.Win(action,opponent,2);
            Check("same roll replay does not add marks",f.Player.YujinMechanic.GetMark(f.Enemy,f.Part)==before);
        });
        Case("own burst",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();var opponent=f.Action(true);
            f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,40);f.Battle._battleEvent.RaiseActionStart(action);f.Win(action,opponent,0);
            Check("marked start grants eight and preserves overflow four",f.Player.YujinMechanic.GetMark(f.Enemy,f.Part)==4);
            Check("own direct burst grants block three",f.Player.RuntimeStatus.currentBlock==3&&f.Committed(EngravedContractRules.BurstRule)==1);
            f.Player.YujinMechanic.TryConsumeMark(f.Enemy,f.Part,4);f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,40);f.Win(action,opponent,1);
            Check("second own burst in same skill does not repeat reward",f.Player.RuntimeStatus.currentBlock==3&&f.Committed(EngravedContractRules.BurstRule)==1);
        });
        Case("derived burst",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();f.Battle._battleEvent.RaiseActionStart(action);
            f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,40);
            var cause=action.GetEffectCause(AbilityEventKind.RollUsing,0).Derive(f.Battle.EventSequence.Next(),EffectOriginKind.Trap);
            f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,8,action,cause);f.Battle.Abilities.DrainEffects();
            Check("trap-derived burst is not this card's direct reward",f.Committed(EngravedContractRules.BurstRule)==0&&f.Player.RuntimeStatus.currentBlock==0);
        });
        Case("reservation",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();f.Battle._battleEvent.RaiseActionStart(action);
            f.Player.TryConsumeEnergy(f.Player.CurrentEnergy);f.Part.SetDebugState(5,100,false,false);
            var damage=f.Damage(action);Check("real damage crosses the weakening transition",damage.AppliedHpDamage>=6&&damage.WasWeakened);
            Check("reservation queued but energy not delivered yet",f.Scheduled==1&&f.Battle.TurnStartEffects.Count==1&&f.Player.CurrentEnergy==0);
            action.CurrentRollIndex=1;var head=f.Enemy.GetBodyPart(PartType.HEAD);head.SetDebugState(5,100,false,false);action.Slot.TargetPart=head;f.Damage(action);
            Check("schedule consumes the skill-scoped use immediately",f.Scheduled==1&&f.Battle.TurnStartEffects.Count==1);
            f.Battle._battleEvent.RaiseActionEnd(action);f.Battle.Abilities.BeginTurn(2);f.Battle.TurnStartEffects.Drain();
            Check("reservation survives action-end cleanup",f.Player.CurrentEnergy==1&&f.Committed(EngravedContractRules.DeliveryRule)==1);
            f.Battle.TurnStartEffects.Drain();Check("repeated turn-start drain cannot redeliver",f.Player.CurrentEnergy==1);
        });
        Case("failed reservation conditions",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();f.Battle._battleEvent.RaiseActionStart(action);f.Part.SetDebugState(100,100,false,false);f.Damage(action);
            Check("HP damage without transition does not reserve",f.Battle.TurnStartEffects.Count==0);
            f.Part.SetDebugState(3,100,false,false);var low=f.Damage(action,2);
            Check("transition below HP threshold does not reserve",low.WasWeakened&&low.AppliedHpDamage<6&&f.Battle.TurnStartEffects.Count==0);
        });
        Case("dead recipient",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();f.Battle._battleEvent.RaiseActionStart(action);f.Part.SetDebugState(5,100,false,false);f.Damage(action);
            f.Battle.Services.DamageManager.ApplyDamageContext(DamageRequest.Custom(DamageType.True,null,f.Player,null,99999,1f,false,false,false));
            f.Battle.Abilities.BeginTurn(2);f.Battle.TurnStartEffects.Drain();
            Check("dead reserved recipient cancels delivery",f.Player.IsDead&&f.Committed(EngravedContractRules.DeliveryRule)==0&&f.Battle.TurnStartEffects.ExpiredCount==1);
        });
        foreach(bool opponentIsDuel in new[]{true,false})Case("production pair "+opponentIsDuel,()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();var opponent=f.Action(true,opponentIsDuel);
            f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,40);f.Part.SetDebugState(5,100,false,false);
            f.Battle.Abilities.Modifiers.Add(new AbilityScopeKey(AbilityOwnerScope.Actor,LiveSlotQuery.ActorRuntimeId(f.Player)),
                new CombatModifierSpec(CombatModifierStage.RollPower,CombatModifierKind.OverrideFinal,10,0,0),action.GetEffectCause(AbilityEventKind.RollUsing,0),1);
            f.Battle.Abilities.Modifiers.Add(new AbilityScopeKey(AbilityOwnerScope.Actor,LiveSlotQuery.ActorRuntimeId(f.Enemy)),
                new CombatModifierSpec(CombatModifierStage.RollPower,CombatModifierKind.OverrideFinal,1,0,0),opponent.GetEffectCause(AbilityEventKind.RollUsing,0),1);
            var manager=new ClashManager(f.Battle,f.Battle.Services.DamageManager,f.Battle.Services.MomentumManager);
            var result=manager.ResolvePair(new ClashPair(action.Slot,opponent.Slot));
            Check("production pair actually applies damage / "+opponentIsDuel,result.Exchanges.Any(e=>e.DamageContext?.AppliedHpDamage>0));
            Check("production mark rule obeys duel gate / "+opponentIsDuel,opponentIsDuel?f.Committed(EngravedContractRules.MarkRule)>0:f.Committed(EngravedContractRules.MarkRule)==0);
            Check("production burst reward obeys duel gate / "+opponentIsDuel,f.Committed(EngravedContractRules.BurstRule)==(opponentIsDuel?1:0));
            Check("production reservation obeys duel gate / "+opponentIsDuel,f.Scheduled==(opponentIsDuel?1:0));
            if(opponentIsDuel)
            {
                f.Player.TryConsumeEnergy(f.Player.CurrentEnergy);f.Battle.Abilities.BeginTurn(2);f.Battle.TurnStartEffects.Drain();
                var delivered=f.Battle.Abilities.Trace.Last(t=>t.Request.CommandId.Contains(":"+EngravedContractRules.DeliveryRule+":"));
                Check("scheduled delivery has a new execution and original causal root",delivered.Request.ActionId!=action.ActionId&&delivered.Request.SourceCause.RootActionId==action.ActionId&&f.Player.CurrentEnergy==1);
                foreach(var record in f.Battle.Abilities.Trace)
                    trace.Add(record.Request.CommandId+" | "+record.Receipt.Outcome+" | requested="+record.Request.RequestedAmount+" | applied="+record.Receipt.AppliedAmount+
                        " | resource="+record.Receipt.Values.ResourceBefore+"->"+record.Receipt.Values.ResourceAfter+
                        " | root="+record.Request.SourceCause.RootActionId+" | event="+record.Request.SourceCause.EventId+" | parent="+record.Request.SourceCause.ParentEventId);
            }
        });
        LastReport="PASS="+passed+" FAIL="+failed+"\n"+string.Join("\n",lines);LastTrace=string.Join("\n",trace);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EngravedContractExampleVerification.txt",LastReport);
        File.WriteAllText("Logs/EngravedContractExampleTrace.txt",LastTrace);
        if(failed>0)Debug.LogError(LastReport);else Debug.Log(LastReport);
    }
}
#endif
