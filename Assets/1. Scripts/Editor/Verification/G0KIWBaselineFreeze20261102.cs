#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>G0 K/I/W read-only audit: never mutates live combat or authored assets.</summary>
public static class G0KIWBaselineFreeze20261102
{
    [Serializable] private sealed class BaseAudit
    {
        public int SkillAssetCount,EntryCount,EmotionAugmentAssetCount;
        public int MissingScriptAssetCount,DuplicateGuidGroupCount;
        public int RequiredKeywordMissingCount,RequiredKeywordUndecidedCount;
        public int NeedsManualReviewCount,AttackWeightedSkillCount;
    }
    [Serializable] private sealed class Golden {public List<GoldenCase> Scenarios;}
    [Serializable] private sealed class GoldenCase {public string Id; public List<string> Trace;}
    [Serializable] private sealed class Report
    {
        public string GeneratedAtUtc,Branch,Head,ActiveScene,GoldenHash,AuditHash;
        public bool SceneDirty;
        public int SceneMissingScripts,SkillAssets,EffectEntries,EmotionAugments,GoldenCount;
        public int SnapshotAssets,EvidenceLines,KeywordMissing,PolicyUndecided,ManualReviewEffects,AuthoredWeightedSkills;
        public int Pass,Fail;
        public string[] Failures,PendingForR1ToR6;
    }
    private const string A="Assets/1. Scripts/Runtime/";
    private static readonly (string family,string file,string token,string explanation)[] Evidence =
    {
        ("I-DOMAIN",A+"Systems/Resolution/ActionResolver.cs","ResolveClashQueue","Main clash coordinator"),
        ("I-DOMAIN",A+"Systems/Resolution/ActionResolver.cs","ResolvePair(","Current synchronous execution"),
        ("I-UI",A+"Systems/Resolution/ActionResolver.cs","PlayClash(","Presentation invoked after resolution"),
        ("I-DOMAIN",A+"Systems/Resolution/ClashManager.cs","RaiseExchangeResolved","Domain exchange event"),
        ("I-DOMAIN",A+"Systems/Resolution/ClashManager.cs","RaiseClashResolved","Domain final clash event"),
        ("I-COUNTER",A+"Characters/Hifumi/Mechanics/HifumiMechanic.cs","ResolveCounterSequence","Existing counter roll, not separate triggered skill"),
        ("I-COUNTER",A+"Characters/Hifumi/Mechanics/HifumiMechanic.cs","OnExchangeResolved","Existing counter listener"),
        ("I-STEP",A+"Systems/Resolution/ClashManager.R6Stepwise.cs","StepwiseSession","Opt-in stepwise session"),
        ("I-STEP",A+"Abilities/Reactions/BattleReactionScheduler.cs","TryDequeue(","Reaction intent queue"),
        ("I-UI",A+"Presentation/UI/Core/BattleUIEventBinder.cs","OnDamageEventResolved","UI observes settled result"),
        ("W-DOMAIN",A+"Battle/Targeting/AttackWeightTargetResolver.cs","SlotWeighted","Opt-in selection route"),
        ("W-DOMAIN",A+"Abilities/Targeting/R5SlotWeightedTargetBridge.cs","Resolve(","Weight bridge"),
        ("W-RESULT",A+"Systems/Resolution/ClashManager.cs","SecondaryDamageContexts","Settled secondary damage"),
        ("W-RESULT",A+"Battle/BattleAction.cs","AttackWeightHitResults","Damage result target grouping"),
        ("W-LEGACY",A+"Battle/Targeting/AttackWeightTargetResolver.cs","SecondaryTargetPart","Protected legacy part selection")
    };

    public static void Run()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Run in Edit Mode only.");
        string project=Directory.GetParent(Application.dataPath).FullName;
        string old=Path.Combine(project,"G0_Ability_Audit_20261029");
        string output=Path.Combine(project,"G0_KIW_Baseline_20261102");
        Directory.CreateDirectory(output);
        var failures=new List<string>();
        int pass=0;
        void Check(string id,bool ok,string details)
        {
            if(ok){pass++;Debug.Log("[G0-KIW] PASS "+id+" "+details);}
            else{failures.Add(id+" "+details);Debug.LogError("[G0-KIW] FAIL "+id+" "+details);}
        }
        // Re-runs the existing full asset audit; no migration or asset edits.
        G0AbilityAuditReport20261029.RunAuditFromMenu();
        string auditFile=Path.Combine(old,"G0_AUDIT.json");
        string goldenFile=Path.Combine(old,"G0_GOLDEN_BASELINE.json");
        Check("A01 audited source",File.Exists(auditFile),auditFile);
        Check("A02 captured PlayMode Golden",File.Exists(goldenFile),goldenFile);
        var audit=File.Exists(auditFile)?JsonUtility.FromJson<BaseAudit>(File.ReadAllText(auditFile)):new BaseAudit();
        var golden=File.Exists(goldenFile)?JsonUtility.FromJson<Golden>(File.ReadAllText(goldenFile)):new Golden();
        int goldenCount=golden.Scenarios?.Count??0;
        Check("A03 Golden scenarios",goldenCount>=10,"count="+goldenCount);
        Check("A04 fatal missing scripts",audit.MissingScriptAssetCount==0,"count="+audit.MissingScriptAssetCount);
        Check("A05 duplicate GUID",audit.DuplicateGuidGroupCount==0,"count="+audit.DuplicateGuidGroupCount);
        Check("A06 scanned skills and effects",audit.SkillAssetCount>0&&audit.EntryCount>0,"skills="+audit.SkillAssetCount+" entries="+audit.EntryCount);
        Check("A07 emotion augment scan",audit.EmotionAugmentAssetCount>0,"count="+audit.EmotionAugmentAssetCount);

        Scene scene=SceneManager.GetActiveScene();
        int missing=0;
        foreach(GameObject root in scene.GetRootGameObjects())
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        Check("A08 loaded scene references",missing==0,"missing="+missing+" dirty="+scene.isDirty);

        var assets=new SortedSet<string>(StringComparer.Ordinal);
        foreach(string filter in new[]{"t:SkillDefinition","t:SkillEffectDefinition","t:EmotionAugmentDefinition"})
            foreach(string guid in AssetDatabase.FindAssets(filter))
            {
                string p=AssetDatabase.GUIDToAssetPath(guid);
                if(!string.IsNullOrEmpty(p))assets.Add(p);
            }
        const string nativeRoot="Assets/2. Data/Resources/ProjectAbyss";
        if(AssetDatabase.IsValidFolder(nativeRoot))
            foreach(string guid in AssetDatabase.FindAssets("t:ScriptableObject",new[]{nativeRoot}))
            {
                string p=AssetDatabase.GUIDToAssetPath(guid);
                if(!string.IsNullOrEmpty(p))assets.Add(p);
            }
        var hashes=new StringBuilder("AssetPath,GUID,AssetSHA256,MetaSHA256,Type\n");
        int assetErrors=0;
        foreach(string p in assets)
        {
            string full=Path.Combine(project,p.Replace('/',Path.DirectorySeparatorChar));
            string guid=AssetDatabase.AssetPathToGUID(p);
            if(!File.Exists(full)||!File.Exists(full+".meta")||string.IsNullOrEmpty(guid))assetErrors++;
            hashes.AppendLine(Csv(p)+","+Csv(guid)+","+Csv(Hash(full))+","+Csv(Hash(full+".meta"))+
                ","+Csv(AssetDatabase.GetMainAssetTypeAtPath(p)?.Name??"Unknown"));
        }
        File.WriteAllText(Path.Combine(output,"G0_ASSET_HASH_MANIFEST.csv"),hashes.ToString(),new UTF8Encoding(false));
        Check("A09 asset/meta GUID and hash manifest",assetErrors==0&&assets.Count>0,
            "assets="+assets.Count+" errors="+assetErrors);

        var evidence=new StringBuilder("Family,SourcePath,Line,Token,Meaning,SourceText\n");
        int count=0;
        var absent=new List<string>();
        foreach(var item in Evidence)
        {
            string file=Path.Combine(project,item.file.Replace('/',Path.DirectorySeparatorChar));
            string[] lines=File.Exists(file)?File.ReadAllLines(file):Array.Empty<string>();
            int found=0;
            for(int i=0;i<lines.Length;i++)
            {
                if(!lines[i].Contains(item.token))continue;
                evidence.AppendLine(Csv(item.family)+","+Csv(item.file)+","+(i+1)+","+
                    Csv(item.token)+","+Csv(item.explanation)+","+Csv(lines[i].Trim()));
                count++;found++;
            }
            if(found==0)absent.Add(item.family+" "+item.token+" @ "+item.file);
        }
        File.WriteAllText(Path.Combine(output,"G0_I_W_SOURCE_EVIDENCE.csv"),evidence.ToString(),new UTF8Encoding(false));
        Check("A10 execution/presentation/target evidence",absent.Count==0,
            "lines="+count+" missing="+string.Join("|",absent));

        var kinds=new StringBuilder("Kind,Meaning,IndependentAction\n");
        foreach(var x in new[]{
            ("HifumiCounterRoll","Original Hifumi counter roll","false"),
            ("EnemyCounterRoll","Enemy counter roll","false"),
            ("SecondaryHit","Attack weight secondary hit","false"),
            ("FollowUpRoll","Roll within current action","false"),
            ("TriggeredSkillAction","New action with independent ID","true")
        })kinds.AppendLine(Csv(x.Item1)+","+Csv(x.Item2)+","+Csv(x.Item3));
        File.WriteAllText(Path.Combine(output,"G0_I_REACTION_BOUNDARIES.csv"),kinds.ToString(),new UTF8Encoding(false));

        string pending=
            "# G0 K/I/W risk and policy ledger (2026-11-02)\n\n"+
            "## K: character unique keywords\n"+
            "- Required keyword missing: "+audit.RequiredKeywordMissingCount+
            "; policy undecided: "+audit.RequiredKeywordUndecidedCount+
            "; see original G0_K_REQUIRED.csv. The scan uses name/path heuristics and is NOT proof of runtime implementation.\n"+
            "- Olaf Bleed: planned scope is per Part, but current G0 required list may say Actor. Reconcile storage and actual damage target before R2-K.\n"+
            "- Hifumi BloodPrice: distinguish from Olaf skill identifiers with similar names. Ownership requires explicit validation.\n"+
            "- Unresolved: Madness extra incoming damage channel; Mark detonation weapon snapshot; Stealth vs Taunt targeting conflict. No invented policies.\n"+
            "- Counter is a SkillTag, not a N/T status. Part bleed/mark and per-slot taunt must not be treated as universal actor status.\n\n"+
            "## I: reaction and playback\n"+
            "- The original ActionResolver runs ResolvePair then PlayClash. The StepwiseSession exists separately and is not presumed production-default.\n"+
            "- HifumiCounterRoll, EnemyCounterRoll, SecondaryHit, FollowUpRoll, TriggeredSkillAction are different concepts. Preserve behavior.\n"+
            "- The current Golden captures outcome and trace; this is not a complete R6-I mid-exchange interruption Golden.\n\n"+
            "## W: weight allocation\n"+
            "- Authored SkillDefinitions whose weight exceeds 1: "+audit.AttackWeightedSkillCount+
            ". Synthetic legacy W01-W03 PlayMode fixture does not prove authored weighted content.\n"+
            "- Preserve SecondaryTargetPart, AnotherPartOnPrimaryCharacter, secondary damage, UI and Undo semantics before enabling SlotWeighted mode.\n"+
            "- R4-W/R5-W/R6-W later own preview/commit/reaction, actual target damage event count and stable slot allocation tests.\n";
        File.WriteAllText(Path.Combine(output,"G0_KIW_POLICY_LEDGER.md"),pending,new UTF8Encoding(false));

        var report=new Report{
            GeneratedAtUtc=DateTime.UtcNow.ToString("o"),
            Branch=GetBranch(project),Head=GetHead(project),ActiveScene=scene.path,
            SceneDirty=scene.isDirty,SceneMissingScripts=missing,
            SkillAssets=audit.SkillAssetCount,EffectEntries=audit.EntryCount,EmotionAugments=audit.EmotionAugmentAssetCount,
            GoldenCount=goldenCount,SnapshotAssets=assets.Count,EvidenceLines=count,
            KeywordMissing=audit.RequiredKeywordMissingCount,PolicyUndecided=audit.RequiredKeywordUndecidedCount,
            ManualReviewEffects=audit.NeedsManualReviewCount,AuthoredWeightedSkills=audit.AttackWeightedSkillCount,
            GoldenHash=Hash(goldenFile),AuditHash=Hash(auditFile),
            Pass=pass,Fail=failures.Count,Failures=failures.ToArray(),
            PendingForR1ToR6=new[]{
                "K: verify per-Part Bleed and BloodPrice ownership; do not infer implementations from token names.",
                "I: Stepwise & reaction scheduler runtime integration plus UI chronological replay are R6 work.",
                "W: zero authored weight>1 cards; synthetic fixtures are separate from shipped cards.",
                "Policy decisions and manual-review effect rows are pending in G0 exports."
            }
        };
        File.WriteAllText(Path.Combine(output,"G0_KIW_FREEZE.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
        var summary="# G0 K/I/W baseline snapshot\n\n"+
            "- Time UTC: "+report.GeneratedAtUtc+"\n- Git: "+report.Branch+" @ "+report.Head+
            "\n- Scene: "+scene.path+" (dirty="+scene.isDirty+", missing="+missing+")"+
            "\n- Skills: "+report.SkillAssets+"; effect entries: "+report.EffectEntries+"; emotion augments: "+report.EmotionAugments+
            "\n- Content+meta fingerprints: "+report.SnapshotAssets+"; source evidence lines: "+report.EvidenceLines+
            "\n- PlayMode Golden scenarios: "+report.GoldenCount+" (previously run in live Unity)"+
            "\n- Keyword missing: "+report.KeywordMissing+"; undecided: "+report.PolicyUndecided+
            "; effect manual reviews: "+report.ManualReviewEffects+
            "\n- Authored cards with weight>1: "+report.AuthoredWeightedSkills+
            "\n- G0-KIW audit PASS="+pass+" FAIL="+failures.Count+
            "\n\n## Scope\nNo change to production combat, ScriptableObject values, runtime status, or enum serialization. G0 audit PASS is not R1-R6 feature completion.\n"+
            "\n## Files\n"+
            "- G0_ASSET_HASH_MANIFEST.csv: asset and .meta hash per GUID\n"+
            "- G0_I_W_SOURCE_EVIDENCE.csv: source path and line evidence\n"+
            "- G0_I_REACTION_BOUNDARIES.csv: mutually distinct reaction concepts\n"+
            "- G0_KIW_POLICY_LEDGER.md: unresolved rules, risks and protection list\n"+
            "- G0_KIW_FREEZE.json: all verification counts and hashes\n"+
            "- ../G0_Ability_Audit_20261029/G0_GOLDEN_BASELINE.json: captured PlayMode trace\n";
        File.WriteAllText(Path.Combine(output,"G0_KIW_SUMMARY_KO.md"),summary,new UTF8Encoding(false));
        Debug.Log("[G0-KIW] RESULT PASS="+pass+" FAIL="+failures.Count+
            " golden="+goldenCount+" assets="+assets.Count+" missingKeywords="+report.KeywordMissing+
            " undecided="+report.PolicyUndecided+" evidence="+count);
        Debug.Log("[G0-KIW] OUTPUT "+output);
        if(failures.Count>0)throw new InvalidOperationException(string.Join(" ; ",failures));
    }

    private static string Hash(string file)
    {
        if(!File.Exists(file))return "MISSING";
        using(var sha=SHA256.Create())
        using(var fs=File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-","").ToLowerInvariant();
    }

    private static string Csv(string value)
    {
        value=value??string.Empty;
        return "\"" + value.Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ") + "\"";
    }

    private static string GetBranch(string root)
    {
        try
        {
            string s=File.ReadAllText(Path.Combine(root,".git","HEAD")).Trim();
            return s.StartsWith("ref: refs/heads/")?s.Substring(16):s;
        }
        catch{return "UNKNOWN";}
    }

    private static string GetHead(string root)
    {
        try
        {
            string d=Path.Combine(root,".git");
            string head=File.ReadAllText(Path.Combine(d,"HEAD")).Trim();
            if(!head.StartsWith("ref: "))return head;
            string refName=head.Substring(5);
            string refPath=Path.Combine(d,refName.Replace('/',Path.DirectorySeparatorChar));
            if(File.Exists(refPath))return File.ReadAllText(refPath).Trim();
            foreach(string s in File.ReadAllLines(Path.Combine(d,"packed-refs")))
                if(s.EndsWith(" "+refName))return s.Split(' ')[0];
        }
        catch{}
        return "UNKNOWN";
    }
}
#endif
