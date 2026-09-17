#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0917 emotion augment roster repair.
/// Canonical source is the existing Canonical0916 folder because the 0917 XLSX
/// does not change the emotion-augment sheet.
///
/// This migration does not invent or rebalance augment effects. It only repairs
/// roster ownership/classification so the fervor offer contract can always see
/// exactly 7 emotions x 3 tiers x 3 cards.
/// </summary>
public static class Canonical0917EmotionAugmentMigration
{
    public const string CatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    public const string CanonicalRoot =
        "Assets/2. Data/Progression/EmotionAugments/Canonical0916";

    // Preserve the same canonical authoring order used by PhaseEContentMigration.
    private static readonly EmotionType[] CanonicalEmotionOrder =
    {
        EmotionType.Compassion,
        EmotionType.Faith,
        EmotionType.Detachment,
        EmotionType.Impression,
        EmotionType.Awe,
        EmotionType.Admiration,
        EmotionType.Longing
    };

    private sealed class ExpectedSlot
    {
        public EmotionAugmentDefinition Definition;
        public EmotionType Emotion;
        public int Tier;
        public string Path;
    }

    [MenuItem("Game System Verification/0917 Spec Patch/Apply 0917 Emotion Roster Patch")]
    public static void ApplyFromMenu()
    {
        if (ApplyCanonicalRoster(out string report))
            Debug.Log("[0917 Emotion Roster Patch] PASS\n" + report);
        else
            Debug.LogError("[0917 Emotion Roster Patch] FAIL\n" + report);
    }

    /// <summary>
    /// Rebuilds the catalog from Canonical0916 only and normalizes the serialized
    /// Emotion/Tier fields from the folder contract. Returns false without writing
    /// a partial catalog when any canonical tier folder does not contain 3 cards.
    /// </summary>
    public static bool ApplyCanonicalRoster(out string report)
    {
        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(CatalogPath);

        if (catalog == null)
        {
            report = $"EmotionAugmentCatalog missing: {CatalogPath}";
            return false;
        }

        List<ExpectedSlot> slots = new List<ExpectedSlot>(63);
        List<string> errors = new List<string>();

        foreach (EmotionType emotion in CanonicalEmotionOrder)
        {
            for (int tier = 1; tier <= 3; tier++)
            {
                string tierPath = $"{CanonicalRoot}/{emotion}/Tier{tier}";
                if (!AssetDatabase.IsValidFolder(tierPath))
                {
                    errors.Add($"Missing folder: {tierPath}");
                    continue;
                }

                // Do NOT use AssetDatabase.FindAssets("t:EmotionAugmentDefinition") here.
                // These canonical assets are NativeFormatImporter assets that serialize with
                // m_Script:fileID=0 + m_EditorClassIdentifier, so Unity's type-filter index
                // can legitimately return zero even though LoadAssetAtPath resolves them.
                // Enumerate only the direct .asset files in the tier folder instead.
                string[] assetPaths = System.IO.Directory
                    .GetFiles(tierPath, "*.asset", System.IO.SearchOption.TopDirectoryOnly)
                    .Select(path => path.Replace('\\', '/'))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                List<ExpectedSlot> tierSlots = new List<ExpectedSlot>(3);
                List<string> unloadable = new List<string>();

                foreach (string assetPath in assetPaths)
                {
                    EmotionAugmentDefinition definition =
                        AssetDatabase.LoadAssetAtPath<EmotionAugmentDefinition>(assetPath);

                    if (definition == null)
                    {
                        unloadable.Add(assetPath);
                        continue;
                    }

                    tierSlots.Add(new ExpectedSlot
                    {
                        Definition = definition,
                        Emotion = emotion,
                        Tier = tier,
                        Path = assetPath
                    });
                }

                if (tierSlots.Count != 3)
                {
                    errors.Add(
                        $"{emotion} Tier{tier}: direct assets={assetPaths.Length}, " +
                        $"loadable definitions={tierSlots.Count}, expected=3" +
                        (unloadable.Count > 0
                            ? $"; unloadable=[{string.Join(", ", unloadable)}]"
                            : string.Empty));
                    continue;
                }

                slots.AddRange(tierSlots);
            }
        }

        if (errors.Count > 0 || slots.Count != 63)
        {
            if (slots.Count != 63)
                errors.Add($"Canonical roster total={slots.Count}, expected=63");

            report = string.Join("\n", errors);
            return false;
        }

        // Validate identity before changing data. We preserve the canonical IDs;
        // this patch is classification/roster repair, not content invention.
        List<string> missingIds = slots
            .Where(x => string.IsNullOrWhiteSpace(x.Definition.AugmentId))
            .Select(x => x.Path)
            .ToList();

        List<string> duplicateIds = slots
            .Where(x => !string.IsNullOrWhiteSpace(x.Definition.AugmentId))
            .GroupBy(x => x.Definition.AugmentId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (missingIds.Count > 0 || duplicateIds.Count > 0)
        {
            if (missingIds.Count > 0)
                errors.Add("Missing AugmentId: " + string.Join(", ", missingIds));
            if (duplicateIds.Count > 0)
                errors.Add("Duplicate AugmentId: " + string.Join(", ", duplicateIds));

            report = string.Join("\n", errors);
            return false;
        }

        int classificationRepairs = 0;
        foreach (ExpectedSlot slot in slots)
        {
            EmotionAugmentDefinition definition = slot.Definition;
            bool dirty = false;

            if (definition.Emotion != slot.Emotion)
            {
                definition.Emotion = slot.Emotion;
                dirty = true;
            }

            if (definition.Tier != slot.Tier)
            {
                definition.Tier = slot.Tier;
                dirty = true;
            }

            if (dirty)
            {
                classificationRepairs++;
                EditorUtility.SetDirty(definition);
            }
        }

        catalog.Entries ??= new List<EmotionAugmentDefinition>();

        bool rosterChanged =
            catalog.Entries.Count != slots.Count ||
            !catalog.Entries.SequenceEqual(slots.Select(x => x.Definition));

        if (rosterChanged)
        {
            catalog.Entries.Clear();
            catalog.Entries.AddRange(slots.Select(x => x.Definition));
            EditorUtility.SetDirty(catalog);
        }

        AssetDatabase.SaveAssets();

        // Runtime-facing contract verification after the repair.
        List<string> grid = new List<string>();
        bool runtimeShape = true;
        foreach (EmotionType emotion in CanonicalEmotionOrder)
        {
            int total = 0;
            List<string> tiers = new List<string>();
            for (int tier = 1; tier <= 3; tier++)
            {
                int count = catalog.GetCandidateCount(emotion, tier);
                total += count;
                tiers.Add($"T{tier}={count}");
                runtimeShape &= count == 3;
            }
            runtimeShape &= total == 9;
            grid.Add($"{emotion}: {string.Join(" ", tiers)} total={total}");
        }

        if (!runtimeShape || catalog.Entries.Count(x => x != null) != 63)
        {
            report =
                "Post-repair runtime shape is still invalid.\n" +
                string.Join("\n", grid);
            return false;
        }

        report =
            $"Canonical roster=63, classification repairs={classificationRepairs}, " +
            $"catalog rebuilt={rosterChanged}.\n" +
            string.Join("\n", grid);
        return true;
    }
}
#endif
