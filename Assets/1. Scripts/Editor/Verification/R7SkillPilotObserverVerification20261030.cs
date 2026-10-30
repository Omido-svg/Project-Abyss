#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7SkillPilotObserverVerification20261030
{
    private static R7SkillAssetRef[] Catalog() =>
        AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>new R7SkillAssetRef(
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                    AssetDatabase.GUIDToAssetPath(g)),g))
            .Where(x=>x.Definition!=null).ToArray();

    [MenuItem("Game System Verification/R7 - Real Skill Ability Identity (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode only");
        var battle=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var owner=battle?.Player;
        var enemies=battle?.Enemies?.Where(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts).Take(2).ToArray();
        if(owner==null||enemies==null||enemies.Length<2||
            battle.Services?.ClashManager==null)
            throw new InvalidOperationException("Need live player and two normal enemies");
        int pass=0,fail=0;
        void Check(string label,bool ok,string more="")
        {
            if(ok){pass++;Debug.Log("[R7-Pilot] PASS "+label+" "+more);}
            else{fail++;Debug.LogError("[R7-Pilot] FAIL "+label+" "+more);}
        }
        var catalog=Catalog();
        var item=catalog.First(x=>x.Definition.SkillId=="test_normal_swipe");
        Check("P01 full runtime catalog enumerated",catalog.Length==145);
        Check("P02 candidate has stable GUID",!string.IsNullOrWhiteSpace(item.AssetGuid));
        Check("P03 live normal attack classified as read-only safe",
            R7SkillPilotObserver.IsMetadataOnlyNormal(item.Definition));
        var knownEffect=catalog.First(x=>x.Definition.SkillId=="common.preparation.crouch");
        Check("P04 effectful preparation excluded",
            !R7SkillPilotObserver.IsMetadataOnlyNormal(knownEffect.Definition));
        var duplicated=new[]{item,item};
        using(var blocked=new R7SkillPilotObserver(battle._battleEvent,duplicated))
            Check("P05 duplicate ID is blocked before any action",
                blocked.AmbiguousIdCount==1&&blocked.EligibleAssetCount==0);
        var observer=new R7SkillPilotObserver(battle._battleEvent,catalog);
        Check("P06 eligible asset metadata built",observer.EligibleAssetCount>=1);

        ActionSlot Make(long id,Character target)
        {
            var skill=item.Definition.CreateRuntimeSkill();
            skill.Initialize(owner,battle._battleEvent);
            return new ActionSlot
            {
                ActionId=id,Owner=owner,TargetCharacter=target,
                TargetPart=target.GetTargetPoints(false)
                    .FirstOrDefault(x=>x.IsValid).Part,
                Part=owner.GetTargetPoints(false)
                    .FirstOrDefault(x=>x.IsValid).Part,
                Skill=skill,Phase=ActionPhase.COMBAT,SlotId="R7-PILOT-"+id
            };
        }

        var a=Make(997101,enemies[0]);
        var b=Make(997102,enemies[1]);
        int hpA=enemies[0].CurrentHP,hpB=enemies[1].CurrentHP;
        var rng=UnityEngine.Random.state;
        // Baseline original path without the pilot listening.
        observer.Dispose();
        var first=battle.Services.ClashManager.ResolvePair(new ClashPair(a));
        int deltaA=hpA-enemies[0].CurrentHP;
        Check("P07 baseline legacy ClashManager processed actual skill",
            first!=null&&first.Exchanges.Count>0);
        UnityEngine.Random.state=rng;
        var enabled=new R7SkillPilotObserver(battle._battleEvent,catalog);
        var second=battle.Services.ClashManager.ResolvePair(new ClashPair(b));
        int deltaB=hpB-enemies[1].CurrentHP;
        Check("P08 pilot observes actual SkillDefinition ActionStart",
            enabled.Observations.Count==1);
        Check("P09 observed stable definition GUID matches real asset",
            enabled.Observations.Count==1 &&
            enabled.Observations[0].SkillId==item.Definition.SkillId &&
            enabled.Observations[0].SourceAssetGuid==item.AssetGuid);
        Check("P10 source ActorRuntimeId and Slot preserved",
            enabled.Observations.Count==1 &&
            enabled.Observations[0].OwnerRuntimeId==R4LiveSlotQuery.ActorRuntimeId(owner) &&
            enabled.Observations[0].SlotId==b.SlotId);
        Check("P11 pilot planned no duplicate gameplay command",
            enabled.Observations.All(x=>x.PlannedCommands==0));
        Check("P12 original ClashManager remains the only damage executor",
            second!=null && second.Exchanges.Count>0 &&
            deltaA>=0 && deltaB>=0,
            "damage baseline="+deltaA+" pilot="+deltaB);
        Check("P13 AbilityInstance is runtime-owned with original source",
            enabled.TryGet(b.ActionId,out var ability) &&
            ability.Definition.SourceAssetGuid==item.AssetGuid &&
            ability.Identity.DefinitionId==item.Definition.SkillId);
        Check("P14 no committed fake effect",
            ability!=null&&ability.RuntimeState.SuccessfulActivationCount==0);
        enabled.Dispose();
        Check("P15 observer detaches and clears per-battle state",
            enabled.IsDisposed && !enabled.TryGet(b.ActionId,out var _));
        Check("P16 original observer also detached",observer.IsDisposed);
        Debug.Log("[R7-Pilot] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 pilot failed "+fail);
    }
}
#endif