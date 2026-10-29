#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot, opt-in repair for Canonical0916 EmotionAugmentDefinition assets
/// whose main MonoBehaviour has m_Script: {fileID: 0} and relies exclusively
/// on m_EditorClassIdentifier. This leaves gameplay fields, asset GUIDs,
/// effect references, and the catalog's existing .meta untouched.
/// </summary>
public static class Canonical1029EmotionDefinitionLoadRepair
{
    private const string Root =
        "Assets/2. Data/Progression/EmotionAugments/Canonical0916";
    private const string ScriptPath =
        "Assets/1. Scripts/Runtime/Systems/Progression/EmotionAugmentDefinition.cs";
    private const string LegacyIdentifier =
        "ProjectAbyss.Runtime::EmotionAugmentDefinition";

    private static readonly string[] Emotions =
    {
        "Compassion", "Faith", "Detachment", "Impression",
        "Awe", "Admiration", "Longing"
    };

    private static readonly Regex ScriptField = new Regex(
        @"(?m)^[ \t]*m_Script: \{[^}\r\n]*\}[ \t]*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex MissingScript = new Regex(
        @"(?m)^([ \t]*)m_Script: \{fileID: 0\}[ \t]*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Identifier = new Regex(
        @"(?m)^[ \t]*m_EditorClassIdentifier: " + Regex.Escape(LegacyIdentifier) + @"[ \t]*$",
        RegexOptions.CultureInvariant);

    private sealed class Patch
    {
        public string Path;
        public string After;
    }

    [MenuItem("Game System Verification/0922 Canonical/Diagnose Emotion Definition Loading (1029)")]
    public static void DiagnoseFromMenu()
    {
        if (!TryDiscover(out List<string> paths, out List<string> problems))
        {
            Debug.LogError("[1029 Emotion Repair] Preflight failed:\n" +
                           string.Join("\n", problems));
            return;
        }

        int loadable = CountLoadable(paths, out List<string> missing);
        Debug.Log($"[1029 Emotion Repair] Canonical definitions: " +
                  $"assets={paths.Count}, loadable={loadable}, expected=63" +
                  (missing.Count == 0 ? "" :
                   "\nUnloadable:\n" + string.Join("\n", missing)));
    }

    [MenuItem("Game System Verification/0922 Canonical/Repair Emotion Definition Script References (1029)")]
    public static void RepairFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogError("[1029 Emotion Repair] Stop Play Mode and wait " +
                           "for script compilation/imports before repairing.");
            return;
        }

        if (!TryDiscover(out List<string> paths, out List<string> problems))
        {
            Debug.LogError("[1029 Emotion Repair] Preflight failed; no files changed:\n" +
                           string.Join("\n", problems));
            return;
        }

        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptPath);
        string scriptGuid = AssetDatabase.AssetPathToGUID(ScriptPath);
        if (script == null || script.GetClass() != typeof(EmotionAugmentDefinition) ||
            string.IsNullOrWhiteSpace(scriptGuid))
        {
            Debug.LogError("[1029 Emotion Repair] EmotionAugmentDefinition.cs " +
                           "could not be resolved to the expected MonoScript. " +
                           "Fix compile/asmdef errors before running this repair.");
            return;
        }

        string validReference =
            $"m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}";

        // Validate ALL 63 definitions before touching disk. Reject unexpected formats.
        List<Patch> patches = new List<Patch>();
        foreach (string path in paths)
        {
            string before = File.ReadAllText(path);
            MatchCollection scriptFields = ScriptField.Matches(before);
            if (scriptFields.Count != 1)
            {
                problems.Add($"Unexpected m_Script field count ({scriptFields.Count}): {path}");
                continue;
            }

            string line = scriptFields[0].Value.Trim();
            if (line == validReference)
                continue; // Already explicitly and correctly linked.

            if (!MissingScript.IsMatch(before) || !Identifier.IsMatch(before))
            {
                problems.Add("Unexpected script/class identifier; not safe to rewrite: " + path +
                             " [" + line + "]");
                continue;
            }

            string after = MissingScript.Replace(before, match =>
                match.Groups[1].Value + validReference, 1);

            if (after == before)
            {
                problems.Add("Script replacement failed: " + path);
                continue;
            }

            patches.Add(new Patch { Path = path, After = after });
        }

        if (problems.Count > 0)
        {
            Debug.LogError("[1029 Emotion Repair] Validation failed; no files changed:\n" +
                           string.Join("\n", problems));
            return;
        }

        if (patches.Count > 0)
        {
            // Backups are placed in Library, outside Assets, before any write.
            string backupRoot = "Library/ProjectAbyssEmotionRepairBackups/" +
                                DateTime.Now.ToString("yyyyMMdd_HHmmss");
            try
            {
                foreach (Patch patch in patches)
                {
                    string backup = Path.Combine(backupRoot, patch.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.Copy(patch.Path, backup, overwrite: false);
                }

                foreach (Patch patch in patches)
                    File.WriteAllText(patch.Path, patch.After, new UTF8Encoding(false));

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[1029 Emotion Repair] Updated {patches.Count}/63 " +
                          $"definition script references. Backup: {backupRoot}");
            }
            catch (Exception exception)
            {
                Debug.LogError("[1029 Emotion Repair] File write or import failed. " +
                               "Restore backed-up assets from Library if necessary. " +
                               exception);
                return;
            }
        }
        else
        {
            Debug.Log("[1029 Emotion Repair] All 63 script references are already explicit.");
        }

        int count = CountLoadable(paths, out List<string> unloadable);
        if (count != 63)
        {
            Debug.LogError($"[1029 Emotion Repair] Script rewiring completed, " +
                           $"but only {count}/63 definitions load. " +
                           "Check Console for upstream compile/asmdef errors; " +
                           "the original asset files are backed up in Library.\n" +
                           string.Join("\n", unloadable));
            return;
        }

        // Delegate catalog ownership/order/registration to the existing migration.
        bool catalogReady =
            Canonical0922Phase12EmotionCatalogRepair.EnsureCanonicalCatalogWiring(
                logResult: true);
        if (catalogReady)
        {
            Debug.Log("[1029 Emotion Repair] PASS: 63 definitions load, " +
                      "and the Phase 12 catalog wiring is valid.");
        }
        else
        {
            Debug.LogError("[1029 Emotion Repair] All 63 definitions now load, " +
                           "but catalog validation still failed. Inspect the " +
                           "preceding Phase 12 error before making further changes.");
        }
    }

    private static bool TryDiscover(out List<string> paths, out List<string> problems)
    {
        paths = new List<string>(63);
        problems = new List<string>();
        foreach (string emotion in Emotions)
        {
            for (int tier = 1; tier <= 3; tier++)
            {
                string folder = $"{Root}/{emotion}/Tier{tier}";
                if (!AssetDatabase.IsValidFolder(folder) || !Directory.Exists(folder))
                {
                    problems.Add("Missing directory: " + folder);
                    continue;
                }

                string[] files = Directory.GetFiles(folder, "*.asset",
                    SearchOption.TopDirectoryOnly)
                    .Select(p => p.Replace('\\', '/'))
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .ToArray();

                if (files.Length != 3)
                {
                    problems.Add($"Expected 3 direct assets but found {files.Length}: {folder}");
                    continue;
                }

                paths.AddRange(files);
            }
        }

        if (paths.Count != 63)
            problems.Add($"Expected 63 assets but found {paths.Count}.");

        return problems.Count == 0;
    }

    private static int CountLoadable(List<string> paths, out List<string> missing)
    {
        int result = 0;
        missing = new List<string>();
        foreach (string path in paths)
        {
            if (AssetDatabase.LoadAssetAtPath<EmotionAugmentDefinition>(path) != null)
                result++;
            else
            {
                Type observed = AssetDatabase.GetMainAssetTypeAtPath(path);
                missing.Add(path + " [mainType=" +
                            (observed == null ? "null" : observed.FullName) + "]");
            }
        }

        return result;
    }
}
#endif
