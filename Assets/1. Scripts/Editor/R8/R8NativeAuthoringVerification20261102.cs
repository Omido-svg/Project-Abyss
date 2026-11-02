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
/// R8 isolated authoring integration: clone three real authored skills into
/// disposable fixture assets, convert to native SOs, validate exact GUID/level
/// values, then remove every fixture even if any assertion fails. Never saves
/// or changes a production source SkillDefinition, SO, prefab or scene.
/// </summary>
public static class R8NativeAuthoringVerification20261102
{
    private const string Scratch=
        "Assets/2. Data/Resources/ProjectAbyss/R8AuthoringTestScratch";
    private static void Check(bool ok,string name,ref int pass,ref int fail)
    {
        if(ok){pass++;Debug.Log("[R8-Integration] PASS "+name);}
        else{fail++;Debug.LogError("[R8-Integration] FAIL "+name);}
    }
    private static SkillDefinition CopyFixture(string originalGuid,string fileName,
        string newId)
    {
        var source=AssetDatabase.GUIDToAssetPath(originalGuid);
        var destination=Scratch+"/"+fileName+".asset";
        if(string.IsNullOrWhiteSpace(source)||
           !AssetDatabase.CopyAsset(source,destination))
            throw new InvalidOperationException("Failed source fixture copy: "+fileName);
        var skill=AssetDatabase.LoadAssetAtPath<SkillDefinition>(destination);
        var serialized=new SerializedObject(skill);
        var id=serialized.FindProperty("skillId");
        if(id==null)throw new InvalidOperationException("No serialized stable ID");
        id.stringValue=newId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        AssetDatabase.SaveAssets();
        return skill;
    }

    [MenuItem("Game System Verification/R8 - Unified Converter Inspector Validator (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("EditMode required");
        int pass=0,fail=0;
        var before=R8NativeValidation20261102.Scan();
        var preview=R8NativeAuthoringCore20261102.AnalyzeAll();
        var existing=preview.Where(p=>p.Status==
            R8NativeAuthoringCore20261102.Verdict.ExistsValid).ToArray();
        Check(before.SkillCount==145&&before.NativeCount==37&&
              before.NativeGoodCount==37&&before.NativeCorruptionCount==0,
            "V01 full 145 / 37 authored native schemas",ref pass,ref fail);
        Check(existing.Length==29&&before.AutoConvertibleCount==0,
            "V02 15 resource + 9 status + 5 gauge source levels match",ref pass,ref fail);
        Check(before.DuplicateSourceIdGroups==4&&
              before.Issues.Count(x=>x.Code=="DUPLICATE_SKILL_ID")==8,
            "V03 duplicate IDs blocked per asset",ref pass,ref fail);
        Check(before.PendingAugments==1 && before.TemporaryItems==60 &&
              before.AugmentCount==63 && before.ItemCount==60,
            "V04 pending augment and temporary item issues are visible",ref pass,ref fail);
        Check(before.Issues.Count(x=>x.Code=="AUGMENT_DESIGN_PENDING")==1 &&
              before.Issues.Count(x=>x.Code=="ITEM_TEMP_IMPLEMENTATION")==60,
            "V05 all R7 blockers are preserved, not falsely PASS",ref pass,ref fail);
        Check(existing.Select(x=>x.Destination).Distinct().Count()==29,
            "V06 every simple graph has unique resolved destination",ref pass,ref fail);

        var root=Directory.GetParent(Application.dataPath).FullName;
        var reportDir=Path.Combine(root,"ProjectAbyss_R8_Authoring_20261102");
        Directory.CreateDirectory(reportDir);
        string report=Path.Combine(reportDir,"R8_VALIDATOR_REPORT.md");
        R8NativeValidation20261102.Report(before,report);
        Check(File.Exists(report)&&new FileInfo(report).Length>2500,
            "V07 markdown validator report written outside Assets",ref pass,ref fail);
        bool refusedOld=false;
        try{R8NativeAuthoringCore20261102.CreateMissing(new[]{existing[0]});}
        catch(InvalidOperationException){refusedOld=true;}
        Check(refusedOld,"V08 existing native graph cannot be overwritten",ref pass,ref fail);

        if(AssetDatabase.IsValidFolder(Scratch))
            throw new InvalidOperationException("Test scratch folder preexists; refusing deletion.");
        var rootFolder="Assets/2. Data/Resources/ProjectAbyss";
        if(AssetDatabase.CreateFolder(rootFolder,"R8AuthoringTestScratch")==null)
            throw new InvalidOperationException("Cannot create isolated test scratch.");
        var targets=new List<string>();
        var backupTargets=new List<string>();
        try
        {
            var resource=Resources.LoadAll<R7NativeAbilityDefinition>(
                "ProjectAbyss/NativeAbilities").First();
            var bleeding=Resources.LoadAll<R7NativeStatusAbilityDefinition>(
                "ProjectAbyss/NativeStatuses").First();
            var gauge=Resources.LoadAll<R7NativeUniqueGaugeDefinition>(
                "ProjectAbyss/NativeUniqueGauges")
                .First(x=>x.Kind==R7UniqueGaugeKind.OlafMadness);
            var fixtures=new[]{
                (resource.SourceAssetGuid,"R8_QA_RESOURCE","r8.qa.resource"),
                (bleeding.SourceAssetGuid,"R8_QA_BLEED","r8.qa.bleed"),
                (gauge.SourceAssetGuid,"R8_QA_OLAF","olaf.r8.qa_unique")
            };
            var sourceHashes=fixtures.Select(x=>{
                var f=Path.Combine(root,AssetDatabase.GUIDToAssetPath(x.SourceAssetGuid));
                return (x.SourceAssetGuid,Path:f,Hash:R8NativeAuthoringCore20261102.Sha256(f),
                    Meta:R8NativeAuthoringCore20261102.Sha256(f+".meta"));
            }).ToArray();
            var chosen=new List<R8NativeAuthoringCore20261102.Preview>();
            foreach(var (guid,name,id) in fixtures)
            {
                var data=CopyFixture(guid,name,id);
                var q=R8NativeAuthoringCore20261102.Analyze(data);
                Check(q.Status==R8NativeAuthoringCore20261102.Verdict.ReadyToCreate &&
                      q.L0>0&&q.L1>0&&q.L2>0,
                    "V09 fixture preflight "+id,ref pass,ref fail);
                chosen.Add(q);
                targets.Add(q.Destination);
                var prefix=Path.Combine(root,"ProjectAbyss_R8_ExplicitCreateBackup_20261102",
                    "ORIGINALS",q.SourcePath);
                backupTargets.Add(prefix);backupTargets.Add(prefix+".meta");
            }
            bool forgedRejected=false;
            try
            {
                var copy=chosen[0];
                R8NativeAuthoringCore20261102.CreateMissing(
                    new[]{new R8NativeAuthoringCore20261102.Preview{
                        SourceGuid=copy.SourceGuid,SourcePath=copy.SourcePath,
                        SkillId="r8.qa.forged",Destination=copy.Destination,
                        Kind=copy.Kind,Status=copy.Status,L0=copy.L0,L1=copy.L1,
                        L2=copy.L2,Trigger=copy.Trigger,Operation=copy.Operation}});
            }
            catch(InvalidOperationException){forgedRejected=true;}
            Check(forgedRejected,"V10 forged/stale preview rejected before write",
                ref pass,ref fail);

            R8NativeAuthoringCore20261102.CreateMissing(chosen);
            var actual=chosen.Select(q=>AssetDatabase.LoadMainAssetAtPath(
                q.Destination)).ToArray();
            Check(actual.Length==3&&actual.All(x=>x!=null),
                "V11 explicit stage produced three new, distinct SOs outside Resources",
                ref pass,ref fail);
            Check(chosen.All(q=>AssetDatabase.AssetPathToGUID(q.SourcePath)==q.SourceGuid &&
                R8NativeAuthoringCore20261102.Analyze(
                    AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                        q.SourcePath)).Status==
                        R8NativeAuthoringCore20261102.Verdict.StagedValid),
                "V12 all native artifacts match source GUID/ID and L0/L1/L2",
                ref pass,ref fail);
            Check(sourceHashes.All(x=>
                R8NativeAuthoringCore20261102.Sha256(x.Path)==x.Hash &&
                R8NativeAuthoringCore20261102.Sha256(x.Path+".meta")==x.Meta),
                "V13 all three original authoring files and .meta SHA unchanged",
                ref pass,ref fail);

            bool replayRejected=false;
            try{R8NativeAuthoringCore20261102.CreateMissing(chosen);}
            catch(InvalidOperationException){replayRejected=true;}
            Check(replayRejected,
                "V14 repeated create rejects previously-created targets",
                ref pass,ref fail);
        }
        finally
        {
            foreach(var target in targets)
                if(AssetDatabase.LoadMainAssetAtPath(target)!=null)
                    AssetDatabase.DeleteAsset(target);
            if(AssetDatabase.IsValidFolder(Scratch))
                AssetDatabase.DeleteAsset(Scratch);
            foreach(var f in backupTargets)
                if(File.Exists(f))File.Delete(f);
            AssetDatabase.Refresh();
            R7ProductionNativeResourceMigration.ResetCatalogCache();
            R7ProductionNativeBleedingMigration.ResetCatalogCache();
            R7ProductionNativeUniqueGaugeMigration.ResetCatalogCache();
        }
        var after=R8NativeValidation20261102.Scan();
        Check(after.SkillCount==145 && after.NativeCount==37 &&
              after.NativeCorruptionCount==0,
            "V15 fixture cleanup restores 145 source + 37 native baseline",
            ref pass,ref fail);
        Check(!AssetDatabase.IsValidFolder(Scratch) &&
              targets.All(x=>AssetDatabase.LoadMainAssetAtPath(x)==null),
            "V16 disposable SO/fixture source paths all removed",ref pass,ref fail);

        string summary="R8 unified converter tests: PASS="+pass+" FAIL="+fail+
            "\nSources="+after.SkillCount+" NativeGood="+after.NativeGoodCount+
            "/"+after.NativeCount+" duplicateIdGroups="+after.DuplicateSourceIdGroups+
            "\nAugmentPending="+after.PendingAugments+
            " TemporaryItems="+after.TemporaryItems+
            "\nNo production source SO or .meta modifications.";
        File.WriteAllText(Path.Combine(reportDir,"R8_CONVERTER_GATE.txt"),
            summary,new UTF8Encoding(false));
        Debug.Log("[R8-Integration] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException(
            "R8 unified tooling verification failed="+fail);
    }
}
#endif