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
/// R8 staged migration preflight. Immutable copies of the EXACT original
/// .asset + .meta are prepared outside Assets, verified by SHA-256.
/// This tool neither performs nor authorizes an actual content migration.
/// </summary>
public static class R8PilotAssetBackupGate20261030
{
    private static string Sha(string file)
    {
        using(var hash=SHA256.Create())
        using(var source=File.OpenRead(file))
            return BitConverter.ToString(hash.ComputeHash(source))
                .Replace("-","").ToLowerInvariant();
    }
    [MenuItem("Game System Verification/R8 - Immutable Pilot Asset Backup (Edit Mode)")]
    public static void Run()
    {
        if(Application.isPlaying)
            throw new InvalidOperationException("Asset backup is forbidden in Play Mode");
        string root=Directory.GetParent(Application.dataPath).FullName;
        string destination=Path.Combine(root,"ProjectAbyss_R8_PilotBackups_20261030");
        var assets=AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g=>new{
                Guid=g,Path=AssetDatabase.GUIDToAssetPath(g),
                Skill=AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                    AssetDatabase.GUIDToAssetPath(g))
            }).Where(x=>x.Skill!=null).ToArray();
        var ambiguous=new HashSet<string>(assets
            .GroupBy(x=>x.Skill.SkillId,StringComparer.Ordinal)
            .Where(x=>x.Count()>1).Select(x=>x.Key),StringComparer.Ordinal);
        var candidates=assets.Where(x=>!ambiguous.Contains(x.Skill.SkillId) &&
            R7SkillPilotObserver.IsMetadataOnlyNormal(x.Skill))
            .OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray();

        // Entire preflight MUST pass before any backup file is created.
        var pairs=new List<(string Relative,string Original,string Backup)>();
        foreach(var item in candidates)
        {
            if(!item.Path.StartsWith("Assets/",StringComparison.Ordinal) ||
               item.Path.Contains(".."))
                throw new InvalidOperationException("Untrusted asset path");
            var actual=Path.Combine(root,item.Path.Replace('/',Path.DirectorySeparatorChar));
            foreach(var rel in new[]{item.Path,item.Path+".meta"})
            {
                var origin=Path.Combine(root,rel.Replace('/',Path.DirectorySeparatorChar));
                if(!File.Exists(origin))
                    throw new FileNotFoundException("Required source or .meta missing",origin);
                var backup=Path.Combine(destination,"ORIGINALS",
                    rel.Replace('/',Path.DirectorySeparatorChar));
                if(File.Exists(backup) && Sha(backup)!=Sha(origin))
                    throw new InvalidOperationException(
                        "Existing immutable backup differs; refusing to overwrite: "+rel);
                pairs.Add((rel,origin,backup));
            }
            if(!string.Equals(AssetDatabase.AssetPathToGUID(item.Path),
                item.Guid,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("GUID path roundtrip mismatch");
        }

        foreach(var pair in pairs)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pair.Backup));
            if(!File.Exists(pair.Backup))
                File.Copy(pair.Original,pair.Backup,overwrite:false);
            if(Sha(pair.Original)!=Sha(pair.Backup))
                throw new InvalidOperationException("Backup verification failed: "+pair.Relative);
        }

        var receipt=new StringBuilder();
        receipt.AppendLine("SkillId,Guid,AssetPath,SourceSHA256,MetaSHA256");
        static string Q(string input)=>"\""+(input??"").Replace("\"","\"\"")+"\"";
        foreach(var item in candidates)
        {
            var actual=Path.Combine(root,item.Path.Replace('/',Path.DirectorySeparatorChar));
            receipt.AppendLine(string.Join(",",new[]{
                item.Skill.SkillId,item.Guid,item.Path,
                Sha(actual),Sha(actual+".meta")
            }.Select(Q)));
        }
        Directory.CreateDirectory(destination);
        string manifest=Path.Combine(destination,"BACKUP_MANIFEST.csv");
        File.WriteAllText(manifest,receipt.ToString(),new UTF8Encoding(false));
        string instructions=Path.Combine(destination,"RESTORE_POLICY_KO.md");
        File.WriteAllText(instructions,
            "# R8 파일럿 원본 백업 및 복원 정책\n\n"+
            "이 디렉터리의 ORIGINALS/Assets 아래 파일은 읽기 전용으로 보존한 원본입니다. "+
            "이번 작업은 원본 SO/GUID/.meta를 전혀 수정하지 않았습니다.\n\n"+
            "실제 향후 승인된 개별 이관에서 오류가 발생한 경우에만 Unity Play Mode 종료 후, "+
            "해당 .asset과 .meta를 본래 Assets 상대 경로에 함께 복원하십시오. "+
            "다른 스킬의 파일은 덮어쓰지 마십시오.\n\n"+
            "BACKUP_MANIFEST.csv의 SHA-256 값을 원본 백업과 비교해 보존 무결성을 확인할 수 있습니다. "+
            "중복 SkillId 4종에 속한 에셋은 보수적인 파일럿에서 제외했습니다.\n",
            new UTF8Encoding(false));

        int pass=0,fail=0;
        void Check(string label,bool ok)
        {
            if(ok){pass++;Debug.Log("[R8-Backup] PASS "+label);}
            else{fail++;Debug.LogError("[R8-Backup] FAIL "+label);}
        }
        Check("B01 expected vetted candidate set",candidates.Length==9);
        Check("B02 source and matching meta backed up",pairs.Count==2*candidates.Length);
        Check("B03 every asset has exact-byte SHA verified backup",
            pairs.All(p=>File.Exists(p.Backup)&&Sha(p.Original)==Sha(p.Backup)));
        Check("B04 all paths are rooted in Assets",
            candidates.All(x=>x.Path.StartsWith("Assets/",StringComparison.Ordinal)));
        Check("B05 every candidate has a unique stable ID",
            candidates.Select(x=>x.Skill.SkillId).Distinct(StringComparer.Ordinal).Count()==candidates.Length);
        Check("B06 all originals retain AssetDatabase GUIDs",
            candidates.All(x=>AssetDatabase.AssetPathToGUID(x.Path)==x.Guid));
        Check("B07 duplicate SkillIds never enter pilot backup",
            candidates.All(x=>!ambiguous.Contains(x.Skill.SkillId)));
        Check("B08 complete manifest has header + one row per asset",
            File.ReadAllLines(manifest).Length==candidates.Length+1);
        Check("B09 human restore instructions preserved",File.Exists(instructions));
        Check("B10 no ScriptableObject marked dirty",
            assets.All(x=>!EditorUtility.IsDirty(x.Skill)));
        Check("B11 no candidate requires effect duplication",
            candidates.All(x=>R7SkillPilotObserver.IsMetadataOnlyNormal(x.Skill)));
        Check("B12 backups are outside the active Unity Assets directory",
            !destination.StartsWith(Application.dataPath+Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
        Debug.Log("[R8-Backup] SUMMARY candidates="+candidates.Length+
            " sourceFileCopies="+pairs.Count+" manifest="+manifest+
            " directory="+destination);
        Debug.Log("[R8-Backup] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R8 backup gate failed "+fail);
    }
}
#endif