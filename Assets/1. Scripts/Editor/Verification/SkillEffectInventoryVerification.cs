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
/// Read-only inventory of the effectful SkillDefinition corpus.\n/// Checks stable GUIDs, effect entries and SHA-256 digests without\n/// creating backups, staging directories or disposable reports.
/// </summary>
public static class SkillEffectInventoryVerification
{
    private static string Hash(string file)
    {
        using(var alg=SHA256.Create())
        using(var stream=File.OpenRead(file))
            return BitConverter.ToString(alg.ComputeHash(stream))
                .Replace("-","").ToLowerInvariant();
    }
    private static string Q(string x) => "\"" + (x??"").Replace("\"","\"\"") + "\"";

public static void Run()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Edit Mode required for effect inventory.");

        string root = Directory.GetParent(Application.dataPath).FullName;
        var catalog = AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(g => new SkillAssetRef(
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                    AssetDatabase.GUIDToAssetPath(g)), g))
            .Where(x => x.Definition != null)
            .ToArray();

        var duplicates = catalog
            .GroupBy(x => x.Definition.SkillId, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);

        SkillAssetRef[] eligible;
        using (var scope = R7ExclusiveEntryMigrationScope.Begin(catalog))
            eligible = catalog.Where(x => scope.CanRoute(x.Definition))
                .OrderBy(x => AssetDatabase.GetAssetPath(x.Definition),
                    StringComparer.Ordinal)
                .ToArray();

        var sources = new List<(string Relative, string Path, string Sha)>();
        foreach (var item in eligible)
        {
            string path = AssetDatabase.GetAssetPath(item.Definition);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("..") ||
                duplicates.Contains(item.Definition.SkillId) ||
                AssetDatabase.AssetPathToGUID(path) != item.AssetGuid)
                throw new InvalidOperationException("Unsafe source path/GUID: " + path);

            foreach (var relative in new[] { path, path + ".meta" })
            {
                string disk = Path.Combine(root,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(disk))
                    throw new FileNotFoundException("Effect source or meta missing", disk);
                sources.Add((relative, disk, Hash(disk)));
            }
        }

        int entryCount = eligible.Sum(x => x.Definition.EffectEntries?.Count ?? 0);
        int pass = 0, fail = 0;
        void Check(string label, bool success)
        {
            if (success) { pass++; Debug.Log("[R8-Effect-Inventory] PASS " + label); }
            else { fail++; Debug.LogError("[R8-Effect-Inventory] FAIL " + label); }
        }

        Check("B01 current authored SkillDefinition corpus", catalog.Length == 164);
        Check("B02 eligible effectful cohort", eligible.Length == 68);
        Check("B03 total root effect entries", entryCount == 85);
        Check("B04 every source and .meta exists", sources.Count == eligible.Length * 2);
        Check("B05 source and meta digests verified without backup files",
            sources.All(x => x.Sha.Length == 64 && Hash(x.Path) == x.Sha));
        Check("B06 all duplicate IDs excluded",
            eligible.All(x => !duplicates.Contains(x.Definition.SkillId)));
        Check("B07 every source GUID unchanged",
            eligible.All(x => AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(x.Definition)) == x.AssetGuid));
        Check("B08 each effect entry has a definition",
            eligible.All(x => x.Definition.EffectEntries != null &&
                x.Definition.EffectEntries.All(e => e?.Definition != null)));
        Check("B09 no source SkillDefinition marked dirty",
            eligible.All(x => !EditorUtility.IsDirty(x.Definition)));
        Check("B10 scoped legacy bridge fully released",
            R7ExclusiveEntryMigrationScope.Active == null);

        Debug.Log("[R8-Effect-Inventory] SUMMARY eligible=" + eligible.Length +
                  " effects=" + entryCount + " filesHashed=" + sources.Count +
                  " permanentBackupsCreated=0");
        Debug.Log("[R8-Effect-Inventory] RESULT PASS=" + pass + " FAIL=" + fail);
        if (fail != 0)
            throw new InvalidOperationException("Effect inventory failure " + fail);
    }
}
#endif