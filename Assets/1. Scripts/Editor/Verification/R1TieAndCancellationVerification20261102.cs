#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>R1 PlayMode: a cancelled 3-roll action and 5 genuine ties on ONE roll use.</summary>
public static class R1TieAndCancellationVerification20261102
{
    private sealed class ScriptedSequenceSkill : DataNormalSkill
    {
        private readonly int[] values;
        public int RollCalls {get;private set;}
        public ScriptedSequenceSkill(SkillDefinition definition,params int[] values):base(definition)
        {
            this.values=values;
        }
        public override RollResult RollPowerResultForExchange(int index)
        {
            int value=values[Math.Min(RollCalls++,values.Length-1)];
            var result=new RollResult{
                ResolverType=SkillResolverType.Dice,RollIndex=index,RollType=CombatRollType.Attack,
                DiceMin=value,DiceMax=value,BasePower=0,RawValue=value,ModifiedValue=value
            };
            result.RecalculateFinalPower();
            return result;
        }
    }
    [Serializable] private sealed class Result
    {
        public string GeneratedAtUtc;
        public int Pass,Fail;
        public string[] Failures;
    }
    private static SkillEffectEntry Probe(string key,SkillEffectTiming timing,
        List<SkillEffectDefinition> effects)
    {
        var effect=ScriptableObject.CreateInstance<G0ProbeEffectDefinition>();
        effect.name=key;effect.ProbeLabel=key;
        effects.Add(effect);
        return new SkillEffectEntry{Definition=effect,OverrideTiming=true,
            Timing=timing,Conditions=new List<SkillEffectCondition>()};
    }
    private static SkillDefinition Create(string tag,
        List<SkillEffectEntry> entries,List<SkillDefinition> defs,int rolls=1)
    {
        var so=ScriptableObject.CreateInstance<SkillDefinition>();
        so.name="R1_SEQUENCE_"+tag;so.SkillName=so.name;
        so.ActionType=ActionType.NormalAttack;
        so.ResolverType=SkillResolverType.Dice;
        so.ExchangeRollCount=rolls;
        so.Color=SkillColor.Red;
        so.EffectEntries=entries;
        so.Rolls=new List<SkillRollData>();
        defs.Add(so);
        return so;
    }
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode required.");
        var manager=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        var enemy=manager?.BattleContext?.Enemies?.FirstOrDefault(c=>c!=null&&!c.IsDead);
        if(enemy==null || manager.BattleContext.Player==null)
            throw new InvalidOperationException("Initialized BattleManager+enemy required");
        var defs=new List<SkillDefinition>();
        var effects=new List<SkillEffectDefinition>();
        var errors=new List<string>();int pass=0;
        void Check(string id,bool ok,string info)
        {
            if(ok){pass++;Debug.Log("[R1-TieCancel] PASS "+id+" "+info);}
            else{errors.Add(id+" "+info);Debug.LogError("[R1-TieCancel] FAIL "+id+" "+info);}
        }
        try
        {
            // Real damage kills the opposing actor during the first exchange.
            // No fabricated RollUse or changed production combat rule.
            var method=typeof(G0AbilityAuditReport20261029)
                .GetMethod("RunScenario",BindingFlags.NonPublic|BindingFlags.Static);
            object cancellation=method.Invoke(null,new object[]{
                manager,enemy,"R1-TR02","Terminate before 2nd roll",
                3,3,ActionType.NormalAttack,ActionType.NormalAttack,
                2000,1,false,0,defs,effects
            });
            int Count(string label)
            {
                var items=(System.Collections.IEnumerable)cancellation.GetType()
                    .GetField("ProbeCounts").GetValue(cancellation);
                foreach(object x in items)
                    if((string)x.GetType().GetField("Label").GetValue(x)==label)
                        return (int)x.GetType().GetField("FirstOwnerCount").GetValue(x);
                return -1;
            }
            Check("TR02 target dies after first exchange; remaining uses cancelled",
                Count("OnExecute")==1 &&
                Count("OnExchangeWin")==1 &&
                (int)cancellation.GetType().GetField("ExchangeCount").GetValue(cancellation)==1,
                "actual use="+Count("OnExecute")+" win="+Count("OnExchangeWin"));

            using(var fixture=GameSystemVerificationFixture.Create(
                manager.BattleContext.Player,enemy,manager.BattleRules))
            {
                CharacterVerificationScenarioTools.ResetCombatState(fixture.Player);
                CharacterVerificationScenarioTools.ResetCombatState(fixture.Enemy);
                G0ProbeEffectDefinition.ResetCounters();
                var firstDef=Create("FIRST",new[]{
                    Probe("TIE_USE_FIRST",SkillEffectTiming.OnExecute,effects),
                    Probe("TIE_WIN_FIRST",SkillEffectTiming.OnExchangeWin,effects)
                }.ToList(),defs);
                var secondDef=Create("SECOND",new[]{
                    Probe("TIE_USE_SECOND",SkillEffectTiming.OnExecute,effects),
                    Probe("TIE_LOSE_SECOND",SkillEffectTiming.OnExchangeLose,effects)
                }.ToList(),defs);
                var one=new ScriptedSequenceSkill(firstDef,3,4,5,6,7,9);
                var two=new ScriptedSequenceSkill(secondDef,3,4,5,6,7,8);
                one.Initialize(fixture.Player,fixture.Context._battleEvent);
                two.Initialize(fixture.Enemy,fixture.Context._battleEvent);
                one.Register();two.Register();
                try
                {
                    var a=new ActionSlot{
                        ActionId=719101,Owner=fixture.Player,Part=fixture.Player.BodyParts.FirstOrDefault(),
                        TargetCharacter=fixture.Enemy,TargetPart=fixture.Enemy.BodyParts.FirstOrDefault(),
                        Skill=one,Speed=10,Phase=ActionPhase.COMBAT,SlotId="R1-TIE-FIRST",
                        ResourceCostCommitted=true
                    };
                    var b=new ActionSlot{
                        ActionId=719102,Owner=fixture.Enemy,Part=fixture.Enemy.BodyParts.FirstOrDefault(),
                        TargetCharacter=fixture.Player,TargetPart=fixture.Player.BodyParts.FirstOrDefault(),
                        Skill=two,Speed=10,Phase=ActionPhase.COMBAT,SlotId="R1-TIE-SECOND",
                        ResourceCostCommitted=true
                    };
                    var result=fixture.ClashManager.ResolvePair(new ClashPair(a,b));
                    int tieCount=result?.Exchanges?.FirstOrDefault()?.TieRerollCount??-1;
                    Check("TR03 five tie rerolls do not re-use the roll",
                        tieCount==5 &&
                        one.RollCalls==6&&two.RollCalls==6 &&
                        G0ProbeEffectDefinition.GetCount("TIE_USE_FIRST")==1 &&
                        G0ProbeEffectDefinition.GetCount("TIE_USE_SECOND")==1,
                        "ties="+tieCount+" calls="+one.RollCalls+"/"+two.RollCalls+
                        " use="+G0ProbeEffectDefinition.GetCount("TIE_USE_FIRST")+
                        "/"+G0ProbeEffectDefinition.GetCount("TIE_USE_SECOND"));
                    Check("TR03 win/loss effect emitted once after 5 ties",
                        G0ProbeEffectDefinition.GetCount("TIE_WIN_FIRST")==1 &&
                        G0ProbeEffectDefinition.GetCount("TIE_LOSE_SECOND")==1,
                        "win="+G0ProbeEffectDefinition.GetCount("TIE_WIN_FIRST")+
                        " lose="+G0ProbeEffectDefinition.GetCount("TIE_LOSE_SECOND"));
                }
                finally{one.Unregister();two.Unregister();}
            }

            using(var majority=GameSystemVerificationFixture.Create(
                manager.BattleContext.Player,enemy,manager.BattleRules))
            {
                CharacterVerificationScenarioTools.ResetCombatState(majority.Player);
                CharacterVerificationScenarioTools.ResetCombatState(majority.Enemy);
                G0ProbeEffectDefinition.ResetCounters();
                var firstDef=Create("MAJ_FIRST",new[]{
                    Probe("MAJ_USE_FIRST",SkillEffectTiming.OnExecute,effects),
                    Probe("MAJ_WIN_FIRST",SkillEffectTiming.OnExchangeWin,effects),
                    Probe("MAJ_LOSE_FIRST",SkillEffectTiming.OnExchangeLose,effects),
                    Probe("MAJ_END_FIRST",SkillEffectTiming.OnClashEnd,effects)
                }.ToList(),defs,rolls:4);
                var secondDef=Create("MAJ_SECOND",new[]{
                    Probe("MAJ_USE_SECOND",SkillEffectTiming.OnExecute,effects),
                    Probe("MAJ_WIN_SECOND",SkillEffectTiming.OnExchangeWin,effects),
                    Probe("MAJ_END_SECOND",SkillEffectTiming.OnClashEnd,effects)
                }.ToList(),defs,rolls:3);
                var one=new ScriptedSequenceSkill(firstDef,10,10,1,10);
                var two=new ScriptedSequenceSkill(secondDef,1,1,10);
                one.Initialize(majority.Player,majority.Context._battleEvent);
                two.Initialize(majority.Enemy,majority.Context._battleEvent);
                one.Register();two.Register();
                try
                {
                    var a=new ActionSlot{
                        ActionId=719201,Owner=majority.Player,Part=majority.Player.BodyParts.FirstOrDefault(),
                        TargetCharacter=majority.Enemy,TargetPart=majority.Enemy.BodyParts.FirstOrDefault(),
                        Skill=one,Speed=10,Phase=ActionPhase.COMBAT,SlotId="R1-MAJ-FIRST",
                        ResourceCostCommitted=true
                    };
                    var b=new ActionSlot{
                        ActionId=719202,Owner=majority.Enemy,Part=majority.Enemy.BodyParts.FirstOrDefault(),
                        TargetCharacter=majority.Player,TargetPart=majority.Player.BodyParts.FirstOrDefault(),
                        Skill=two,Speed=10,Phase=ActionPhase.COMBAT,SlotId="R1-MAJ-SECOND",
                        ResourceCostCommitted=true
                    };
                    var result=majority.ClashManager.ResolvePair(new ClashPair(a,b));
                    int firstWins=G0ProbeEffectDefinition.GetCount("MAJ_WIN_FIRST");
                    int firstLosses=G0ProbeEffectDefinition.GetCount("MAJ_LOSE_FIRST");
                    Check("TR09 two exchange wins, one loss, then one-sided tail",
                        result!=null && result.IsClash &&
                        firstWins==2 && firstLosses==1 &&
                        G0ProbeEffectDefinition.GetCount("MAJ_USE_FIRST")==4 &&
                        G0ProbeEffectDefinition.GetCount("MAJ_USE_SECOND")==3 &&
                        G0ProbeEffectDefinition.GetCount("MAJ_END_FIRST")==1 &&
                        G0ProbeEffectDefinition.GetCount("MAJ_END_SECOND")==1 &&
                        result.OneSidedHitCount==1,
                        "wins="+firstWins+" losses="+firstLosses+
                        " uses="+G0ProbeEffectDefinition.GetCount("MAJ_USE_FIRST")+
                        "/"+G0ProbeEffectDefinition.GetCount("MAJ_USE_SECOND")+
                        " one-sided="+(result?.OneSidedHitCount??-1));
                }
                finally{one.Unregister();two.Unregister();}
            }
        }
        finally
        {
            foreach(var d in defs)if(d!=null)UnityEngine.Object.DestroyImmediate(d);
            foreach(var e in effects)if(e!=null)UnityEngine.Object.DestroyImmediate(e);
            G0ProbeEffectDefinition.Reset();
        }
        string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "R1_Trigger_Contract_20261102");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"R1_TIE_CANCEL_GATE.json"),
            JsonUtility.ToJson(new Result{
                GeneratedAtUtc=DateTime.UtcNow.ToString("o"),
                Pass=pass,Fail=errors.Count,Failures=errors.ToArray()
            },true));
        Debug.Log("[R1-TieCancel] RESULT PASS="+pass+" FAIL="+errors.Count);
        if(errors.Count>0)throw new InvalidOperationException("R1 tie/cancel failed: "+string.Join("|",errors));
    }
}
#endif
