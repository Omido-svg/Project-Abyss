#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>5 authentic source SkillDefinitions vs native SO/R5 command,
/// L0/L1/L2 and repeat/duplicate gating on actual character mechanics.</summary>
public static class NativeUniqueGaugeParity
{
    private static int Gauge(Character owner,UniqueGaugeKind kind)=>
        kind==UniqueGaugeKind.OlafMadness
            ?owner.GetMechanic<OlafMadnessMechanic>()?.CurrentMadness??-1
            :owner.GetMechanic<YujinMechanic>()?.Sense??-1;
    private static ActionSlot Make(BattleContext battle,Character owner,
        Character enemy,SkillDefinition definition,long id)
    {
        var skill=definition.CreateRuntimeSkill();
        skill.Initialize(owner,battle._battleEvent);
        return new ActionSlot{
            ActionId=id,Owner=owner,TargetCharacter=enemy,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            TargetPart=enemy.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Skill=skill,Phase=ActionPhase.COMBAT,
            SlotId="R7-GAUGE-"+id
        };
    }

    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("PlayMode required");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var route=battle?.Services?.R7NativeUniqueGaugeMigration;
        var enemy=battle?.Enemies?.FirstOrDefault(e=>e!=null&&!e.IsDead&&!e.UsesBodyParts);
        if(route==null||enemy==null||battle.SkillUpgrades==null)
            throw new InvalidOperationException("Required production graph unavailable");
        var assets=Resources.LoadAll<R7NativeUniqueGaugeDefinition>(
            "ProjectAbyss/NativeUniqueGauges").OrderBy(x=>x.StableId).ToArray();
        var type=typeof(Skill).Assembly.GetType("SkillEffectDispatcher",true);
        var dispatcher=Activator.CreateInstance(type,true);
        var execute=type.GetMethods(BindingFlags.Instance|BindingFlags.Public|
            BindingFlags.NonPublic).Single(m=>m.Name=="Execute"&&
                m.GetParameters().Length==14);
        IReadOnlyList<SkillEffectResult> Call(SkillDefinition d,BattleAction a,
            SkillEffectTiming timing)=>
            (IReadOnlyList<SkillEffectResult>)execute.Invoke(dispatcher,
                new object[]{d,a,timing,1,null,null,null,null,-1,null,
                    false,false,false,null});
        var prior=route.Enabled;
        var restore=new List<(SkillDefinition data,int level)>();
        int pass=0,fail=0;
        void Check(string id,bool yes,string detail="")
        {
            if(yes){pass++;Debug.Log("[R7-GaugeParity] PASS "+id+" "+detail);}
            else{fail++;Debug.LogError("[R7-GaugeParity] FAIL "+id+" "+detail);}
        }
        Check("G01 exact five native graphs",assets.Length==5);
        try
        {
            long action=777700;
            foreach(var graph in assets)
            {
                var path=AssetDatabase.GUIDToAssetPath(graph.SourceAssetGuid);
                var data=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
                if(data==null)throw new InvalidOperationException(
                    "Native source missing "+graph.StableId);
                restore.Add((data,battle.SkillUpgrades.GetLevel(data)));
                Check("G02 exact source graph "+graph.StableId,
                    graph.IsValid && data.SkillId==graph.StableId &&
                    data.name==graph.SourceAssetName && route.CanRoute(data));
                if(HistoricalParityScope.IsSupersededSource(data))
                {
                    Check("G02b final authoritative gauge owner "+graph.StableId,
                        HistoricalParityScope.HasFinalOwner(data,battle));
                    continue;
                }
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                    graph.Kind==UniqueGaugeKind.OlafMadness
                        ?"Assets/2. Data/TestEncounters/Prefabs/Olaf_Test.prefab"
                        :"Assets/2. Data/TestEncounters/Prefabs/Yujin_Test.prefab");
                if(prefab==null)throw new InvalidOperationException(
                    "Missing "+graph.Kind+" prefab");
                var oldGo=UnityEngine.Object.Instantiate(prefab);
                var newGo=UnityEngine.Object.Instantiate(prefab);
                var oldOwner=oldGo.GetComponent<Character>();
                var newOwner=newGo.GetComponent<Character>();
                oldOwner.Initialize(battle);
                newOwner.Initialize(battle);
                try
                {
                    for(int lvl=0;lvl<=2;lvl++)
                    {
                        battle.SkillUpgrades.TrySetLevel(data,lvl);
                        var expected=graph.AmountForLevel(lvl);
                        Check("G03 prestate "+graph.StableId+" L"+lvl,
                            Gauge(oldOwner,graph.Kind)==Gauge(newOwner,graph.Kind),
                            "old="+Gauge(oldOwner,graph.Kind)+
                            ",native="+Gauge(newOwner,graph.Kind));
                        var timing=data.EffectEntries[0].EffectiveTiming;
                        route.Enabled=false;
                        var legacy=HistoricalParityScope.ExecuteLegacy(data, () =>
                            Call(data,new BattleAction{
                            Slot=Make(battle,oldOwner,enemy,data,action++)},timing));
                        route.Enabled=true;
                        int commits=route.Commits;
                        var newer=Call(data,new BattleAction{
                            Slot=Make(battle,newOwner,enemy,data,action++)},timing);
                        Check("G04 gauge parity "+graph.StableId+" L"+lvl,
                            Gauge(oldOwner,graph.Kind)==Gauge(newOwner,graph.Kind),
                            "legacy="+Gauge(oldOwner,graph.Kind)+
                            ",native="+Gauge(newOwner,graph.Kind)+
                            ",authored delta="+expected);
                        Check("G05 real native command + state parity "+graph.StableId+" L"+lvl,
                            legacy.Count==1&&newer.Count==1 &&
                            legacy[0].State==newer[0].State &&
                            route.Commits==commits+1,
                            "native commits="+route.Commits);
                    }
                }
                finally
                {
                    UnityEngine.Object.Destroy(oldGo);
                    UnityEngine.Object.Destroy(newGo);
                }
            }
        }
        finally
        {
            foreach(var x in restore)
                battle.SkillUpgrades.TrySetLevel(x.data,x.level);
            route.Enabled=prior;
        }
        Check("G06 routing flag restored",route.Enabled==prior);
        Debug.Log("[R7-GaugeParity] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 unique gauge parity "+fail);
    }
}
#endif
