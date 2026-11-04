#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// Read original authoring, assert upgrade payloads, write 5 independent native
/// SOs. Never edit original SkillDefinitions/effect templates/GUIDs.
/// </summary>
public static class NativeUniqueGaugeAssetMigration
{
    private const string Folder="Assets/2. Data/Resources/ProjectAbyss/NativeUniqueGauges";
    private sealed class Spec
    {
        public string SkillId,Guid,Name;
        public UniqueGaugeKind Kind;
        public AbilityEventKind Trigger;
        public int L1,L2;
        public Spec(string id,string guid,string name,UniqueGaugeKind kind,
            AbilityEventKind trigger,int l1,int l2)
        {
            SkillId=id;Guid=guid;Name=name;Kind=kind;Trigger=trigger;
            L1=l1;L2=l2;
        }
    }
    private static readonly Spec[] Specs={
        new Spec("olaf.duel.mad_bite","1ecdb0f93b9ad3e4a921d7534ba253c5",
            "Olaf_D_04",UniqueGaugeKind.OlafMadness,AbilityEventKind.ExchangeWon,2,3),
        new Spec("olaf.duel.self_cut","c870a452bdc56294ab616730fb329c09",
            "Olaf_D_07",UniqueGaugeKind.OlafMadness,AbilityEventKind.ExchangeWon,2,3),
        new Spec("olaf.preparation.last_resistance","99837cafaea66dd4ea77de4b1a3e703a",
            "Olaf_P_07",UniqueGaugeKind.OlafMadness,AbilityEventKind.RollUsing,2,3),
        new Spec("olaf.prestige.berserk","5a8949af28612984f97b8167c040c398",
            "Olaf_R_02",UniqueGaugeKind.OlafMadness,AbilityEventKind.RollUsing,2,3),
        new Spec("yujin.duel.borrow_weapon","620a7e18873bb554297c5f3a4421b983",
            "Yujin_D_10",UniqueGaugeKind.YujinSense,AbilityEventKind.ExchangeWon,2,2)
    };

    private static string AssetPath(Spec spec)=>
        Folder+"/"+spec.SkillId.Replace('.','_')+".asset";

    private static SkillDefinition Authoring(Spec spec)
    {
        var path=AssetDatabase.GUIDToAssetPath(spec.Guid);
        var s=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if(s==null||s.SkillId!=spec.SkillId||s.name!=spec.Name ||
           s.EffectEntries?.Count!=1||s.Effects?.Any(x=>x!=null)==true)
            throw new InvalidOperationException("Source GUID/SkillId/name or entries mismatch: "+spec.SkillId);
        var entry=s.EffectEntries[0];
        if(!(entry.Definition is TempBalanceSkillProxyEffectDefinition effect)||
           effect.Operation!=(spec.Kind==UniqueGaugeKind.OlafMadness
               ?ProvisionalBalanceSkillProxyOperation.OlafMadness
               :ProvisionalBalanceSkillProxyOperation.YujinSense) ||
           effect.BaseAmount!=1 || entry.Conditions?.Count>0 ||
           entry.RestrictToRoll || entry.Overrides?.OverrideAmount==true ||
           entry.UpgradeKey!="temp_proxy" ||
           entry.EffectiveTiming!=(spec.Trigger==AbilityEventKind.ExchangeWon
               ?SkillEffectTiming.OnExchangeWin:SkillEffectTiming.OnExecute))
            throw new InvalidOperationException("Authoring changed: "+spec.SkillId);
        if(s.UpgradeProfile==null)
            throw new InvalidOperationException("Source missing upgrades: "+spec.SkillId);
        int AmountFor(SkillUpgradeStep step)
        {
            var payload=step?.EffectPayloads?.SingleOrDefault(x=>
                x.TargetEffectKey=="temp_proxy");
            if(payload==null||!payload.OverrideAmount)
                throw new InvalidOperationException("Effect upgrade not exact: "+spec.SkillId);
            return payload.Amount;
        }
        if(AmountFor(s.UpgradeProfile.Upgrade1)!=spec.L1 ||
           AmountFor(s.UpgradeProfile.Upgrade2)!=spec.L2)
            throw new InvalidOperationException("Upgrade amount mismatch: "+spec.SkillId);
        return s;
    }

    public static void Stage()
    {
        if(Application.isPlaying)throw new InvalidOperationException("EditMode required");
        if(!AssetDatabase.IsValidFolder(Folder))
        {
            var root="Assets/2. Data/Resources/ProjectAbyss";
            AssetDatabase.CreateFolder(root,"NativeUniqueGauges");
        }
        int created=0;
        foreach(var spec in Specs)
        {
            Authoring(spec);
            var path=AssetPath(spec);
            var existing=AssetDatabase.LoadAssetAtPath<R7NativeUniqueGaugeDefinition>(path);
            if(existing!=null)continue;
            if(File.Exists(path))
                throw new InvalidOperationException("Unrecognized file exists: "+path);
            var graph=ScriptableObject.CreateInstance<R7NativeUniqueGaugeDefinition>();
            graph.SchemaVersion=1;
            graph.StableId=spec.SkillId;
            graph.SourceAssetGuid=spec.Guid;
            graph.SourceAssetName=spec.Name;
            graph.Kind=spec.Kind;
            graph.Trigger=spec.Trigger;
            graph.Level0Amount=1;graph.Level1Amount=spec.L1;graph.Level2Amount=spec.L2;
            graph.RestrictToRoll=false;graph.RollNumber=1;
            AssetDatabase.CreateAsset(graph,path);
            created++;
        }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        R7ProductionNativeUniqueGaugeMigration.ResetCatalogCache();
        Verify();
        Debug.Log("[R7-GaugeMigration] STAGED="+created+" (existing preserved)");
    }

    public static void Verify()
    {
        if(Application.isPlaying)throw new InvalidOperationException("EditMode required");
        int pass=0,fail=0;
        void Check(string id,bool yes,string desc)
        {
            if(yes){pass++;Debug.Log("[R7-GaugeData] PASS "+id+" "+desc);}
            else{fail++;Debug.LogError("[R7-GaugeData] FAIL "+id+" "+desc);}
        }
        var loaded=Resources.LoadAll<R7NativeUniqueGaugeDefinition>(
            "ProjectAbyss/NativeUniqueGauges");
        Check("D01 exactly five graphs",loaded.Length==5,"found="+loaded.Length);
        Check("D02 unique IDs and GUIDs",
            loaded.Select(x=>x.StableId).Distinct().Count()==5 &&
            loaded.Select(x=>x.SourceAssetGuid).Distinct().Count()==5,"5 unique identities");
        foreach(var s in Specs)
        {
            var graph=AssetDatabase.LoadAssetAtPath<R7NativeUniqueGaugeDefinition>(
                AssetPath(s));
            var source=Authoring(s);
            Check("D "+s.SkillId,graph!=null&&graph.IsValid&&
                graph.StableId==source.SkillId &&
                graph.SourceAssetGuid==s.Guid&&graph.SourceAssetName==source.name&&
                graph.Kind==s.Kind&&graph.Trigger==s.Trigger&&
                graph.Level0Amount==1&&graph.Level1Amount==s.L1&&
                graph.Level2Amount==s.L2&&!graph.RestrictToRoll,
                "source GUID, timing and three levels");
        }
        Check("D08 production native catalog five",
            R7ProductionNativeUniqueGaugeMigration.ApprovedSkillCount==5,
            "routing count");
        Debug.Log("[R7-GaugeData] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("Native unique gauge data failed "+fail);
    }
}
#endif
