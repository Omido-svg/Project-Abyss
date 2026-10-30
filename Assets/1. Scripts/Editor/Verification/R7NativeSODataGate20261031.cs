#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R7NativeSODataGate20261031
{
    [MenuItem("Game System Verification/R7 Native SO - 24 Data Gate (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit mode required");
        var all=AssetDatabase.FindAssets("t:SkillDefinition").Select(g=>new{
            Guid=g,Path=AssetDatabase.GUIDToAssetPath(g),
            Data=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g))}).Where(x=>x.Data!=null).ToArray();
        var duplicates=all.GroupBy(x=>x.Data.SkillId,StringComparer.Ordinal)
            .Where(x=>x.Count()>1).Select(x=>x.Key)
            .ToHashSet(StringComparer.Ordinal);
        var resources=Resources.LoadAll<R7NativeAbilityDefinition>(
            "ProjectAbyss/NativeAbilities");
        var statuses=Resources.LoadAll<R7NativeStatusAbilityDefinition>(
            "ProjectAbyss/NativeStatuses");
        int pass=0,fail=0;
        void Check(string id,bool yes,string detail="")
        {
            if(yes){pass++;Debug.Log("[R7-DataGate] PASS "+id+" "+detail);}
            else{fail++;Debug.LogError("[R7-DataGate] FAIL "+id+" "+detail);}
        }
        Check("D01 native resource assets exactly 15",resources.Length==15);
        Check("D02 native status assets exactly 9",statuses.Length==9);
        var ids=resources.Select(x=>x.StableId)
            .Concat(statuses.Select(x=>x.StableId)).ToArray();
        Check("D03 no native duplicate stable ID",
            ids.Distinct(StringComparer.Ordinal).Count()==24);
        Check("D04 no native ID collides with duplicate legacy",
            ids.All(x=>!duplicates.Contains(x)));
        Check("D05 every graph points to one existing source asset",
            ids.All(id=>all.Count(x=>x.Data.SkillId==id)==1));
        bool resourceValid=resources.All(n=>
        {
            var src=all.Single(x=>x.Data.SkillId==n.StableId);
            var e=src.Data.EffectEntries.Single();
            var proxy=e.Definition as TempBalanceSkillProxyEffectDefinition;
            var kind=proxy?.Operation==TempBalanceSkillProxyOperation.GainBlock
                ?R7NativeResourceKind.Block:
                proxy?.Operation==TempBalanceSkillProxyOperation.GainPrestige
                ?R7NativeResourceKind.Prestige:
                R7NativeResourceKind.TurnClashPower;
            return n.IsValid&&n.SourceAssetGuid==src.Guid&&
                n.SourceAssetName==src.Data.name&&n.Operation==kind&&
                n.Trigger==(e.EffectiveTiming==SkillEffectTiming.OnExecute
                    ?AbilityEventKind.RollUsing:AbilityEventKind.ExchangeWon)&&
                n.Level0Amount==R7NativeResourceAssetMigration20261031.ResolveAmount(src.Data,0)&&
                n.Level1Amount==R7NativeResourceAssetMigration20261031.ResolveAmount(src.Data,1)&&
                n.Level2Amount==R7NativeResourceAssetMigration20261031.ResolveAmount(src.Data,2);
        });
        Check("D06 all 15 compiled resource values/timing/GUID match",resourceValid);
        bool statusValid=statuses.All(n=>
        {
            var src=all.Single(x=>x.Data.SkillId==n.StableId);
            var e=src.Data.EffectEntries.Single();
            return n.IsValid&&n.SourceAssetGuid==src.Guid&&
                n.SourceAssetName==src.Data.name&&
                n.Trigger==(e.EffectiveTiming==SkillEffectTiming.OnExecute
                    ?AbilityEventKind.RollUsing:AbilityEventKind.ExchangeWon)&&
                n.Level0Stacks==R7NativeStatusAssetMigration20261031.ResolveStacks(src.Data,0)&&
                n.Level1Stacks==R7NativeStatusAssetMigration20261031.ResolveStacks(src.Data,1)&&
                n.Level2Stacks==R7NativeStatusAssetMigration20261031.ResolveStacks(src.Data,2);
        });
        Check("D07 all 9 compiled status stacks/timing/GUID match",statusValid);
        Check("D08 native graphs have no SkillDefinition fields",
            typeof(R7NativeAbilityDefinition).GetFields(
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.Public|
                System.Reflection.BindingFlags.NonPublic)
                .All(f=>f.FieldType!=typeof(SkillDefinition)) &&
            typeof(R7NativeStatusAbilityDefinition).GetFields(
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.Public|
                System.Reflection.BindingFlags.NonPublic)
                .All(f=>f.FieldType!=typeof(SkillDefinition)));
        Check("D09 native graph runtime Resource count",
            R7ProductionNativeResourceMigration.ApprovedSkillCount==15);
        Check("D10 native graph runtime Bleeding count",
            R7ProductionNativeBleedingMigration.ApprovedSkillCount==9);

        var csv=new StringBuilder("StableId,NativeAssetPath,SourceAssetPath,SourceGuid,Operation,Trigger,Level0,Level1,Level2\n");
        string Q(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
        foreach(var n in resources.OrderBy(x=>x.StableId,StringComparer.Ordinal))
        {
            var x=all.Single(x=>x.Data.SkillId==n.StableId);
            csv.AppendLine(string.Join(",",new[]{
                n.StableId,AssetDatabase.GetAssetPath(n),x.Path,
                n.SourceAssetGuid,n.Operation.ToString(),n.Trigger.ToString(),
                n.Level0Amount.ToString(),n.Level1Amount.ToString(),
                n.Level2Amount.ToString()
            }.Select(Q)));
        }
        foreach(var n in statuses.OrderBy(x=>x.StableId,StringComparer.Ordinal))
        {
            var x=all.Single(x=>x.Data.SkillId==n.StableId);
            csv.AppendLine(string.Join(",",new[]{
                n.StableId,AssetDatabase.GetAssetPath(n),x.Path,
                n.SourceAssetGuid,"Bleeding",n.Trigger.ToString(),
                n.Level0Stacks.ToString(),n.Level1Stacks.ToString(),
                n.Level2Stacks.ToString()
            }.Select(Q)));
        }
        string root=Directory.GetParent(Application.dataPath).FullName;
        var manifest=Path.Combine(root,"ProjectAbyss_R7NativeSO_24_Audit_20261031.csv");
        File.WriteAllText(manifest,csv.ToString(),new UTF8Encoding(false));
        Check("D11 auditable manifest exactly 24 definitions",
            File.ReadAllLines(manifest).Length==25);
        Debug.Log("[R7-DataGate] RESULT PASS="+pass+" FAIL="+fail+
            " manifest="+manifest);
        if(fail!=0)throw new InvalidOperationException(
            "Native R7 data gate failed "+fail);
    }
}
#endif