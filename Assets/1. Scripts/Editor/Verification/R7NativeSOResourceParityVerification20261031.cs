#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7NativeSOResourceParityVerification20261031
{
    private static (int Block,int Prestige,int Power) Read(Hifumi c)=>
        (c.RuntimeStatus?.currentBlock??0,
         c.RuntimeStatus?.currentPrestige??0,
         c.TurnClashPowerBonus);

    private static ActionSlot Make(BattleContext ctx,Hifumi owner,
        Character enemy,SkillDefinition data,long id)
    {
        var skill=data.CreateRuntimeSkill();
        skill.Initialize(owner,ctx._battleEvent);
        return new ActionSlot {
            ActionId=id,Owner=owner,TargetCharacter=enemy,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            TargetPart=enemy.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Skill=skill,Phase=ActionPhase.COMBAT,SlotId="R7-GRAPH-"+id
        };
    }

    [MenuItem("Game System Verification/R7 Native SO - 15 Skills x 3 Upgrade Levels (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)
            throw new InvalidOperationException("Play Mode only");
        var ctx=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var router=ctx?.Services?.R7NativeResourceMigration;
        var enemy=ctx?.Enemies?.FirstOrDefault(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2. Data/TestEncounters/Prefabs/Hifumi_Test.prefab");
        if(router==null||enemy==null||prefab==null||ctx.SkillUpgrades==null)
            throw new InvalidOperationException("Missing live test scene");
        var graphs=Resources.LoadAll<R7NativeAbilityDefinition>(
            "ProjectAbyss/NativeAbilities").OrderBy(x=>x.StableId).ToArray();
        var source=AssetDatabase.FindAssets("t:SkillDefinition").Select(g=>
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g)))
            .Where(x=>x!=null).ToArray();
        var type=typeof(Skill).Assembly.GetType("SkillEffectDispatcher",true);
        var dispatcher=Activator.CreateInstance(type,true);
        var execute=type.GetMethods(BindingFlags.Instance|BindingFlags.Public|
            BindingFlags.NonPublic).Single(x=>x.Name=="Execute"&&
                x.GetParameters().Length==14);
        IReadOnlyList<SkillEffectResult> Call(SkillDefinition data,BattleAction action,
            SkillEffectTiming timing)=>
            (IReadOnlyList<SkillEffectResult>)execute.Invoke(dispatcher,
                new object[]{data,action,timing,1,null,null,null,null,-1,null,
                    false,false,false,null});
        int pass=0,fail=0;
        void Check(string name,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R7-Graph] PASS "+name+" "+detail);}
            else{fail++;Debug.LogError("[R7-Graph] FAIL "+name+" "+detail);}
        }
        bool prior=router.Enabled;
        var restore=new List<(SkillDefinition Data,int Level)>();
        Check("G01 15 native SOs loaded",graphs.Length==15);
        Check("G02 router source is native SO catalog",
            R7ProductionNativeResourceMigration.ApprovedSkillCount==15);
        try
        {
            long id=991000;
            foreach(var graph in graphs)
            {
                var data=source.Single(d=>d.SkillId==graph.StableId);
                restore.Add((data,ctx.SkillUpgrades.GetLevel(data)));
                Check("G03 compiled authoring identity "+graph.StableId,
                    graph.IsValid&&router.CanRoute(data)&&
                    graph.SourceAssetName==data.name&&
                    graph.SourceAssetGuid==AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(data)));
                Check("G04 upgrade amounts compiled from source "+graph.StableId,
                    graph.Level0Amount==R7NativeResourceAssetMigration20261031.ResolveAmount(data,0)&&
                    graph.Level1Amount==R7NativeResourceAssetMigration20261031.ResolveAmount(data,1)&&
                    graph.Level2Amount==R7NativeResourceAssetMigration20261031.ResolveAmount(data,2));
                var leftObj=UnityEngine.Object.Instantiate(prefab);
                var rightObj=UnityEngine.Object.Instantiate(prefab);
                var oldOwner=leftObj.GetComponent<Hifumi>();
                var newOwner=rightObj.GetComponent<Hifumi>();
                oldOwner.Initialize(ctx);newOwner.Initialize(ctx);
                try
                {
                    for(int lvl=0;lvl<=2;lvl++)
                    {
                        ctx.SkillUpgrades.TrySetLevel(data,lvl);
                        var previous=Read(oldOwner);
                        var previousNew=Read(newOwner);
                        Check("G05 preconditions "+graph.StableId+" LV"+lvl,
                            previous==previousNew);
                        var timing=data.EffectEntries[0].EffectiveTiming;
                        router.Enabled=false;
                        var old=Call(data,new BattleAction{
                            Slot=Make(ctx,oldOwner,enemy,data,id++)},timing);
                        router.Enabled=true;
                        int count=router.ActualNativeCommits;
                        var newer=Call(data,new BattleAction{
                            Slot=Make(ctx,newOwner,enemy,data,id++)},timing);
                        Check("G06 real resource parity "+graph.StableId+" LV"+lvl,
                            Read(oldOwner)==Read(newOwner),
                            "old="+Read(oldOwner)+" native="+Read(newOwner));
                        Check("G07 result parity and actual native commit "+graph.StableId+" LV"+lvl,
                            old.Count==1&&newer.Count==1&&
                            old[0].State==newer[0].State&&
                            router.ActualNativeCommits==count+1);
                    }
                }
                finally
                {
                    UnityEngine.Object.Destroy(leftObj);
                    UnityEngine.Object.Destroy(rightObj);
                }
            }
        }
        finally
        {
            foreach(var pair in restore)
                ctx.SkillUpgrades.TrySetLevel(pair.Data,pair.Level);
            router.Enabled=prior;
        }
        Check("G08 router enable flag restored",router.Enabled==prior);
        Debug.Log("[R7-Graph] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException(
            "R7 native authored ability parity failed "+fail);
    }
}
#endif