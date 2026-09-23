#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [0922_PHASE12_EMOTION_CATALOG_REPAIR]
/// Repairs the existing EmotionAugmentCatalog roster from the already-authored
/// Canonical0916 definition assets. This never invents gameplay values and never
/// recreates the definition assets.
/// </summary>
[InitializeOnLoad]
public static class Canonical0922Phase12EmotionCatalogRepair
{
    public const string CatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    public const string CanonicalRoot =
        "Assets/2. Data/Progression/EmotionAugments/Canonical0916";

    public const string RegistryPath =
        "Assets/Resources/ProjectAbyss/RunFlowLiveContentRegistry.asset";

    private const int ExpectedCount = 63;

    static Canonical0922Phase12EmotionCatalogRepair()
    {
        EditorApplication.delayCall += RepairSilentlyWhenSettled;
    }

    [MenuItem(
        "Game System Verification/0922 Canonical/" +
        "Phase 12 - Repair Emotion Catalog Wiring")]
    public static void RepairFromMenu()
    {
        bool ok = EnsureCanonicalCatalogWiring(logResult: true);

        if (ok)
        {
            Debug.Log(
                "[0922 Phase12][EmotionCatalogRepair] " +
                "PASS - canonical 63-card catalog wiring is healthy.");
        }
    }

    private static void RepairSilentlyWhenSettled()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RepairSilentlyWhenSettled;
            return;
        }

        EnsureCanonicalCatalogWiring(logResult: false);
    }

    public static bool EnsureCanonicalCatalogWiring(bool logResult)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            if (logResult)
            {
                Debug.LogWarning(
                    "[0922 Phase12][EmotionCatalogRepair] " +
                    "Editor is compiling/updating. Retry after it settles.");
            }

            return false;
        }

        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(CatalogPath);

        if (IsHealthy(catalog, out string healthyDetails))
        {
            EnsureRegistryReference(catalog);
            return true;
        }

        string[] canonicalAssets =
            Directory.Exists(CanonicalRoot)
                ? Directory
                    .GetFiles(
                        CanonicalRoot,
                        "*.asset",
                        SearchOption.AllDirectories)
                    .Select(path => path.Replace('\\', '/'))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();

        foreach (string assetPath in canonicalAssets)
        {
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);
        }

        AssetDatabase.ImportAsset(
            CatalogPath,
            ImportAssetOptions.ForceUpdate |
            ImportAssetOptions.ForceSynchronousImport);

        bool rosterApplied =
            Canonical0917EmotionAugmentMigration.ApplyCanonicalRoster(
                out string rosterReport);

        if (!rosterApplied)
        {
            string message =
                "[0922 Phase12][EmotionCatalogRepair] " +
                "Canonical roster reconstruction failed.\n" +
                rosterReport;

            if (logResult)
                Debug.LogError(message);
            else
                Debug.LogWarning(message);

            return false;
        }

        AssetDatabase.SaveAssets();

        AssetDatabase.ImportAsset(
            CatalogPath,
            ImportAssetOptions.ForceUpdate |
            ImportAssetOptions.ForceSynchronousImport);

        catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(CatalogPath);

        bool repaired =
            IsHealthy(catalog, out string repairedDetails);

        if (!repaired)
        {
            string message =
                "[0922 Phase12][EmotionCatalogRepair] " +
                "Roster rebuild completed but validation still failed. " +
                repairedDetails;

            if (logResult)
                Debug.LogError(message);
            else
                Debug.LogWarning(message);

            return false;
        }

        EnsureRegistryReference(catalog);
        AssetDatabase.SaveAssets();

        if (logResult)
        {
            Debug.Log(
                "[0922 Phase12][EmotionCatalogRepair] Repaired. " +
                repairedDetails);
        }

        return true;
    }

    private static bool IsHealthy(
        EmotionAugmentCatalog catalog,
        out string details)
    {
        if (catalog?.Entries == null)
        {
            details = "Catalog or Entries is null.";
            return false;
        }

        int slots = catalog.Entries.Count;
        int nonNull = catalog.Entries.Count(x => x != null);
        int uniqueIds =
            catalog.Entries
                .Where(x => x != null)
                .Select(x => x.AugmentId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Count();

        details =
            $"Slots={slots}, NonNull={nonNull}, UniqueIds={uniqueIds}";

        return
            slots == ExpectedCount &&
            nonNull == ExpectedCount &&
            uniqueIds == ExpectedCount;
    }

    private static void EnsureRegistryReference(
        EmotionAugmentCatalog catalog)
    {
        if (catalog == null)
            return;

        RunFlowLiveContentRegistry registry =
            AssetDatabase.LoadAssetAtPath<RunFlowLiveContentRegistry>(
                RegistryPath);

        if (registry == null ||
            ReferenceEquals(registry.EmotionAugmentCatalog, catalog))
        {
            return;
        }

        registry.EmotionAugmentCatalog = catalog;
        EditorUtility.SetDirty(registry);
    }
}
#endif
