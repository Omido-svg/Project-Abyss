#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7EffectfulMultiAssetParity20261030
{
    private static ActionSlot Make(BattleContext ctx,Character owner,
        Character target,SkillDefinition data,long id)
    {
        var skill=data.CreateRuntimeSkill();skill.Initialize(owner,ctx._battleEvent);
        return new ActionSlot
        {
            ActionId=id,Owner=owner,TargetCharacter=target,
            TargetPart=target.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Part=owner.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            Skill=skill,Phase=ActionPhase.COMBAT,SlotId="R7-MULTI-"+id
        };
    }
    [MenuItem("Game System Verification/R7 - 3 Effectful Assets Parity (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode only");
        var ctx=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var owner=ctx?.Player;
        var targets=ctx?.Enemies?.Where(x=>x!=null&&!x.IsDead&&!x.UsesBodyParts).Take(2).ToArray();
        if(owner==null||targets?.Length<2)
            throw new InvalidOperationException("2 normal enemies required");
        var assets=AssetDatabase.FindAssets("t:SkillDefinition").Select(g=>
            new R7SkillAssetRef(AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g)),g))
            .Where(x=>x.Definition!=null).ToArray();
        var names=new[]{"yujin.normal.ledger_cleanup","yujin.normal.step_back",
            "yujin.normal.familiar_hand"};
        int pass=0,fail=0;
        void Check(string name,bool ok,string extra="")
        {
            if(ok){pass++;Debug.Log("[R7-Multi] PASS "+name+" "+extra);}
            else{fail++;Debug.LogError("[R7-Multi] FAIL "+name+" "+extra);}
        }

        int caseIndex=0;
        foreach(var id in names)
        {
            caseIndex++;
            var asset=assets.Single(x=>x.Definition.SkillId==id);
            var data=asset.Definition;
            Check("M"+caseIndex+"A authored effect selected "+id,
                data.ActionType==ActionType.NormalAttack &&
                data.EffectEntries.Any(x=>x?.Definition!=null));
            if(targets.Any(x=>x.IsDead))break;
            int hp0=targets[0].CurrentHP,hp1=targets[1].CurrentHP;
            Check("M"+caseIndex+"B enemy preconditions still mirror",
                hp0==hp1,"hp="+hp0+"/"+hp1);
            long rootId=998000+caseIndex*10;
            var left=Make(ctx,owner,targets[0],data,rootId);
            var right=Make(ctx,owner,targets[1],data,rootId+1);
            UnityEngine.Random.InitState(44910+caseIndex);
            var rng=UnityEngine.Random.state;
            var legacy=ctx.Services.ClashManager.ResolvePair(new ClashPair(left));
            int before=hp0-targets[0].CurrentHP;
            UnityEngine.Random.state=rng;
            using(var scope=R7ExclusiveEntryMigrationScope.Begin(assets))
            {
                var scoped=ctx.Services.ClashManager.ResolvePair(new ClashPair(right));
                int after=hp1-targets[1].CurrentHP;
                Check("M"+caseIndex+"C exact actual damage parity",
                    legacy!=null&&scoped!=null&&before==after,
                    "skill="+id+" legacy="+before+" scoped="+after);
                Check("M"+caseIndex+"D actual effect applied once per timing",
                    scope.Attempts>=1 && scope.Applied>=1 &&
                    scope.History.Where(x=>x.Applied>0).All(x=>x.Planned==1),
                    "applied="+scope.Applied+" attempts="+scope.Attempts);
                Check("M"+caseIndex+"E root GUID and identity consistent",
                    scope.History.All(x=>x.Guid==asset.AssetGuid && x.SkillId==id));
                Check("M"+caseIndex+"F same number of domain exchanges",
                    legacy.Exchanges.Count==scoped.Exchanges.Count);
            }
        }
        Check("M_END no scope leakage",R7ExclusiveEntryMigrationScope.Active==null);
        Debug.Log("[R7-Multi] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R7 multi-asset failed "+fail);
    }
}
#endif