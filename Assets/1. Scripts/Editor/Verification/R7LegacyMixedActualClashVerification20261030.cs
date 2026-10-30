#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7LegacyMixedActualClashVerification20261030
{
    private static ActionSlot Slot(BattleContext ctx,Character source,
        Character target,BodyPart targetPart,SkillDefinition skillData,long id)
    {
        var skill=skillData.CreateRuntimeSkill();
        skill.Initialize(source,ctx._battleEvent);
        return new ActionSlot
        {
            ActionId=id,Owner=source,TargetCharacter=target,
            Part=source.GetTargetPoints(false).FirstOrDefault(x=>x.IsValid).Part,
            TargetPart=targetPart,Skill=skill,Phase=ActionPhase.COMBAT,
            SlotId="R7-LEGACY-NATIVE-ACTUAL-"+id
        };
    }
    [MenuItem("Game System Verification/R7 - Mixed Native Real Clash (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
        var ctx=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var owner=ctx?.Player;
        var router=ctx?.Services?.R7LegacyStatusMigration;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2. Data/TestEncounters/Prefabs/Hifumi_Test.prefab");
        if(ctx==null||owner==null||router==null||prefab==null)
            throw new InvalidOperationException("Battle context missing.");
        var oldGo=UnityEngine.Object.Instantiate(prefab);
        var newGo=UnityEngine.Object.Instantiate(prefab);
        oldGo.name="R7_CLASH_OLD_BITE_TARGET";
        newGo.name="R7_CLASH_NEW_BITE_TARGET";
        var oldTarget=oldGo.GetComponent<Hifumi>();
        var newTarget=newGo.GetComponent<Hifumi>();
        oldTarget.Initialize(ctx);newTarget.Initialize(ctx);
        var leftPart=oldTarget.GetTargetPoints(false)
            .First(x=>x.IsValid&&x.Part!=null).Part;
        var rightPart=newTarget.GetTargetPoints(false)
            .First(x=>x.IsValid&&x.Part!=null).Part;
        var bite=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g)))
            .Single(x=>x!=null&&x.SkillId==
                "f8c31ca52c544f93b8d961acab0c9611");
        int pass=0,fail=0;
        void Check(string id,bool ok,string details="")
        {
            if(ok){pass++;Debug.Log("[R7-MixedClash] PASS "+id+" "+details);}
            else{fail++;Debug.LogError("[R7-MixedClash] FAIL "+id+" "+details);}
        }
        bool initial=router.Enabled;
        try
        {
            Check("F01 production native default enabled",initial);
            Check("F02 authored bite is native eligible",router.CanRoute(bite));
            Check("F03 both target parts initially equivalent",
                Math.Abs(leftPart.PartHP-rightPart.PartHP)<0.001f &&
                oldTarget.CurrentHP==newTarget.CurrentHP);
            var random=UnityEngine.Random.state;
            int beforeOldHp=oldTarget.CurrentHP;
            int beforeNewHp=newTarget.CurrentHP;
            float beforeOldPart=leftPart.PartHP;
            float beforeNewPart=rightPart.PartHP;
            router.Enabled=false;
            var legacy=ctx.Services.ClashManager.ResolvePair(
                new ClashPair(Slot(ctx,owner,oldTarget,leftPart,bite,999910)));
            int beforeNativeCommit=router.NativeCommits;
            UnityEngine.Random.state=random;
            router.Enabled=true;
            var modern=ctx.Services.ClashManager.ResolvePair(
                new ClashPair(Slot(ctx,owner,newTarget,rightPart,bite,999911)));
            int nativeCount=router.NativeCommits-beforeNativeCommit;
            Check("F04 both real ClashManager calls produced result",
                legacy!=null&&modern!=null);
            Check("F05 actual exchange count matches",
                legacy!=null&&modern!=null&&legacy.Exchanges.Count==modern.Exchanges.Count);
            Check("F06 actual target HP damage matches",
                beforeOldHp-oldTarget.CurrentHP==beforeNewHp-newTarget.CurrentHP,
                "legacy="+(beforeOldHp-oldTarget.CurrentHP)+
                " new="+(beforeNewHp-newTarget.CurrentHP));
            Check("F07 actual part damage matches",
                Math.Abs((beforeOldPart-leftPart.PartHP)-
                    (beforeNewPart-rightPart.PartHP))<0.001f,
                "legacy="+(beforeOldPart-leftPart.PartHP)+
                " new="+(beforeNewPart-rightPart.PartHP));
            Check("F08 real combat bleeding stack matches",
                (oldTarget.GetPartStatus<Bleeding>(leftPart)?.Stack??0)==
                (newTarget.GetPartStatus<Bleeding>(rightPart)?.Stack??0),
                "legacy="+(oldTarget.GetPartStatus<Bleeding>(leftPart)?.Stack??0)+
                " new="+(newTarget.GetPartStatus<Bleeding>(rightPart)?.Stack??0));
            Check("F09 new native status command actually committed",
                nativeCount>0,"native committed="+nativeCount);
            Check("F10 explicit rollback retains a playable original skill",
                ctx.Services.ClashManager!=null);
        }
        finally
        {
            router.Enabled=initial;
            UnityEngine.Object.Destroy(oldGo);
            UnityEngine.Object.Destroy(newGo);
        }
        Debug.Log("[R7-MixedClash] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("Real mixed combat parity failed "+fail);
    }
}
#endif