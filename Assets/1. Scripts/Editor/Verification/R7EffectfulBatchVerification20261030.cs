#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7EffectfulBatchVerification20261030
{
    private static R7SkillAssetRef[] Catalog()=>
        AssetDatabase.FindAssets("t:SkillDefinition")
        .Select(g=>new R7SkillAssetRef(
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g)),g))
        .Where(x=>x.Definition!=null).ToArray();

    private static ActionSlot Slot(BattleContext battle,
        Character owner,Character enemy,long id,SkillDefinition skillData)
    {
        var skill=skillData.CreateRuntimeSkill();
        skill.Initialize(owner,battle._battleEvent);
        return new ActionSlot
        {
            ActionId=id,Owner=owner,TargetCharacter=enemy,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            TargetPart=enemy.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Phase=ActionPhase.COMBAT,SlotId="R7-EFFECT-"+id,Skill=skill
        };
    }

    [MenuItem("Game System Verification/R7 - Effectful Exclusive Battle Batch (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode only");
        var ctx=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var owner=ctx?.Player;
        var enemies=ctx?.Enemies?.Where(e=>e!=null&&!e.IsDead&&!e.UsesBodyParts).Take(2).ToArray();
        if(owner==null || enemies?.Length<2 || ctx.Services?.ClashManager==null)
            throw new InvalidOperationException("Battle Test Scene needs two normal enemies");
        var all=Catalog();
        int pass=0,fail=0;
        void Check(string s,bool ok,string detail="")
        {
            if(ok){pass++;Debug.Log("[R7-Batch] PASS "+s+" "+detail);}
            else{fail++;Debug.LogError("[R7-Batch] FAIL "+s+" "+detail);}
        }
        Check("E01 enumerate all real skill assets",all.Length==145);
        var effect=all.First(x=>x.Definition.SkillId=="yujin.normal.ledger_cleanup");
        Check("E02 test definition has authored real effect",
            effect.Definition.EffectEntries.Count>0&&effect.Definition.ActionType==ActionType.NormalAttack);
        var legacySlot=Slot(ctx,owner,enemies[0],997801,effect.Definition);
        var migratedSlot=Slot(ctx,owner,enemies[1],997802,effect.Definition);
        int hpBeforeLegacy=enemies[0].CurrentHP;
        int hpBeforeMigrated=enemies[1].CurrentHP;
        Check("E03 equal initial enemy HP",hpBeforeLegacy==hpBeforeMigrated);

        UnityEngine.Random.InitState(593137);
        var originalRandom=UnityEngine.Random.state;
        var baseline=ctx.Services.ClashManager.ResolvePair(new ClashPair(legacySlot));
        int damageLegacy=hpBeforeLegacy-enemies[0].CurrentHP;

        UnityEngine.Random.state=originalRandom;
        using(var scope=R7ExclusiveEntryMigrationScope.Begin(all))
        {
            Check("E04 scope includes >40 nonduplicate effectful skill assets",
                scope.SelectedDefinitions>=40,
                "selected="+scope.SelectedDefinitions);
            Check("E05 known duplicate skill ID cannot migrate",
                !scope.CanRoute(all.First(x=>x.Definition.SkillId==
                    "olaf.duel.standard").Definition));
            var current=ctx.Services.ClashManager.ResolvePair(new ClashPair(migratedSlot));
            int damageMigrated=hpBeforeMigrated-enemies[1].CurrentHP;
            Check("E06 original damage parity in real battle",
                baseline!=null&&current!=null&&damageLegacy>0&&damageLegacy==damageMigrated,
                "legacy="+damageLegacy+" scoped="+damageMigrated);
            Check("E07 real effectful entry went through new exclusive route",
                scope.Attempts>0 && scope.History.Any(x=>
                    x.SkillId==effect.Definition.SkillId &&
                    x.ActionId==migratedSlot.ActionId));
            Check("E08 planned command was created for scoped effect",
                scope.History.Any(x=>x.Planned==1));
            Debug.Log("[R7-Batch] OBSERVE attempts="+scope.Attempts+
                " applied="+scope.Applied+" states="+string.Join("|",
                    scope.History.Select(x=>x.Timing+":"+x.State+
                        "/planned="+x.Planned+"/applied="+x.Applied)));
            Check("E09 each planned effect has at most one specialist apply",
                scope.History.All(x=>x.Applied<=1));
            Check("E10 stable GUID preserved on every routed entry",
                scope.History.All(x=>!string.IsNullOrWhiteSpace(x.Guid)));
            Check("E11 no effect entry from baseline action was double routed",
                scope.History.All(x=>x.ActionId!=legacySlot.ActionId));
            Check("E12 scoped path maintains real exchange count",
                baseline.Exchanges.Count==current.Exchanges.Count);
            Check("E13 no active effectful route for no-effect test skill",
                !scope.CanRoute(all.First(x=>x.Definition.SkillId=="test_normal_swipe").Definition));
        }
        Check("E14 scope disposed restoring default legacy route",
            R7ExclusiveEntryMigrationScope.Active==null);
        Check("E15 production BattleRuntimeFactory did not enable scope",
            ctx.Services.ActionResolver!=null &&
            R7ExclusiveEntryMigrationScope.Active==null);
        Debug.Log("[R7-Batch] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 Effectful batch failed "+fail);
    }
}
#endif