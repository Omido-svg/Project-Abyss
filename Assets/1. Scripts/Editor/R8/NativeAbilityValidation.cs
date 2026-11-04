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
/// R8 all-content validator: typed SO/GUID/ID/schema/upgrade coverage. A
/// truthful report may contain content blockers without failing the scanner;
/// corrupt native schemas or untracked duplicate native routes fail the gate.
/// </summary>
public static class NativeAbilityValidation
{
    public enum Severity { Info, Warning, Blocker }
    public sealed class Issue
    {
        public Severity Level;
        public string Code,Path,Details;
    }
    public sealed class Result
    {
        public readonly List<Issue> Issues=new List<Issue>();
        public int SkillCount,NativeCount,AugmentCount,ItemCount;
        public int NativeCorruptionCount;
        public int BlockerCount=>Issues.Count(x=>x.Level==Severity.Blocker);
        public int WarningCount=>Issues.Count(x=>x.Level==Severity.Warning);
        public int DuplicateSourceIdGroups;
        public int NativeGoodCount;
        public int AutoConvertibleCount;
        public int NativePendingCount;
        public int PendingAugments;
        public int TemporaryItems;
        public void Add(Severity level,string code,string path,string detail)=>
            Issues.Add(new Issue{Level=level,Code=code,Path=path??"",Details=detail??""});
    }

    public static Result Scan()
    {
        var result=new Result();
        var sources=NativeAbilityAuthoringCore.Sources();
        result.SkillCount=sources.Count;
        var sourceByGuid=sources.ToDictionary(
            s=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(s)),
            s=>s,StringComparer.OrdinalIgnoreCase);
        var duplicated=sources.GroupBy(x=>x.SkillId,StringComparer.Ordinal)
            .Where(g=>g.Count()>1).ToArray();
        result.DuplicateSourceIdGroups=duplicated.Length;
        foreach(var g in duplicated)
        {
            bool approved=SkillIdentityVerification.IsApprovedGroup(g);
            foreach(var s in g)
                result.Add(approved?Severity.Info:Severity.Blocker,
                    approved?"R7_2_GUID_CONTRACT_RESOLVED":"DUPLICATE_SKILL_ID",
                    AssetDatabase.GetAssetPath(s),
                    g.Key+(approved?"; GUID-qualified save contract, gameplay native pending":
                        "; unapproved ambiguous SkillId"));
        }
        var all=NativeAbilityAuthoringCore.AnalyzeAll();
        result.AutoConvertibleCount=all.Count(p=>p.Status==
            NativeAbilityAuthoringCore.Verdict.ReadyToCreate);
        foreach(var p in all.Where(x=>x.Status==
            NativeAbilityAuthoringCore.Verdict.ExistsMismatch))
            result.Add(Severity.Blocker,"SOURCE_NATIVE_MISMATCH",
                p.Destination,p.Reason);
        foreach(var p in all.Where(x=>x.Status==
            NativeAbilityAuthoringCore.Verdict.Blocked))
            if(p.Reason!=null&&p.Reason.Contains("Unsupported"))
                result.Add(Severity.Info,"MANUAL_NATIVE_CONTRACT",
                    p.SourcePath,p.Reason);

        var native=new List<(ScriptableObject Graph,string Id,string Guid,
            string Name,bool Valid,string Family)>();
        void AddNative<T>(string folder,Func<T,string> id,
            Func<T,string> guid,Func<T,string> name,Func<T,bool> valid,
            string family) where T:ScriptableObject
        {
            foreach(var d in Resources.LoadAll<T>(folder))
                native.Add((d,id(d),guid(d),name(d),valid(d),family));
        }
        AddNative<R7NativeAbilityDefinition>("ProjectAbyss/NativeAbilities",
            x=>x.StableId,x=>x.SourceAssetGuid,x=>x.SourceAssetName,x=>x.IsValid,"Resource");
        AddNative<R7NativeStatusAbilityDefinition>("ProjectAbyss/NativeStatuses",
            x=>x.StableId,x=>x.SourceAssetGuid,x=>x.SourceAssetName,x=>x.IsValid,"Status");
        AddNative<R7NativeMixedAbilityDefinition>("ProjectAbyss/NativeMixed",
            x=>x.StableId,x=>x.SourceAssetGuid,x=>x.SourceAssetName,x=>x.IsValid,"Mixed");
        AddNative<R7NativeUniqueGaugeDefinition>("ProjectAbyss/NativeUniqueGauges",
            x=>x.StableId,x=>x.SourceAssetGuid,x=>x.SourceAssetName,x=>x.IsValid,"UniqueGauge");
        AddNative<NativePreparationSetupDefinition>("ProjectAbyss/NativePreparations",
            x=>x.StableId,x=>x.SourceAssetGuid,x=>x.SourceAssetName,x=>x.IsValid,"Preparation");
        result.NativeCount=native.Count;
        foreach(var x in native)
        {
            string path=AssetDatabase.GetAssetPath(x.Graph);
            SkillDefinition source=null;
            bool identity=!string.IsNullOrWhiteSpace(x.Guid)&&
                sourceByGuid.TryGetValue(x.Guid,out source) &&
                source.SkillId==x.Id&&source.name==x.Name &&
                string.Equals(AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(source)),x.Guid,
                    StringComparison.OrdinalIgnoreCase);
            if(!x.Valid||!identity)
            {
                result.NativeCorruptionCount++;
                result.Add(Severity.Blocker,"INVALID_NATIVE_IDENTITY_OR_SCHEMA",
                    path,x.Family+" "+x.Id+" valid="+x.Valid+" identity="+identity);
                continue;
            }
            if(x.Graph is R7NativeAbilityDefinition ||
               x.Graph is R7NativeStatusAbilityDefinition ||
               x.Graph is R7NativeUniqueGaugeDefinition)
            {
                var p=NativeAbilityAuthoringCore.Analyze(
                    source,sources);
                if(p.Status!=NativeAbilityAuthoringCore.Verdict.ExistsValid||
                   AssetDatabase.GetAssetPath(x.Graph)!=p.Destination)
                {
                    result.NativeCorruptionCount++;
                    result.Add(Severity.Blocker,"NATIVE_AUTHORING_PARITY",
                        path,x.Id+" : "+p.Reason);
                    continue;
                }
            }
            result.NativeGoodCount++;
        }
        foreach(var group in native.GroupBy(x=>x.Guid,
            StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1))
        {
            // Preparation intentionally has one distinct SO to augment an
            // already-native pair of status operations, not a duplicate route.
            bool preparationOnly=group.Count()==2 &&
                group.Any(x=>x.Family=="Preparation") &&
                group.Any(x=>x.Family=="Resource"||
                             x.Family=="Status"||x.Family=="Mixed");
            if(!preparationOnly)
            {
                result.NativeCorruptionCount++;
                result.Add(Severity.Blocker,"DUPLICATE_NATIVE_GUID",
                    string.Join("|",group.Select(x=>AssetDatabase.GetAssetPath(x.Graph))),
                    group.Key);
            }
        }
        foreach(var group in native.Where(x=>x.Family!="Preparation")
            .GroupBy(x=>x.Id,StringComparer.Ordinal).Where(g=>g.Count()>1))
        {
            result.NativeCorruptionCount++;
            result.Add(Severity.Blocker,"DUPLICATE_NATIVE_STABLE_ID",
                string.Join("|",group.Select(x=>AssetDatabase.GetAssetPath(x.Graph))),
                group.Key);
        }
        result.NativePendingCount=result.SkillCount-
            native.Where(x=>x.Family!="Preparation")
                .Select(x=>x.Guid).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        var aug=CanonicalEmotionAugmentInventory.Load();
        var item=AssetDatabase.FindAssets("t:RunItemDefinition")
            .Select(g=>AssetDatabase.LoadAssetAtPath<RunItemDefinition>(
                AssetDatabase.GUIDToAssetPath(g))).Where(x=>x!=null).ToArray();
        
        // Keep unregistered prototypes visible in diagnostics without
        // inflating the 63-entry active combat catalog.
        foreach (var draft in CanonicalEmotionAugmentInventory.UnregisteredDrafts())
            result.Add(Severity.Info, "AUGMENT_UNREGISTERED_DRAFT",
                AssetDatabase.GetAssetPath(draft),
                draft.AugmentId + " is not registered in the active catalog");
        
result.AugmentCount=aug.Length;
        result.ItemCount=item.Length;
        foreach(var a in aug)
            if(a.RuntimeReadiness!=EmotionAugmentRuntimeReadiness.RuntimeConnected)
            {
                result.PendingAugments++;
                result.Add(Severity.Blocker,"AUGMENT_DESIGN_PENDING",
                    AssetDatabase.GetAssetPath(a),a.AugmentId+" "+a.RuntimeNote);
            }
        foreach(var itemData in item)
        {
            if(itemData.CombatItem is ProvisionalBalanceRunItem)
            {
                result.TemporaryItems++;
                result.Add(Severity.Warning,"ITEM_TEMP_IMPLEMENTATION",
                    AssetDatabase.GetAssetPath(itemData),itemData.ItemId);
            }
            else if(itemData.CombatItem==null)
                result.Add(Severity.Blocker,"ITEM_NO_COMBAT_LINK",
                    AssetDatabase.GetAssetPath(itemData),itemData.ItemId);
        }
        return result;
    }

    public static string Report(Result r,string path)
    {
        var sb=new StringBuilder();
        sb.AppendLine("# Project Abyss R8 — Native Authoring Validator");
        sb.AppendLine("Read-only scan. Content blockers are not automatically fixed.");
        sb.AppendLine();
        sb.AppendLine("- SkillDefinition: "+r.SkillCount);
        sb.AppendLine("- Native SO: "+r.NativeGoodCount+"/"+r.NativeCount+
            " valid, corruption="+r.NativeCorruptionCount);
        sb.AppendLine("- Native auto-convertible not created: "+r.AutoConvertibleCount);
        sb.AppendLine("- Full-native coverage not verified: "+r.NativePendingCount);
        sb.AppendLine("- Duplicate source SkillId groups: "+r.DuplicateSourceIdGroups);
        sb.AppendLine("- Augments: "+r.AugmentCount+" / pending="+r.PendingAugments);
        sb.AppendLine("- Run items: "+r.ItemCount+" / temporary="+r.TemporaryItems);
        sb.AppendLine("- Issues: blocker="+r.BlockerCount+
            " warning="+r.WarningCount);
        sb.AppendLine();
        sb.AppendLine("## Issues");
        foreach(var x in r.Issues.OrderByDescending(x=>x.Level)
             .ThenBy(x=>x.Code).ThenBy(x=>x.Path))
            sb.AppendLine("- ["+x.Level+"] "+x.Code+
                " | "+x.Path+" | "+x.Details);
        string text=sb.ToString();
        if(!string.IsNullOrEmpty(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,text,new UTF8Encoding(false));
        }
        return text;
    }
}
#endif