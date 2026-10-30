#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7ProductionActualClashVerification20261030
{
    private static ActionSlot Build(BattleContext battle,SkillDefinition data,long id)
    {
        var owner=battle.Player;
        var target=battle.Enemies.First(x=>x!=null&&!x.IsDead);
        var runtime=data.CreateRuntimeSkill();
        runtime.Initialize(owner,battle._battleEvent);
        return new ActionSlot
        {
            Owner=owner,TargetCharacter=target,ActionId=id,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            TargetPart=target.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            SlotId="R7-PROD-FULL-"+id,Phase=ActionPhase.COMBAT,Skill=runtime
        };
    }

    [MenuItem("Game System Verification/R7 - Production Native Real Clash Gate (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var router=battle?.Services?.R7NativeResourceMigration;
        var owner=battle?.Player;
        var definition=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(x=>x!=null&&x.SkillId=="common.preparation.crouch");
        if(battle==null||router==null||owner==null||definition==null)
            throw new InvalidOperationException("Live Battle runtime unavailable");
        int pass=0,fail=0;
        void Check(string label,bool ok,string extra="")
        {
            if(ok){pass++;Debug.Log("[R7-FullClash] PASS "+label+" "+extra);}
            else{fail++;Debug.LogError("[R7-FullClash] FAIL "+label+" "+extra);}
        }
        bool wasEnabled=router.Enabled;
        try
        {
            Check("F01 BattleRuntimeFactory registered native service",router!=null);
            Check("F02 current source SkillDefinition matches production whitelist",
                router.CanRoute(definition));
            int initial=owner.RuntimeStatus?.currentBlock??0;
            router.Enabled=false;
            var baseline=battle.Services.ClashManager.ResolvePair(
                new ClashPair(Build(battle,definition,999501)));
            int legacyDelta=(owner.RuntimeStatus?.currentBlock??0)-initial;
            router.Enabled=true;
            int nativeBefore=router.ActualNativeCommits;
            var converted=battle.Services.ClashManager.ResolvePair(
                new ClashPair(Build(battle,definition,999502)));
            int nativeDelta=(owner.RuntimeStatus?.currentBlock??0)-initial-legacyDelta;
            Check("F03 original real ClashManager produced a result",
                baseline!=null && baseline.Exchanges.Count>0);
            Check("F04 production real ClashManager produced a result",
                converted!=null && converted.Exchanges.Count>0);
            Check("F05 native and legacy actual resource gain equal",
                legacyDelta>0&&legacyDelta==nativeDelta,
                "legacy="+legacyDelta+" native="+nativeDelta);
            Check("F06 native route committed actual commands in combat",
                router.ActualNativeCommits>nativeBefore,
                "commits="+(router.ActualNativeCommits-nativeBefore));
            Check("F07 real exchange count preserved",
                baseline.Exchanges.Count==converted.Exchanges.Count);
            Check("F08 unchanged normal combat path still exists",
                battle.Services.ClashManager!=null &&
                battle.Services.ActionResolver!=null);
        }
        finally{router.Enabled=wasEnabled;}
        Debug.Log("[R7-FullClash] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 real combat parity failed "+fail);
    }
}
#endif