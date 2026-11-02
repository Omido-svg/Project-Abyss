#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// R1 follow-up live PlayMode probes using the G0 temporary combat fixture.
/// No scene or ScriptableObject is saved; all created definitions are destroyed.
/// </summary>
public static class R1ExtendedLiveTimingVerification20261102
{
    [Serializable] private sealed class Report
    {
        public string GeneratedAtUtc;
        public int Pass,Fail;
        public string[] Failures;
    }
    private static readonly BindingFlags PrivateStatic=
        BindingFlags.NonPublic|BindingFlags.Static;
    private static readonly BindingFlags PublicInstance=
        BindingFlags.Public|BindingFlags.Instance;

    private static int Counter(object scenario,string label,bool first=true)
    {
        var list=(System.Collections.IEnumerable)scenario.GetType()
            .GetField("ProbeCounts",PublicInstance).GetValue(scenario);
        foreach(object item in list)
            if((string)item.GetType().GetField("Label",PublicInstance).GetValue(item)==label)
                return (int)item.GetType().GetField(
                    first?"FirstOwnerCount":"SecondOwnerCount",PublicInstance).GetValue(item);
        return -1;
    }
    private static int Number(object scenario,string property)=>
        (int)scenario.GetType().GetField(property,PublicInstance).GetValue(scenario);
    private static object CombatScenario(
        BattleManager manager,Character enemy,string id,int firstRolls,int secondRolls,
        ActionType firstType,ActionType secondType,bool oneSided,
        List<SkillDefinition> definitions,List<SkillEffectDefinition> effects)
    {
        var method=typeof(G0AbilityAuditReport20261029).GetMethod("RunScenario",PrivateStatic);
        if(method==null)throw new InvalidOperationException("G0 temporary fixture runner not found.");
        return method.Invoke(null,new object[]{
            manager,enemy,id,id,firstRolls,secondRolls,firstType,secondType,
            10,1,oneSided,0,definitions,effects
        });
    }

    private static SkillEffectEntry Probe(string key,SkillEffectTiming timing,
        List<SkillEffectDefinition> effects)
    {
        var so=ScriptableObject.CreateInstance<G0ProbeEffectDefinition>();
        so.name="R1_PROBE_"+key;so.ProbeLabel=key;
        effects.Add(so);
        return new SkillEffectEntry{
            Definition=so,OverrideTiming=true,Timing=timing,
            Conditions=new List<SkillEffectCondition>(),
            RestrictToRoll=false,RollNumber=1
        };
    }

    private static SkillDefinition Definition(
        ActionType type,IEnumerable<SkillEffectEntry> entries,
        List<SkillDefinition> definitions)
    {
        var d=ScriptableObject.CreateInstance<SkillDefinition>();
        d.name="R1_RUNTIME_ONLY_"+type;
        d.SkillName=d.name;d.ActionType=type;
        d.ResolverType=SkillResolverType.Dice;
        d.ExchangeRollCount=1;
        d.Color=SkillColor.Red;
        d.EffectEntries=entries.ToList();
        d.Rolls=new List<SkillRollData>();
        definitions.Add(d);
        return d;
    }

    [MenuItem("Game System Verification/R1 - Extended Live Timing (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)
            throw new InvalidOperationException("Run R1 extended timing in Play Mode.");
        var manager=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var context=manager?.BattleContext;
        Character enemy=context?.Enemies?.FirstOrDefault(c=>c!=null && !c.IsDead);
        if(manager==null||context?.Player==null||enemy==null)
            throw new InvalidOperationException("Initialized BattleManager, Player, enemy required.");

        var createdDefinitions=new List<SkillDefinition>();
        var createdEffects=new List<SkillEffectDefinition>();
        var errors=new List<string>();
        int pass=0;
        void Check(string id,bool valid,string detail)
        {
            if(valid){pass++;Debug.Log("[R1-Live] PASS "+id+" | "+detail);}
            else{errors.Add(id+": "+detail);Debug.LogError("[R1-Live] FAIL "+id+" | "+detail);}
        }

        try
        {
            object full=CombatScenario(manager,enemy,"R1-FIVE-FULL",5,5,
                ActionType.NormalAttack,ActionType.NormalAttack,
                false,createdDefinitions,createdEffects);
            Check("L01 5 real opposed rolls, exactly 5 use events",
                Counter(full,"OnExecute")==5 &&
                Counter(full,"OnExchangeWin")==5 &&
                Counter(full,"OnClashEnd")==1,
                "use="+Counter(full,"OnExecute")+" win="+Counter(full,"OnExchangeWin")+
                " end="+Counter(full,"OnClashEnd"));

            object tail=CombatScenario(manager,enemy,"R1-FIVE-TAIL",5,1,
                ActionType.Duel,ActionType.Duel,false,
                createdDefinitions,createdEffects);
            Check("L02 eligible duel keeps use effects across one-sided tail",
                Counter(tail,"OnExecute")==5 &&
                Counter(tail,"OnExchangeWin")==1 &&
                Counter(tail,"OnClashEnd")==1,
                "use="+Counter(tail,"OnExecute")+" wins="+Counter(tail,"OnExchangeWin")+
                " end="+Counter(tail,"OnClashEnd"));

            object duelSolo=CombatScenario(manager,enemy,"R1-DUEL-SOLO",3,0,
                ActionType.Duel,ActionType.NormalAttack,true,
                createdDefinitions,createdEffects);
            Check("L03 unmatched duel uses no card effects",
                Counter(duelSolo,"OnExecute")==0 &&
                Counter(duelSolo,"OnExchangeWin")==0 &&
                Counter(duelSolo,"OnClashEnd")==0,
                "use="+Counter(duelSolo,"OnExecute")+
                " win="+Counter(duelSolo,"OnExchangeWin"));

            object normalSolo=CombatScenario(manager,enemy,"R1-NORMAL-SOLO",3,0,
                ActionType.NormalAttack,ActionType.NormalAttack,true,
                createdDefinitions,createdEffects);
            Check("L04 ordinary one-sided roll cannot publish win event",
                Counter(normalSolo,"OnExecute")==3 &&
                Counter(normalSolo,"OnExchangeWin")==0 &&
                Counter(normalSolo,"OnExchangeLose")==0,
                "use="+Counter(normalSolo,"OnExecute")+
                " win="+Counter(normalSolo,"OnExchangeWin")+
                " lose="+Counter(normalSolo,"OnExchangeLose"));

            // This temporary skill's global BattleStarted subscriber is
            // intentionally fired only on the isolated fixture BattleEvent.
            using(var fixture=GameSystemVerificationFixture.Create(
                context.Player,enemy,manager.BattleRules))
            {
                CharacterVerificationScenarioTools.ResetCombatState(fixture.Player);
                CharacterVerificationScenarioTools.ResetCombatState(fixture.Enemy);
                G0ProbeEffectDefinition.ResetCounters();
                var d=Definition(ActionType.NormalAttack,new[]{
                    Probe("R1_GLOBAL",SkillEffectTiming.OnBattleStart,createdEffects),
                    Probe("R1_CARD",SkillEffectTiming.OnAttackResolutionStarting,createdEffects),
                    Probe("R1_USE",SkillEffectTiming.OnExecute,createdEffects)
                },createdDefinitions);
                var skill=d.CreateRuntimeSkill();
                skill.Initialize(fixture.Player,fixture.Context._battleEvent);
                skill.Register();
                try
                {
                    var slot=new ActionSlot{
                        ActionId=717100,Owner=fixture.Player,
                        TargetCharacter=fixture.Enemy,
                        Part=fixture.Player.BodyParts.FirstOrDefault(),
                        TargetPart=fixture.Enemy.BodyParts.FirstOrDefault(),
                        Skill=skill,Phase=ActionPhase.COMBAT,
                        SlotId="R1-TIMING-PROBE",ResourceCostCommitted=true
                    };
                    var a=new BattleAction{Slot=slot};
                    fixture.Context._battleEvent.RaiseBattleStarted();
                    skill.NotifyAttackResolutionStarting(a,null,false,true);
                    skill.NotifyRollUsing(a,null,0,false,true);
                    skill.NotifyRollUsing(a,null,1,false,true);
                    Check("L05 global BattleStarted one, independent card start one",
                        G0ProbeEffectDefinition.GetCount("R1_GLOBAL")==1 &&
                        G0ProbeEffectDefinition.GetCount("R1_CARD")==1 &&
                        G0ProbeEffectDefinition.GetCount("R1_USE")==2,
                        "battle="+G0ProbeEffectDefinition.GetCount("R1_GLOBAL")+
                        " card="+G0ProbeEffectDefinition.GetCount("R1_CARD")+
                        " use="+G0ProbeEffectDefinition.GetCount("R1_USE"));

                    var next=new BattleAction{Slot=new ActionSlot{
                        ActionId=717101,Owner=fixture.Player,
                        TargetCharacter=fixture.Enemy,Skill=skill,Phase=ActionPhase.COMBAT,
                        SlotId="R1-SECOND-CARD",ResourceCostCommitted=true
                    }};
                    skill.NotifyAttackResolutionStarting(next,null,false,true);
                    Check("L06 next card starts independently of global battle",
                        G0ProbeEffectDefinition.GetCount("R1_GLOBAL")==1 &&
                        G0ProbeEffectDefinition.GetCount("R1_CARD")==2,
                        "battle="+G0ProbeEffectDefinition.GetCount("R1_GLOBAL")+
                        " card="+G0ProbeEffectDefinition.GetCount("R1_CARD"));
                }
                finally{skill.Unregister();}
            }

            using(var fixture=GameSystemVerificationFixture.Create(
                context.Player,enemy,manager.BattleRules))
            {
                G0ProbeEffectDefinition.ResetCounters();
                var d=Definition(ActionType.Preparation,new[]{
                    Probe("R1_PREP",SkillEffectTiming.OnPreparationActivated,createdEffects),
                    Probe("R1_PREP_LEGACY",SkillEffectTiming.OnExecute,createdEffects),
                    Probe("R1_PREP_WIN",SkillEffectTiming.OnExchangeWin,createdEffects)
                },createdDefinitions);
                var skill=d.CreateRuntimeSkill();
                skill.Initialize(fixture.Player,fixture.Context._battleEvent);
                var a=new BattleAction{Slot=new ActionSlot{
                    ActionId=717120,Owner=fixture.Player,Skill=skill,
                    TargetCharacter=fixture.Enemy,
                    Phase=ActionPhase.FORESIGHT,SlotId="R1-PREPARATION",
                    ResourceCostCommitted=true
                }};
                skill.Execute(a);
                Check("L07 preparation activates exactly once without a roll",
                    G0ProbeEffectDefinition.GetCount("R1_PREP")==1 &&
                    G0ProbeEffectDefinition.GetCount("R1_PREP_LEGACY")==1 &&
                    G0ProbeEffectDefinition.GetCount("R1_PREP_WIN")==0,
                    "prep="+G0ProbeEffectDefinition.GetCount("R1_PREP")+
                    " compatibility-use="+G0ProbeEffectDefinition.GetCount("R1_PREP_LEGACY")+
                    " win="+G0ProbeEffectDefinition.GetCount("R1_PREP_WIN"));
            }
        }
        finally
        {
            foreach(var d in createdDefinitions)
                if(d!=null)UnityEngine.Object.DestroyImmediate(d);
            foreach(var e in createdEffects)
                if(e!=null)UnityEngine.Object.DestroyImmediate(e);
            G0ProbeEffectDefinition.Reset();
        }

        string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "R1_Trigger_Contract_20261102");
        Directory.CreateDirectory(folder);
        var report=new Report{GeneratedAtUtc=DateTime.UtcNow.ToString("o"),
            Pass=pass,Fail=errors.Count,Failures=errors.ToArray()};
        File.WriteAllText(Path.Combine(folder,"R1_LIVE_TIMING_GATE.json"),
            JsonUtility.ToJson(report,true));
        Debug.Log("[R1-Live] RESULT PASS="+pass+" FAIL="+errors.Count);
        if(errors.Count>0)
            throw new InvalidOperationException("R1 live timing failed: "+string.Join("|",errors));
    }
}
#endif
