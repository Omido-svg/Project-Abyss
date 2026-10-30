#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// R8 bulk dry-run for R7 exclusive effect route: no asset changes.
/// Backs up source .asset/.meta for ALL eligible effectful SkillDefinitions.
/// Creates per-entry manifest for future explicit, batched opt-in.
/// </summary>
public static class R8EffectfulBatchPreflight20261030
{
    private static string Hash(string file)
    {
        using(var alg=SHA256.Create())
        using(var stream=File.OpenRead(file))
            return BitConverter.ToString(alg.ComputeHash(stream))
                .Replace("-","").ToLowerInvariant();
    }
    private static string Q(string x) => "\"" + (x??"").Replace("\"","\"\"") + "\"";

    [MenuItem("Game System Verification/R8 - 68 Effectful Batch Backup Gate (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)
            throw new InvalidOperationException("Edit Mode required.");
        string root=Directory.GetParent(Application.dataPath).FullName;
        string target=Path.Combine(root,"ProjectAbyss_R8_EffectfulBackups_20261030");
        var catalog=AssetDatabase.FindAssets("t:SkillDefinition").Select(g=>
            new R7SkillAssetRef(AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                AssetDatabase.GUIDToAssetPath(g)),g))
            .Where(x=>x.Definition!=null).ToArray();
        var duplicates=catalog.GroupBy(x=>x.Definition.SkillId,StringComparer.Ordinal)
            .Where(x=>x.Count()>1).Select(x=>x.Key)
            .ToHashSet(StringComparer.Ordinal);

        R7SkillAssetRef[] eligible;
        using(var scope=R7ExclusiveEntryMigrationScope.Begin(catalog))
            eligible=catalog.Where(x=>scope.CanRoute(x.Definition))
                .OrderBy(x=>AssetDatabase.GetAssetPath(x.Definition),
                    StringComparer.Ordinal).ToArray();

        var entries=new List<(string Rel,string Source,string Backup)>();
        foreach(var item in eligible)
        {
            var path=AssetDatabase.GetAssetPath(item.Definition);
            if(!path.StartsWith("Assets/",StringComparison.Ordinal) ||
               path.Contains("..") ||
               duplicates.Contains(item.Definition.SkillId) ||
               AssetDatabase.AssetPathToGUID(path)!=item.AssetGuid)
                throw new InvalidOperationException("Unsafe source path or GUID: "+path);
            foreach(var name in new[]{path,path+".meta"})
            {
                var source=Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar));
                var backup=Path.Combine(target,"ORIGINALS",
                    name.Replace('/',Path.DirectorySeparatorChar));
                if(!File.Exists(source))
                    throw new FileNotFoundException("Preflight source missing",source);
                if(File.Exists(backup)&&Hash(backup)!=Hash(source))
                    throw new InvalidOperationException(
                        "Preexisting immutable snapshot differs: "+name);
                entries.Add((name,source,backup));
            }
        }

        // All candidates are validated before first write.
        foreach(var e in entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(e.Backup));
            if(!File.Exists(e.Backup))
                File.Copy(e.Source,e.Backup,overwrite:false);
            if(Hash(e.Source)!=Hash(e.Backup))
                throw new InvalidOperationException("SHA mismatch: "+e.Rel);
        }

        var manifest=new StringBuilder();
        manifest.AppendLine("SkillId,AssetGuid,AssetPath,ActionType,EntryIndex,EffectType,Timing,AssetSHA256,MetaSHA256");
        int count=0;
        foreach(var item in eligible)
        {
            var data=item.Definition;
            var path=AssetDatabase.GetAssetPath(data);
            var disk=Path.Combine(root,path.Replace('/',Path.DirectorySeparatorChar));
            for(int i=0;i<data.EffectEntries.Count;i++)
            {
                var entry=data.EffectEntries[i];
                manifest.AppendLine(string.Join(",",new[]{
                    data.SkillId,item.AssetGuid,path,data.ActionType.ToString(),
                    i.ToString(),entry.Definition.GetType().Name,
                    entry.EffectiveTiming.ToString(),Hash(disk),Hash(disk+".meta")
                }.Select(Q)));
                count++;
            }
        }
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target,"EFFECT_ENTRY_MANIFEST.csv"),
            manifest.ToString(),new UTF8Encoding(false));
        var instructions=new StringBuilder();
        instructions.AppendLine("# R8 효과 카드 배치 전환 원본 보호 (2026-10-30)");
        instructions.AppendLine("기본 게임 실행 경로는 레거시. 이 폴더는 명시적 Opt-in 이관 전 원본 바이트 보존만 수행합니다.");
        instructions.AppendLine("검사 대상: SkillDefinition "+catalog.Length+" / 중복 ID 종류 "+duplicates.Count);
        instructions.AppendLine("배치 전환 조건 통과: SkillDefinition "+eligible.Length+
            " / 루트 EffectEntries "+count);
        instructions.AppendLine("저장된 .asset + .meta 원본 복사본 "+entries.Count+"개");
        instructions.AppendLine("원본을 실제 변경하거나 신규 Command에 전문 효과를 완전 포팅한 것이 아닙니다.");
        instructions.AppendLine("R7ExclusiveEntryMigrationScope가 명시적으로 켜진 경우에만 AbilityRunner.Plan→LegacySkillEffectEntryAdapter로 전환됩니다.");
        instructions.AppendLine("어떤 이관에서든 두 실행 경로를 동시 구독시키면 안 됩니다.");
        File.WriteAllText(Path.Combine(target,"README_KO.md"),
            instructions.ToString(),new UTF8Encoding(false));

        int pass=0,fail=0;
        void Check(string id,bool yes)
        {
            if(yes){pass++;Debug.Log("[R8-Batch] PASS "+id);}
            else{fail++;Debug.LogError("[R8-Batch] FAIL "+id);}
        }
        Check("B01 catalog 145",catalog.Length==145);
        Check("B02 effectful safe cohort verified",eligible.Length==68);
        Check("B03 root effect entries verified",count==85);
        Check("B04 every eligible asset has backed-up .meta",
            entries.Count==eligible.Length*2);
        Check("B05 backup bytes equal original SHA",
            entries.All(x=>File.Exists(x.Backup) &&
                Hash(x.Source)==Hash(x.Backup)));
        Check("B06 no duplicate IDs selected",
            eligible.All(x=>!duplicates.Contains(x.Definition.SkillId)));
        Check("B07 no source GUID changed",
            eligible.All(x=>AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(x.Definition))==x.AssetGuid));
        Check("B08 effect manifest includes every entry",
            File.ReadAllLines(Path.Combine(target,"EFFECT_ENTRY_MANIFEST.csv")).Length==count+1);
        Check("B09 all selected assets remain clean",
            eligible.All(x=>!EditorUtility.IsDirty(x.Definition)));
        Check("B10 no opt-in runtime scope leaked",
            R7ExclusiveEntryMigrationScope.Active==null);
        Debug.Log("[R8-Batch] SUMMARY eligible="+eligible.Length+
            " effects="+count+" originals="+entries.Count+
            " directory="+target);
        Debug.Log("[R8-Batch] RESULT PASS="+pass+" FAIL="+fail);
        if(fail!=0)throw new InvalidOperationException("R8 batch audit failed");
    }
}
#endif