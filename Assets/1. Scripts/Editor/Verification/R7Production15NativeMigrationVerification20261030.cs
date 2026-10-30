#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7Production15NativeMigrationVerification20261030
{
    private static readonly string[] Expected =
    {
        "common.preparation.crouch","common.preparation.glare",
        "olaf.duel.payback_all",
        "olaf.normal.shield_wall","olaf.normal.endure",
        "olaf.normal.parry","olaf.normal.rise_again",
        "olaf.normal.cover","olaf.normal.first_strike",
        "olaf.preparation.breath_bought_with_blood",
        "olaf.preparation.calm_madness",
        "olaf.preparation.erase_wound",
        "olaf.preparation.devour_stagger",
        "olaf.prestige.ragnarok",
        "yujin.duel.block_retreat"
    };

    private static ActionSlot Slot(Hifumi owner,Character enemy,
        SkillDefinition data,long id)
    {
        var runtime=data.CreateRuntimeSkill();
        runtime.Initialize(owner,owner.BattleContext._battleEvent);
        return new ActionSlot
        {
            ActionId=id,Owner=owner,TargetCharacter=enemy,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            TargetPart=enemy.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Skill=runtime,Phase=ActionPhase.COMBAT,SlotId="R7-PROD-"+id
        };
    }
    private static (int Block,int Prestige,int Bonus) Stats(Hifumi h)=>
        (h.RuntimeStatus?.currentBlock??0,
         h.RuntimeStatus?.currentPrestige??0,
         h.TurnClashPowerBonus);

    [MenuItem("Game System Verification/R7 - 15 Native Production Skill Migration (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)
            throw new InvalidOperationException("Requires Play Mode.");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var enemy=battle?.Enemies?.FirstOrDefault(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2. Data/TestEncounters/Prefabs/Hifumi_Test.prefab");
        var router=battle?.Services?.R7NativeResourceMigration;
        if(prefab==null||enemy==null||router==null)
            throw new InvalidOperationException("Battle factory R7 native service is missing.");
        var a=UnityEngine.Object.Instantiate(prefab);
        var b=UnityEngine.Object.Instantiate(prefab);
        a.name="R7_PRODUCTION_LEGACY_BASELINE";
        b.name="R7_PRODUCTION_NATIVE_COMMAND";
        var legacy=a.GetComponent<Hifumi>();
        var modern=b.GetComponent<Hifumi>();
        legacy.Initialize(battle);
        modern.Initialize(battle);

        var catalog=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g)))
            .Where(d=>d!=null).ToArray();
        var dType=typeof(Skill).Assembly.GetType("SkillEffectDispatcher",true);
        var dispatcher=Activator.CreateInstance(dType,true);
        var method=dType.GetMethods(BindingFlags.Instance|BindingFlags.Public|
            BindingFlags.NonPublic).Single(m=>m.Name=="Execute"&&
                m.GetParameters().Length==14);
        IReadOnlyList<SkillEffectResult> Call(SkillDefinition d,
            BattleAction action,SkillEffectTiming timing)=>
            (IReadOnlyList<SkillEffectResult>)method.Invoke(dispatcher,
                new object[]{d,action,timing,1,null,null,null,null,-1,null,
                    false,false,false,null});

        int pass=0,fail=0;
        void Check(string name,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R7-Prod] PASS "+name+" "+detail);}
            else{fail++;Debug.LogError("[R7-Prod] FAIL "+name+" "+detail);}
        }
        bool priorEnabled=router.Enabled;
        try
        {
            Check("C01 factory enables production cohort by default",
                priorEnabled && R7ProductionNativeResourceMigration.ApprovedSkillCount==15);
            Check("C02 two identical combatants start matched",Stats(legacy)==Stats(modern));
            Check("C03 all 15 intended SkillIds have exactly one asset",
                Expected.All(id=>catalog.Count(d=>d.SkillId==id)==1));
            Check("C04 unapproved effect family does not get native routing",
                !router.CanRoute(catalog.Single(d=>
                    d.SkillId=="yujin.normal.ledger_cleanup")));
            int previousNative=router.ActualNativeCommits;
            for(int i=0;i<Expected.Length;i++)
            {
                var id=Expected[i];
                var data=catalog.Single(d=>d.SkillId==id);
                var entry=data.EffectEntries.Single();
                long actionId=999100+10*i;
                var one=new BattleAction{Slot=Slot(legacy,enemy,data,actionId)};
                var two=new BattleAction{Slot=Slot(modern,enemy,data,actionId+1)};
                router.Enabled=false;
                var old=Call(data,one,entry.EffectiveTiming);
                router.Enabled=true;
                Check("C"+(i+1)+"a "+id+" production router accepts authored asset",
                    router.CanRoute(data));
                var now=Call(data,two,entry.EffectiveTiming);
                Check("C"+(i+1)+"b "+id+" native production state parity",
                    Stats(legacy)==Stats(modern),
                    "legacy="+Stats(legacy)+" native="+Stats(modern));
                Check("C"+(i+1)+"c "+id+" exact entry outcome/no double apply",
                    old.Count==1 && now.Count==1 &&
                    old[0].State==now[0].State &&
                    router.ActualNativeCommits==previousNative+1);
                previousNative=router.ActualNativeCommits;
            }
            Check("C05 all 15 actual native command writes committed",
                router.ActualNativeCommits==Expected.Length);
            var hold=catalog.Single(d=>d.SkillId==Expected[0]);
            var action=new BattleAction{
                Slot=Slot(modern,enemy,hold,999999)
            };
            int before=router.ActualNativeAttempts;
            var prior=Stats(modern);
            var wrong=Call(hold,action,SkillEffectTiming.OnClashEnd);
            Check("C06 mismatched event cannot commit a resource",
                router.ActualNativeAttempts==before && Stats(modern)==prior &&
                wrong.All(x=>!x.WasApplied));
            router.Enabled=false;
            Check("C07 manual rollback toggle restores legacy selection",
                !router.CanRoute(hold));
            Check("C08 no global experimental scope leaked",
                R7ExclusiveEntryMigrationScope.Active==null);
        }
        finally
        {
            router.Enabled=priorEnabled;
            UnityEngine.Object.Destroy(a);
            UnityEngine.Object.Destroy(b);
        }
        Debug.Log("[R7-Prod] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 production native parity failed "+fail);
    }
}
#endif