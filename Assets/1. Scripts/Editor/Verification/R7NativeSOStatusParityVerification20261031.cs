#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7NativeSOStatusParityVerification20261031
{
    private static ActionSlot Make(BattleContext c,Character owner,Character target,
        SkillDefinition data,long id)
    {
        var skill=data.CreateRuntimeSkill();skill.Initialize(owner,c._battleEvent);
        return new ActionSlot {
            ActionId=id,Owner=owner,TargetCharacter=target,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            TargetPart=null,Skill=skill,Phase=ActionPhase.COMBAT,
            SlotId="R7-NATIVE-STATUS-"+id
        };
    }
    [MenuItem("Game System Verification/R7 Native SO - 9 Bleeding x 3 Levels (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode");
        var c=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var actor=c?.Player;
        var targets=c?.Enemies?.Where(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts)
            .Take(2).ToArray();
        var router=c?.Services?.R7NativeBleedingMigration;
        if(router==null||actor==null||targets?.Length!=2||c.SkillUpgrades==null)
            throw new InvalidOperationException("Two targets / native router required");
        var graphs=Resources.LoadAll<R7NativeStatusAbilityDefinition>(
            "ProjectAbyss/NativeStatuses").OrderBy(x=>x.StableId).ToArray();
        var catalog=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g))).Where(x=>x!=null).ToArray();
        var type=typeof(Skill).Assembly.GetType("SkillEffectDispatcher",true);
        var dispatcher=Activator.CreateInstance(type,true);
        var m=type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|
            BindingFlags.Instance).Single(x=>x.Name=="Execute"&&
                x.GetParameters().Length==14);
        IReadOnlyList<SkillEffectResult> Call(SkillDefinition d,BattleAction action,
            SkillEffectTiming timing)=>
            (IReadOnlyList<SkillEffectResult>)m.Invoke(dispatcher,new object[]{
                d,action,timing,1,null,null,null,null,-1,null,false,false,false,null});
        int pass=0,fail=0;
        void Check(string id,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R7-StatusGraph] PASS "+id+" "+detail);}
            else{fail++;Debug.LogError("[R7-StatusGraph] FAIL "+id+" "+detail);}
        }
        bool enabled=router.Enabled;
        var restore=new List<(SkillDefinition Skill,int Level)>();
        Check("S01 exactly nine native authored status assets",graphs.Length==9);
        try
        {
            long id=994100;
            foreach(var graph in graphs)
            {
                var d=catalog.Single(x=>x.SkillId==graph.StableId);
                restore.Add((d,c.SkillUpgrades.GetLevel(d)));
                Check("S02 native data matches source "+graph.StableId,
                    graph.IsValid&&graph.SourceAssetName==d.name&&router.CanRoute(d)&&
                    graph.SourceAssetGuid==AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(d)));
                Check("S03 compiled upgrade levels "+graph.StableId,
                    graph.Level0Stacks==R7NativeStatusAssetMigration20261031.ResolveStacks(d,0)&&
                    graph.Level1Stacks==R7NativeStatusAssetMigration20261031.ResolveStacks(d,1)&&
                    graph.Level2Stacks==R7NativeStatusAssetMigration20261031.ResolveStacks(d,2));
                for(int level=0;level<=2;level++)
                {
                    c.SkillUpgrades.TrySetLevel(d,level);
                    int priorOld=targets[0].GetStatus<Bleeding>()?.Stack??0;
                    int priorNew=targets[1].GetStatus<Bleeding>()?.Stack??0;
                    Check("S04 equal starting status "+d.SkillId+" lv"+level,
                        priorOld==priorNew);
                    var timing=d.EffectEntries[0].EffectiveTiming;
                    router.Enabled=false;
                    var old=Call(d,new BattleAction{Slot=Make(c,actor,targets[0],d,id++)},timing);
                    router.Enabled=true;
                    int commits=router.NativeCommits;
                    var modern=Call(d,new BattleAction{Slot=Make(c,actor,targets[1],d,id++)},timing);
                    int stackOld=targets[0].GetStatus<Bleeding>()?.Stack??0;
                    int stackNew=targets[1].GetStatus<Bleeding>()?.Stack??0;
                    Check("S05 exact real status stack "+d.SkillId+" lv"+level,
                        stackOld==stackNew,"old="+stackOld+" native="+stackNew);
                    Check("S06 native receipt matches old "+d.SkillId+" lv"+level,
                        old.Count==1&&modern.Count==1&&
                        old[0].State==modern[0].State &&
                        router.NativeCommits==commits+1);
                }
            }
        }
        finally
        {
            foreach(var x in restore)c.SkillUpgrades.TrySetLevel(x.Skill,x.Level);
            router.Enabled=enabled;
        }
        Check("S07 router enable flag restored",router.Enabled==enabled);
        Debug.Log("[R7-StatusGraph] RESULT PASS="+pass+" FAIL="+fail);
        if(fail!=0)throw new InvalidOperationException(
            "Native status x3-level parity failed "+fail);
    }
}
#endif