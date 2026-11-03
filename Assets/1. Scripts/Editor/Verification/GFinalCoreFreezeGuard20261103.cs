#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// G-FINAL scoped core source/serialization baseline.
/// This is change detection and review policy, not a file-permission lock.
/// Pending content/character specialist effects are intentionally outside the scope.
/// </summary>
public static class GFinalCoreFreezeGuard20261103
{
    public const string BaselinePath=
        "Assets/1. Scripts/Editor/Verification/GFinalCoreFreezeBaseline_20261103.csv";
    // Pinned at freeze issuance; changing this value requires reviewed rebaseline.
    public const string BaselineSha256="BE0801ED8868DA12784BC77A8F5BEB0B232E91B756375D1E7336657887BD7777";

    private static readonly string Root="Assets/1. Scripts/Runtime/";
    private static readonly string[] LockedDirectories=
    {
        "Abilities/Contracts/",
        "Abilities/Conditions/",
        "Abilities/Reactions/",
        "Abilities/Targeting/",
        "Battle/Damage/",
        "Battle/Targeting/",
        "Systems/Resolution/"
    };

    private static readonly string[] LockedFiles=
    {
        "Abilities/Adapters/LegacySkillEffectEntryAdapter.cs",
        "Abilities/Runtime/AbilityRunner.cs",
        "Abilities/Runtime/AbilityRuntimeState.cs",
        "Abilities/Runtime/R6AbilityLifetimeMemory.cs",
        "Battle/BattleAction.cs",
        "Battle/BattleRuntimeServices.cs",
        "Characters/CharacterBase/CharacterEventBinder.cs",
        "Skills/Base/Skill.cs",
        "Skills/Base/SkillEffectDispatcher.cs",
        "Skills/DataDriven/SkillDefinition.cs",
        "Skills/DataDriven/SkillEffectContext.cs",
        "Skills/Effects/SkillEffectCondition.cs",
        "Skills/Effects/SkillEffectDefinition.cs",
        "Skills/Effects/SkillEffectEntry.cs",
        "Skills/Effects/SkillEffectResult.cs",
        "Skills/Effects/SkillEffectTargetSelector.cs",
        "Skills/Effects/SkillEffectTiming.cs",
        "Systems/BattleFlow/BattleManager.cs",
        "Systems/BattleFlow/BattleRuntimeFactory.cs"
    };

    public static bool IsProtectedPath(string path)
    {
        if(string.IsNullOrEmpty(path))return false;
        path=path.Replace('\\','/');
        if(path.EndsWith(".meta",StringComparison.OrdinalIgnoreCase))
            path=path.Substring(0,path.Length-5);
        if(!path.StartsWith(Root,StringComparison.Ordinal))
            return false;
        var rel=path.Substring(Root.Length);
        return rel.EndsWith(".cs",StringComparison.OrdinalIgnoreCase) &&
            (LockedDirectories.Any(dir=>rel.StartsWith(dir,StringComparison.Ordinal)) ||
             LockedFiles.Contains(rel,StringComparer.Ordinal) ||
             rel.StartsWith("Abilities/Operations/R5",StringComparison.Ordinal));
    }

    public static string[] ProtectedPaths()
    {
        var all=AssetDatabase.FindAssets("t:MonoScript",new[]{"Assets/1. Scripts/Runtime"})
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsProtectedPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x=>x,StringComparer.Ordinal)
            .ToArray();
        return all;
    }

    private static string Sha(byte[] value)
    {
        using(var h=SHA256.Create())
            return BitConverter.ToString(h.ComputeHash(value)).Replace("-","");
    }
    private static string ShaFile(string path)=>
        Sha(File.ReadAllBytes(path));

    private sealed class Row
    {
        public string Path, Guid, SourceSha, MetaSha;
    }

    [MenuItem("Game System Verification/G-FINAL - Verify Frozen Core Baseline (Edit Only)")]
    public static void Verify()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Core freeze verification requires idle EditMode.");

        var root=Directory.GetParent(Application.dataPath).FullName;
        var manifestFile=Path.Combine(root,BaselinePath);
        var errors=new List<string>();
        if(!File.Exists(manifestFile))
            throw new FileNotFoundException("G-FINAL immutable source manifest missing",manifestFile);
        string hash=ShaFile(manifestFile);
        if(!string.Equals(hash,BaselineSha256,StringComparison.OrdinalIgnoreCase))
            errors.Add("BASELINE_MANIFEST_CHANGED expected="+BaselineSha256+" current="+hash);

        var rows=new Dictionary<string,Row>(StringComparer.Ordinal);
        var input=File.ReadAllLines(manifestFile);
        if(input.Length<2||input[0]!="Path,GUID,SourceSHA256,MetaSHA256")
            errors.Add("MANIFEST_HEADER_INVALID");
        foreach(var line in input.Skip(1))
        {
            var values=line.Split(',');
            if(values.Length!=4||!rows.TryAdd(values[0],new Row {
                Path=values[0],Guid=values[1],
                SourceSha=values[2],MetaSha=values[3]}))
                errors.Add("MANIFEST_ROW_INVALID "+line);
        }

        var current=ProtectedPaths();
        foreach(var p in current.Where(p=>!rows.ContainsKey(p)))
            errors.Add("UNBASELINED_CORE_SCRIPT "+p);
        foreach(var p in rows.Keys.Where(p=>!current.Contains(p,StringComparer.Ordinal)))
            errors.Add("BASELINED_CORE_SCRIPT_MISSING "+p);
        foreach(var item in rows.Values)
        {
            if(!IsProtectedPath(item.Path))
            {
                errors.Add("MANIFEST_NON_CORE_PATH "+item.Path);
                continue;
            }
            var f=Path.Combine(root,item.Path);
            if(!File.Exists(f)||!File.Exists(f+".meta"))
            {
                errors.Add("SOURCE_OR_META_MISSING "+item.Path);
                continue;
            }
            var g=AssetDatabase.AssetPathToGUID(item.Path);
            if(!string.Equals(g,item.Guid,StringComparison.OrdinalIgnoreCase))
                errors.Add("GUID_CHANGED "+item.Path+" old="+item.Guid+" now="+g);
            if(!string.Equals(ShaFile(f),item.SourceSha,StringComparison.OrdinalIgnoreCase))
                errors.Add("SOURCE_CHANGED "+item.Path);
            if(!string.Equals(ShaFile(f+".meta"),item.MetaSha,StringComparison.OrdinalIgnoreCase))
                errors.Add("META_CHANGED "+item.Path);
        }

        var r9=Path.Combine(root,"ProjectAbyss_R9_Integration_20261102","R9_GATE_RESULTS.csv");
        if(!File.Exists(r9))errors.Add("R9_EVIDENCE_MISSING");
        else
        {
            var gates=File.ReadAllLines(r9).Skip(1).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
            if(gates.Length!=69 || gates.Any(x=>!x.Contains(",\"PASS\",")))
                errors.Add("R9_LATEST_REGRESSION_NOT_GREEN");
        }

        var outDir=Path.Combine(root,"ProjectAbyss_GFinal_20261103");
        Directory.CreateDirectory(outDir);
        var report=new StringBuilder()
            .AppendLine("# G-FINAL scoped core structure freeze verification")
            .AppendLine("- Core baseline SHA256: "+BaselineSha256)
            .AppendLine("- Protected source scripts: "+rows.Count)
            .AppendLine("- Current protected source scripts: "+current.Length)
            .AppendLine("- Scope: shared ability contract, condition, targeting, reaction, command, clash, damage and serialization boundaries")
            .AppendLine("- Explicit exclusions: authoring/content SO values, unique character specialist handlers, item/emotion balancing and future approved local extensions")
            .AppendLine("- This policy does not claim v4 roadmap full G-FINAL acceptance; canonical content and extension/lifecycle tests remain pending.")
            .AppendLine("- Result: "+(errors.Count==0?"CORE_STRUCTURE_BASELINE_INTACT":"CORE_STRUCTURE_DRIFT_DETECTED"))
            .AppendLine("- Errors: "+errors.Count);
        foreach(var e in errors)report.AppendLine("- "+e);
        File.WriteAllText(Path.Combine(outDir,"GFINAL_CORE_FREEZE_CHECK.md"),report.ToString(),new UTF8Encoding(false));

        if(errors.Count==0)Debug.Log("[G-FINAL CORE FREEZE] RESULT PASS="+
            (rows.Count+3)+" FAIL=0 LOCKED_SCRIPTS="+rows.Count+
            " STATUS=CORE_STRUCTURE_BASELINE_INTACT; overall G-FINAL still conditional");
        else
        {
            foreach(var error in errors)Debug.LogError("[G-FINAL CORE FREEZE] "+error);
            Debug.LogError("[G-FINAL CORE FREEZE] RESULT PASS=0 FAIL="+errors.Count+
                " STATUS=CORE_STRUCTURE_DRIFT_DETECTED");
            throw new InvalidOperationException("Frozen core source drift detected: "+errors.Count+
                " issue(s); see GFINAL_CORE_FREEZE_CHECK.md");
        }
    }
}

/// <summary>Warn at import time. Validation is the authoritative full comparison.</summary>
public sealed class GFinalCoreFreezeChangeNotice20261103: AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported,
        string[] deleted,string[] moved,string[] movedFrom)
    {
        var paths=imported.Concat(deleted).Concat(moved).Concat(movedFrom)
            .Where(GFinalCoreFreezeGuard20261103.IsProtectedPath)
            .Distinct(StringComparer.Ordinal)
            .Take(12).ToArray();
        if(paths.Length==0)return;
        Debug.LogWarning("[G-FINAL CORE FREEZE] Protected core source was imported, "+
            "moved, or deleted. Run Game System Verification/G-FINAL - "+
            "Verify Frozen Core Baseline (Edit Only). Paths: "+string.Join(" | ",paths));
    }
}
#endif