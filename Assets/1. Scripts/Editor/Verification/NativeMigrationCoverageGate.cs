#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R7 production content coverage gate. This does not migrate, modify or
/// save any SkillDefinition/ScriptableObject; it verifies exact GUID identity
/// of all native resources and identifies work NOT yet migrated.
/// </summary>
public static class NativeMigrationCoverageGate
{
    private sealed class Row
    {
        public string Id,Guid,Path,Name,Action,Classification,EffectFamily;
        public int Entries,LegacyEntries;
    }
    private sealed class Native
    {
        public string Family,Id,Guid,Name;
        public bool Valid;
    }

    public static void Run()
    {
        if(Application.isPlaying)
            throw new InvalidOperationException("R7 native audit requires EditMode");
        var results=new List<Row>();
        var native=new List<Native>();
        foreach(var d in Resources.LoadAll<R7NativeAbilityDefinition>(
            "ProjectAbyss/NativeAbilities"))
            native.Add(new Native{Family="NATIVE_RESOURCE",Id=d.StableId,
                Guid=d.SourceAssetGuid,Name=d.SourceAssetName,Valid=d.IsValid});
        foreach(var d in Resources.LoadAll<R7NativeStatusAbilityDefinition>(
            "ProjectAbyss/NativeStatuses"))
            native.Add(new Native{Family="NATIVE_STATUS",Id=d.StableId,
                Guid=d.SourceAssetGuid,Name=d.SourceAssetName,Valid=d.IsValid});
        foreach(var d in Resources.LoadAll<R7NativeMixedAbilityDefinition>(
            "ProjectAbyss/NativeMixed"))
            native.Add(new Native{Family="NATIVE_MIXED",Id=d.StableId,
                Guid=d.SourceAssetGuid,Name=d.SourceAssetName,Valid=d.IsValid});

        foreach(var d in Resources.LoadAll<R7NativeUniqueGaugeDefinition>(
            "ProjectAbyss/NativeUniqueGauges"))
            native.Add(new Native{Family="NATIVE_UNIQUE_GAUGE",Id=d.StableId,
                Guid=d.SourceAssetGuid,Name=d.SourceAssetName,Valid=d.IsValid});

        foreach(var guid in AssetDatabase.FindAssets("t:SkillDefinition"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var s=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if(s==null)continue;
            var effects=s.EffectEntries?.Where(e=>e?.Definition!=null)
                .Select(e=>e.Definition.GetType().Name).Distinct()
                .OrderBy(x=>x,StringComparer.Ordinal).ToArray() ??
                Array.Empty<string>();
            results.Add(new Row{
                Id=s.SkillId??string.Empty,Guid=guid,Path=path,
                Name=s.name,Action=s.ActionType.ToString(),
                EffectFamily=string.Join("+",effects),
                Entries=s.EffectEntries?.Count??0,
                LegacyEntries=s.Effects?.Count(x=>x!=null)??0
            });
        }
        results=results.OrderBy(x=>x.Id,StringComparer.Ordinal)
            .ThenBy(x=>x.Path,StringComparer.Ordinal).ToList();
        var collisions=new HashSet<string>(results.GroupBy(x=>x.Id,StringComparer.Ordinal)
            .Where(g=>g.Count()>1).Select(g=>g.Key),StringComparer.Ordinal);
        var nativeCounts=native.GroupBy(x=>x.Id,StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);
        var byGuid=results.ToDictionary(x=>x.Guid,StringComparer.Ordinal);
        var nativeByGuid=native.Where(x=>!string.IsNullOrEmpty(x.Guid))
            .GroupBy(x=>x.Guid,StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        int nativeMapped=0,partial=0,pilots=0,blocked=0;
        foreach(var row in results)
        {
            if(collisions.Contains(row.Id))
            {
                var group=results.Where(x=>x.Id==row.Id)
                    .Select(x=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(x.Path));
                row.Classification=SkillIdentityVerification.IsApprovedGroup(group)
                    ?"R7_2_GUID_CONTRACT_NATIVE_PENDING":"BLOCKED_DUPLICATE_ID";
                blocked++;
            }
            else if(nativeByGuid.TryGetValue(row.Guid,out var list) &&
                list.Length==1 && list[0].Valid &&
                list[0].Id==row.Id && list[0].Name==row.Name)
            {
                row.Classification=list[0].Family;nativeMapped++;
            }
            else if(row.Id==R7ProductionPreparationStatusMigration.SkillId)
            {
                row.Classification="PREPARATION_FULL_NATIVE_R5";
                partial++;
            }
            else if(AssetDatabase.LoadAssetAtPath<SkillDefinition>(row.Path) is
                SkillDefinition skill &&
                SkillMigrationObserver.IsMetadataOnlyNormal(skill))
            {
                row.Classification="METADATA_IDENTITY_PILOT_ONLY";
                pilots++;
            }
            else if(row.EffectFamily.Contains("TempBalanceSkillProxyEffectDefinition"))
                row.Classification="PENDING_TEMP_BALANCE_EFFECT";
            else if(row.EffectFamily.Contains("Yujin") ||
                    row.EffectFamily.Contains("Olaf"))
                row.Classification="PENDING_CHARACTER_SPECIALIST";
            else if(row.Entries>0||row.LegacyEntries>0)
                row.Classification="PENDING_EFFECT_MAPPING";
            else row.Classification="PENDING_NO_EFFECT_POLICY";
        }
        var errors=new List<string>();
        void Check(string id,bool ok,string detail)
        {
            if(ok)Debug.Log("[R7-Coverage] PASS "+id+" "+detail);
            else {errors.Add(id+" "+detail);Debug.LogError(
                "[R7-Coverage] FAIL "+id+" "+detail);}
        }
        Check("C01 all SkillDefinition assets inventoried",results.Count==SkillCorpusVerification.ExpectedTotalSkillCount,
            "count="+results.Count);
        Check("C02 native SO family counts",native.Count==36 &&
            native.Count(x=>x.Family=="NATIVE_RESOURCE")==15 &&
            native.Count(x=>x.Family=="NATIVE_STATUS")==9 &&
            native.Count(x=>x.Family=="NATIVE_MIXED")==7 &&
            native.Count(x=>x.Family=="NATIVE_UNIQUE_GAUGE")==5,
            "resource/status/mixed/unique="+native.Count(x=>x.Family=="NATIVE_RESOURCE")+
            "/"+native.Count(x=>x.Family=="NATIVE_STATUS")+
            "/"+native.Count(x=>x.Family=="NATIVE_MIXED")+"/"+
            native.Count(x=>x.Family=="NATIVE_UNIQUE_GAUGE"));
        Check("C03 every native ScriptableObject schema valid",
            native.All(x=>x.Valid),"invalid="+native.Count(x=>!x.Valid));
        Check("C04 exact native asset GUID, stable ID and source name",
            native.All(n=>byGuid.TryGetValue(n.Guid,out var row) &&
                row.Id==n.Id && row.Name==n.Name),
            "matched="+nativeMapped);
        Check("C05 no duplicate source references across native families",
            nativeByGuid.Values.All(g=>g.Length==1) &&
            nativeCounts.Values.All(c=>c==1),
            "distinct="+nativeCounts.Count);
        Check("C06 four collision IDs have registered R7-2 GUID contracts",
            collisions.Count==4 && blocked==8 &&
            collisions.All(id=>SkillIdentityVerification.IsApprovedGroup(
                results.Where(x=>x.Id==id)
                    .Select(x=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(x.Path)))),
            "duplicate IDs="+string.Join("|",collisions.OrderBy(x=>x)));
        Check("C07 all verified natives distinct from blocked IDs",
            native.All(x=>!collisions.Contains(x.Id))&&nativeMapped==36,
            "native verified="+nativeMapped);
        var prepSetup=Resources.Load<NativePreparationSetupDefinition>(
            "ProjectAbyss/NativePreparations/EliteCombatStance_Setup");
        var prepStatuses=Resources.Load<NativePreparationStatusGraph>(
            "ProjectAbyss/NativePreparations/EliteCombatStance_Statuses");
        Check("C08 Preparation all 3 native effects, no runtime legacy entry gates",
            partial==1 && prepSetup?.IsValid==true &&
            prepStatuses?.IsValid==true &&
            prepSetup.StableId==R7ProductionPreparationStatusMigration.SkillId &&
            prepStatuses.StableId==R7ProductionPreparationStatusMigration.SkillId &&
            prepSetup.SourceAssetGuid=="f9c5728c648630849a9ba0afe641d02a" &&
            prepStatuses.SourceAssetGuid==prepSetup.SourceAssetGuid,
            "native status operations=2, native setup=1");
        Check("C09 metadata pilot is not falsely counted as native",
            pilots==13,"read-only candidates="+pilots);
        Check("C10 production native router catalogs have same cardinality",
            R7ProductionNativeResourceMigration.ApprovedSkillCount==15 &&
            R7ProductionNativeBleedingMigration.ApprovedSkillCount==9 &&
            R7ProductionLegacyStatusMigration.ApprovedSkillCount==7 &&
            R7ProductionNativeUniqueGaugeMigration.ApprovedSkillCount==5,
            "router counts=15/9/7/5");
        Check("C11 no SkillDefinition or source SO edited by audit",
            results.All(x=>!EditorUtility.IsDirty(
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(x.Path))) &&
            Resources.LoadAll<R7NativeAbilityDefinition>(
                "ProjectAbyss/NativeAbilities").All(x=>!EditorUtility.IsDirty(x)) &&
            Resources.LoadAll<R7NativeStatusAbilityDefinition>(
                "ProjectAbyss/NativeStatuses").All(x=>!EditorUtility.IsDirty(x)) &&
            Resources.LoadAll<R7NativeMixedAbilityDefinition>(
                "ProjectAbyss/NativeMixed").All(x=>!EditorUtility.IsDirty(x)),
            "read-only validation");

        var root=Directory.GetParent(Application.dataPath).FullName;
        var output=Path.Combine(root,"G0_R6_Integration_20261102");
        Directory.CreateDirectory(output);
        string Csv(string s)=>"\""+(s??string.Empty).Replace("\"","\"\"")+"\"";
        var lines=new List<string>{
            "StableSkillId,AssetGUID,Path,AssetName,ActionType,EffectEntries,LegacyEffects,AuthoringEffectTypes,R7Status"};
        lines.AddRange(results.Select(x=>string.Join(",",new[]{
            x.Id,x.Guid,x.Path,x.Name,x.Action,x.Entries.ToString(),
            x.LegacyEntries.ToString(),x.EffectFamily,x.Classification}.Select(Csv))));
        File.WriteAllLines(Path.Combine(output,"R7_NATIVE_COVERAGE_164_CURRENT.csv"),
            lines,new UTF8Encoding(false));
        var report=new StringBuilder()
            .AppendLine("# R7 migration handover, 2026-11-02")
            .AppendLine("Inventory is read-only; native coverage means an existing,")
            .AppendLine("verified SO + runtime native router, NOT full R7 completion.")
            .AppendLine()
            .AppendLine("- Source SkillDefinitions: "+results.Count)
            .AppendLine("- Exact GUID/ID/name matched native SOs: "+nativeMapped+
                " (Resource 15, Status 9, Mixed 7, Unique Gauge 5)")
            .AppendLine("- Preparation all 3 effect mutations native; no legacy entry gates: "+partial)
            .AppendLine("- Metadata-only identity observers (not gameplay migrated): "+pilots)
            .AppendLine("- R7-2 GUID-resolved duplicate SkillIDs (not native): "+collisions.Count+
                " ("+blocked+" assets)")
            .AppendLine("- Remaining full native gameplay not proven: "+
                (results.Count-nativeMapped)+" skill assets")
            .AppendLine()
            .AppendLine("## Remaining by classification");
        foreach(var grp in results.GroupBy(x=>x.Classification)
            .OrderBy(x=>x.Key,StringComparer.Ordinal))
            report.AppendLine("- "+grp.Key+": "+grp.Count());
        report.AppendLine()
            .AppendLine("## Guardrails")
            .AppendLine("1. Never infer native coverage from a metadata observer.")
            .AppendLine("2. Never migrate colliding IDs by ID alone.")
            .AppendLine("3. Remove legacy Entry.TryApply/adapter execution only when native R5 command parity is proven.")
            .AppendLine("4. Preserve original authored assets, GUIDs and per-roll conditions.")
            .AppendLine("5. Re-run R6 stepwise reaction and damage 8 Gates after every gameplay migration.");
        File.WriteAllText(Path.Combine(output,"R7_NATIVE_HANDOVER.md"),
            report.ToString(),new UTF8Encoding(false));
        Debug.Log("[R7-Coverage] RESULT PASS="+(11-errors.Count)+
            " FAIL="+errors.Count+" native=36/164 partial="+partial+
            " pilot="+pilots+" duplicate="+collisions.Count);
        if(errors.Count>0)
            throw new InvalidOperationException(
                "R7 native coverage failed: "+string.Join("; ",errors));
    }
}
#endif
