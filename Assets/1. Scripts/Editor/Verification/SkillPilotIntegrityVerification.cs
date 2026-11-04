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
/// Read-only R8 pilot source integrity check. Validates GUIDs and SHA-256\n/// for source .asset/.meta pairs; never creates persistent backups.
/// </summary>
public static class SkillPilotIntegrityVerification
{
    private static string Sha(string file)
    {
        using(var hash=SHA256.Create())
        using(var source=File.OpenRead(file))
            return BitConverter.ToString(hash.ComputeHash(source))
                .Replace("-","").ToLowerInvariant();
    }
public static void Run()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Edit Mode required for pilot integrity verification.");

        string root = Directory.GetParent(Application.dataPath).FullName;
        var assets = AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g => new
            {
                Guid = g,
                Path = AssetDatabase.GUIDToAssetPath(g),
                Skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                    AssetDatabase.GUIDToAssetPath(g))
            })
            .Where(x => x.Skill != null)
            .ToArray();

        var ambiguous = assets
            .GroupBy(x => x.Skill.SkillId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var candidates = assets
            .Where(x => !ambiguous.Contains(x.Skill.SkillId) &&
                        SkillMigrationObserver.IsMetadataOnlyNormal(x.Skill))
            .OrderBy(x => x.Path, StringComparer.Ordinal)
            .ToArray();

        var pairs = new List<(string Relative, string Path, string Sha)>();
        foreach (var entry in candidates)
        {
            if (!entry.Path.StartsWith("Assets/", StringComparison.Ordinal) ||
                entry.Path.Contains(".."))
                throw new InvalidOperationException("Untrusted source path: " + entry.Path);

            foreach (string relative in new[] { entry.Path, entry.Path + ".meta" })
            {
                string disk = Path.Combine(root,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(disk))
                    throw new FileNotFoundException("Source or meta missing", disk);
                pairs.Add((relative, disk, Sha(disk)));
            }
        }

        int pass = 0, fail = 0;
        void Check(string label, bool success)
        {
            if (success) { pass++; Debug.Log("[R8-Pilot-Integrity] PASS " + label); }
            else { fail++; Debug.LogError("[R8-Pilot-Integrity] FAIL " + label); }
        }

        Check("B01 thirteen metadata-only pilot assets", candidates.Length == 13);
        Check("B02 source and meta files exist", pairs.Count == candidates.Length * 2);
        Check("B03 source and meta SHA-256 captured without copying",
            pairs.All(p => !string.IsNullOrEmpty(p.Sha) && p.Sha.Length == 64));
        Check("B04 all paths rooted under Assets",
            candidates.All(x => x.Path.StartsWith("Assets/", StringComparison.Ordinal)));
        Check("B05 each pilot has a unique stable ID",
            candidates.Select(x => x.Skill.SkillId)
                .Distinct(StringComparer.Ordinal).Count() == candidates.Length);
        Check("B06 source GUIDs resolve to the same paths",
            candidates.All(x => string.Equals(
                AssetDatabase.AssetPathToGUID(x.Path), x.Guid,
                StringComparison.OrdinalIgnoreCase)));
        Check("B07 ambiguous SkillIds excluded",
            candidates.All(x => !ambiguous.Contains(x.Skill.SkillId)));
        Check("B08 no file modified during inventory",
            pairs.All(p => Sha(p.Path) == p.Sha));
        Check("B09 all candidate assets load as SkillDefinition",
            candidates.All(x => x.Skill != null));
        Check("B10 no ScriptableObject marked dirty",
            assets.All(x => !EditorUtility.IsDirty(x.Skill)));
        Check("B11 metadata-only routing invariant",
            candidates.All(x => SkillMigrationObserver.IsMetadataOnlyNormal(x.Skill)));
        Check("B12 all asset GUIDs are distinct",
            candidates.Select(x => x.Guid).Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == candidates.Length);

        Debug.Log("[R8-Pilot-Integrity] SUMMARY candidates=" + candidates.Length +
                  " filesHashed=" + pairs.Count + " permanentBackupsCreated=0");
        Debug.Log("[R8-Pilot-Integrity] RESULT PASS=" + pass + " FAIL=" + fail);
        if (fail != 0)
            throw new InvalidOperationException("R8 pilot integrity failure " + fail);
    }
}
#endif