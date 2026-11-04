#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Canonical combat augmentation inventory. The 63 usable augments are
/// registered explicitly in EmotionAugmentCatalog. Other importable assets
/// (including 9 unregistered Awe prototype drafts) are authoring candidates,
/// not additional active augments. Never delete or silently include them.
/// </summary>
public static class CanonicalEmotionAugmentInventory
{
    public const string CatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    public static EmotionAugmentDefinition[] Load()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(CatalogPath);
        if (catalog == null || catalog.Entries == null)
            throw new InvalidOperationException("Canonical EmotionAugmentCatalog is unavailable.");

        var entries = catalog.Entries.ToArray();
        if (entries.Any(x => x == null))
            throw new InvalidOperationException("Canonical EmotionAugmentCatalog contains null entries.");

        var guids = entries.Select(x =>
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x))).ToArray();

        if (guids.Any(string.IsNullOrWhiteSpace) ||
            guids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
            throw new InvalidOperationException(
                "Canonical EmotionAugmentCatalog has missing or duplicate asset references.");

        return entries;
    }

    public static EmotionAugmentDefinition[] UnregisteredDrafts()
    {
        var canonical = new HashSet<EmotionAugmentDefinition>(Load());
        return AssetDatabase.FindAssets("t:EmotionAugmentDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<EmotionAugmentDefinition>)
            .Where(x => x != null && !canonical.Contains(x))
            .OrderBy(x => AssetDatabase.GetAssetPath(x), StringComparer.Ordinal)
            .ToArray();
    }
}
#endif