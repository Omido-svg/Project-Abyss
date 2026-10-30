#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7NativeResourceMigrationVerification20261030
{
    private static ActionSlot Slot(Hifumi h,Character enemy,
        SkillDefinition d,long id)
    {
        var skill=d.CreateRuntimeSkill();
        skill.Initialize(h,h.BattleContext._battleEvent);
        return new ActionSlot
        {
            ActionId=id,Owner=h,TargetCharacter=enemy,
            Part=h.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            TargetPart=enemy.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Skill=skill,Phase=ActionPhase.COMBAT,SlotId="R7-NATIVE-"+id
        };
    }
    private static (int Block,int Prestige,int Bonus) Snapshot(Hifumi h) =>
        (h.RuntimeStatus?.currentBlock??0,
         h.RuntimeStatus?.currentPrestige??0,
         h.TurnClashPowerBonus);

    [MenuItem("Game System Verification/R7 - Native Resource Command Parity (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode only");
        var ctx=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var enemy=ctx?.Enemies?.FirstOrDefault(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2. Data/TestEncounters/Prefabs/Hifumi_Test.prefab");
        if(ctx==null||enemy==null||prefab==null)
            throw new InvalidOperationException("Battle Test Scene/Hifumi prefab required");
        var a=UnityEngine.Object.Instantiate(prefab);
        var b=UnityEngine.Object.Instantiate(prefab);
        a.name="R7_NATIVE_LEGACY_CONTROL";
        b.name="R7_NATIVE_COMMAND_CONTROL";
        var legacy=a.GetComponent<Hifumi>();
        var migrated=b.GetComponent<Hifumi>();
        legacy.Initialize(ctx);migrated.Initialize(ctx);
        var production=ctx.Services.R7NativeResourceMigration;
        if(production==null)
            throw new InvalidOperationException("Production R7 router is missing.");
        // Preserve a true legacy baseline; explicit scope still runs native.
        bool wasEnabled=production.Enabled;
        production.Enabled=false;
        var catalog=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>new R7SkillAssetRef(
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                    AssetDatabase.GUIDToAssetPath(g)),g))
            .Where(x=>x.Definition!=null).ToArray();
        var examples=new []{
            "common.preparation.crouch",
            "common.preparation.glare",
            "olaf.normal.shield_wall",
            "olaf.normal.endure",
            "olaf.normal.first_strike",
            "olaf.preparation.breath_bought_with_blood",
            "yujin.duel.block_retreat"
        };
        int pass=0,fail=0;
        void Check(string name,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R7-Native] PASS "+name+" "+detail);}
            else{fail++;Debug.LogError("[R7-Native] FAIL "+name+" "+detail);}
        }
        try
        {
            Check("N01 two equal runtime owners",Snapshot(legacy)==Snapshot(migrated));
            var dispatcherType=typeof(Skill).Assembly.GetType("SkillEffectDispatcher",true);
            var dispatcher=Activator.CreateInstance(dispatcherType,true);
            var execute=dispatcherType.GetMethods(
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.Public|
                System.Reflection.BindingFlags.NonPublic)
                .Single(m=>m.Name=="Execute"&&m.GetParameters().Length==14);
            IReadOnlyList<SkillEffectResult> Call(
                SkillDefinition definition,BattleAction action,SkillEffectTiming timing)
            {
                return (IReadOnlyList<SkillEffectResult>)execute.Invoke(dispatcher,
                    new object[]{definition,action,timing,1,null,null,null,null,
                        -1,null,false,false,false,null});
            }
            int nativeTotal=0;
            int caseNo=0;
            foreach(var id in examples)
            {
                caseNo++;
                var asset=catalog.Single(x=>x.Definition.SkillId==id);
                var d=asset.Definition;
                var entry=d.EffectEntries.First(e=>
                    R7NativeResourceCommandExecutor.IsNative(e.Definition));
                var timing=entry.EffectiveTiming;
                var left=new BattleAction{Slot=Slot(legacy,enemy,d,998200+caseNo*2)};
                var right=new BattleAction{Slot=Slot(migrated,enemy,d,998201+caseNo*2)};
                var original=Call(d,left,timing);
                var before=Snapshot(legacy);
                using(var scope=R7ExclusiveEntryMigrationScope.Begin(catalog))
                {
                    var modern=Call(d,right,timing);
                    int commits=scope.NativeCommitted;
                    int attempts=scope.NativeAttempts;
                    nativeTotal+=commits;
                    Check("N"+caseNo+"A "+id+" native command used",
                        scope.CanRoute(d)&&attempts==1&&commits==1,
                        "attempted="+attempts+" committed="+commits);
                    Check("N"+caseNo+"B "+id+" equal actual resource state",
                        Snapshot(legacy)==Snapshot(migrated),
                        "legacy="+Snapshot(legacy)+" native="+Snapshot(migrated));
                    Check("N"+caseNo+"C "+id+" exact entry outcome",
                        original.Count==modern.Count &&
                        original.Zip(modern,(x,y)=>x.State==y.State).All(x=>x) &&
                        scope.History.Any(x=>x.SkillId==id&&x.Applied==1));
                    Check("N"+caseNo+"D "+id+" R5 command committed once",
                        scope.NativeCommitted==1&&
                        scope.History.Count(x=>x.SkillId==id&&x.Applied==1)==1);
                }
            }
            Check("N02 native path applied all seven tested entries",nativeTotal==examples.Length);
            Check("N03 scopes fully cleaned after each case",
                R7ExclusiveEntryMigrationScope.Active==null);
            // Timing contract must not make a resource command when
            // its authored timing does not match the current event.
            var gate=catalog.Single(x=>x.Definition.SkillId=="common.preparation.crouch");
            using(var scope=R7ExclusiveEntryMigrationScope.Begin(catalog))
            {
                var act=new BattleAction{Slot=Slot(migrated,enemy,gate.Definition,998299)};
                var prior=Snapshot(migrated);
                var result=Call(gate.Definition,act,SkillEffectTiming.OnClashEnd);
                Check("N04 wrong timing yields no native command",
                    scope.NativeAttempts==0&&Snapshot(migrated)==prior &&
                    result.All(x=>!x.WasApplied));
            }
            Debug.Log("[R7-Native] RESULT PASS="+pass+" FAIL="+fail);
            if(fail>0)throw new InvalidOperationException("R7 Native Resource failed "+fail);
        }
        finally
        {
            production.Enabled=wasEnabled;
            UnityEngine.Object.Destroy(a);
            UnityEngine.Object.Destroy(b);
        }
    }
}
#endif