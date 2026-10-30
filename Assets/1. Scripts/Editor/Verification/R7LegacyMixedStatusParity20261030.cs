#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7LegacyMixedStatusParity20261030
{
    private static readonly string[] Ids=
    {
        "5fb3b00e68684389a86c92f3e0345061",
        "5a239ec7d4944c458ac5329511091871",
        "c09f804f95004e70a9864e0d02ae4509",
        "313842626a60416f97ac13a24ca171e3",
        "f8c31ca52c544f93b8d961acab0c9611",
        "35a5b1e98c8547218682cc24566c91c0",
        "cf4031e1aabf4207b52248c0d821b85a"
    };
    private static ActionSlot Make(BattleContext battle,Character owner,
        Character target,BodyPart part,SkillDefinition data,long id)
    {
        var skill=data.CreateRuntimeSkill();
        skill.Initialize(owner,battle._battleEvent);
        return new ActionSlot
        {
            Owner=owner,TargetCharacter=target,TargetPart=part,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Skill=skill,ActionId=id,Phase=ActionPhase.COMBAT,
            SlotId="R7-MIXED-"+id
        };
    }
    [MenuItem("Game System Verification/R7 - 7 Legacy Mixed Native Status (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode only");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var router=battle?.Services?.R7LegacyStatusMigration;
        var owner=battle?.Player;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2. Data/TestEncounters/Prefabs/Hifumi_Test.prefab");
        if(battle==null||router==null||owner==null||prefab==null)
            throw new InvalidOperationException("Missing battle context and Hifumi prefab");
        var lGo=UnityEngine.Object.Instantiate(prefab);
        var nGo=UnityEngine.Object.Instantiate(prefab);
        lGo.name="R7_MIXED_LEGACY_TARGET";
        nGo.name="R7_MIXED_NATIVE_TARGET";
        var left=lGo.GetComponent<Hifumi>();
        var right=nGo.GetComponent<Hifumi>();
        left.Initialize(battle);
        right.Initialize(battle);
        var partA=left.GetTargetPoints(false).First(x=>x.IsValid&&x.Part!=null).Part;
        var partB=right.GetTargetPoints(false).First(x=>x.IsValid&&x.Part!=null).Part;
        var catalog=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g)))
            .Where(x=>x!=null).ToArray();
        var type=typeof(Skill).Assembly.GetType("SkillEffectDispatcher",true);
        var dispatcher=Activator.CreateInstance(type,true);
        var execute=type.GetMethods(BindingFlags.Instance|BindingFlags.Public|
            BindingFlags.NonPublic).Single(m=>m.Name=="Execute"&&
                m.GetParameters().Length==14);
        IReadOnlyList<SkillEffectResult> Call(SkillDefinition data,BattleAction action)=>
            (IReadOnlyList<SkillEffectResult>)execute.Invoke(dispatcher,
                new object[]{data,action,SkillEffectTiming.OnExecute,1,null,null,
                    null,null,-1,null,false,false,false,null});
        int pass=0,fail=0;
        void Check(string name,bool ok,string note="")
        {
            if(ok){pass++;Debug.Log("[R7-Mixed] PASS "+name+" "+note);}
            else{fail++;Debug.LogError("[R7-Mixed] FAIL "+name+" "+note);}
        }
        bool original=router.Enabled;
        try
        {
            Check("M00 factory enables seven legacy-status native assets",
                original);
            Check("M01 exactly seven approved skill asset IDs",
                R7ProductionLegacyStatusMigration.ApprovedSkillCount==7);
            Check("M02 two identical test targets",
                left.CurrentHP==right.CurrentHP&&
                (left.GetPartStatus<Bleeding>(partA)?.Stack??0)==
                (right.GetPartStatus<Bleeding>(partB)?.Stack??0));
            int prevCommits=router.NativeCommits;
            for(int i=0;i<Ids.Length;i++)
            {
                var data=catalog.Single(x=>x.SkillId==Ids[i]);
                var legacyAction=new BattleAction{Slot=Make(battle,owner,left,
                    partA,data,999810+i*2)};
                var nativeAction=new BattleAction{Slot=Make(battle,owner,right,
                    partB,data,999811+i*2)};
                router.Enabled=false;
                Check("M"+i+"A "+data.name+" disabled route uses legacy",
                    !router.CanRoute(data));
                int hp0=left.CurrentHP,hp1=right.CurrentHP;
                float part0=partA.PartHP,part1=partB.PartHP;
                var state=UnityEngine.Random.state;
                router.Enabled=false;
                var prior=Call(data,legacyAction);
                UnityEngine.Random.state=state;
                router.Enabled=true;
                Check("M"+i+"B "+data.name+" routed by verified asset",
                    router.CanRoute(data));
                var current=Call(data,nativeAction);
                Check("M"+i+"C "+data.name+" bleeding stack parity",
                    (left.GetPartStatus<Bleeding>(partA)?.Stack??0)==
                    (right.GetPartStatus<Bleeding>(partB)?.Stack??0),
                    "legacy="+(left.GetPartStatus<Bleeding>(partA)?.Stack??0)+
                    " native="+(right.GetPartStatus<Bleeding>(partB)?.Stack??0));
                Check("M"+i+"D "+data.name+" native commit exactly once",
                    router.NativeCommits==prevCommits+1);
                Check("M"+i+"E "+data.name+" entry outcome order",
                    prior.Count==current.Count&&
                    prior.Zip(current,(a,b)=>a.State==b.State).All(x=>x));
                Check("M"+i+"F "+data.name+" mixed damage not double applied",
                    (hp0-left.CurrentHP)==(hp1-right.CurrentHP) &&
                    Math.Abs((part0-partA.PartHP)-(part1-partB.PartHP))<0.001f,
                    "hp="+(hp0-left.CurrentHP)+"/"+(hp1-right.CurrentHP)+
                    " part="+(part0-partA.PartHP)+"/"+(part1-partB.PartHP));
                prevCommits=router.NativeCommits;
            }
            Check("M03 exactly seven native status commits",router.NativeCommits==7);
            Check("M07 the two mixed damage entries use real native R5 commands",
                router.NativeDamageAttempts==2 && router.NativeDamageCommits==2,
                "attempts="+router.NativeDamageAttempts+
                " commits="+router.NativeDamageCommits);
            var dupe=Ids[0];
            var d=catalog.Single(x=>x.SkillId==dupe);
            var replayAction=new BattleAction{Slot=Make(battle,owner,right,
                partB,d,999999)};
            var first=Call(d,replayAction);
            int last=right.GetPartStatus<Bleeding>(partB)?.Stack??0;
            int before=router.NativeCommits;
            var retry=Call(d,replayAction);
            Check("M04 same action cannot reapply native status",
                router.NativeCommits==before &&
                (right.GetPartStatus<Bleeding>(partB)?.Stack??0)==last);
            Check("M05 replay has no applied receipt",
                retry.All(x=>!x.WasApplied));
            router.Enabled=false;
            Check("M06 explicit disable reverts to legacy",!router.CanRoute(d));
        }
        finally
        {
            router.Enabled=original;
            UnityEngine.Object.Destroy(lGo);UnityEngine.Object.Destroy(nGo);
        }
        Debug.Log("[R7-Mixed] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 mixed status failed "+fail);
    }
}
#endif