#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Canonical0917EmotionAugmentVerification
{
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

    [MenuItem("Game System Verification/0917 Spec Patch/Verify 0917 Emotion Roster")]
    public static void VerifyFromMenu()
    {
        List<string> pass = new List<string>();
        List<string> fail = new List<string>();
        List<string> details = new List<string>();

        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                Canonical0917EmotionAugmentMigration.CatalogPath);

        if (catalog?.Entries == null)
        {
            fail.Add("EmotionAugmentCatalog missing/null");
            Log(pass, fail, details);
            return;
        }

        List<EmotionAugmentDefinition> entries =
            catalog.Entries.Where(x => x != null).ToList();

        if (entries.Count == 63)
            pass.Add("Emotion augment catalog total = 63");
        else
            fail.Add($"Emotion augment catalog total = {entries.Count}, expected 63");

        int uniqueRefs = entries.Distinct().Count();
        if (uniqueRefs == 63)
            pass.Add("Emotion augment catalog references = 63 unique");
        else
            fail.Add($"Emotion augment unique references = {uniqueRefs}/63");

        int uniqueIds = entries
            .Where(x => !string.IsNullOrWhiteSpace(x.AugmentId))
            .Select(x => x.AugmentId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (uniqueIds == 63)
            pass.Add("Emotion AugmentId = 63 unique");
        else
            fail.Add($"Emotion AugmentId unique = {uniqueIds}/63");

        List<string> outsideCanonical = entries
            .Select(AssetDatabase.GetAssetPath)
            .Where(path =>
                string.IsNullOrEmpty(path) ||
                !path.StartsWith(
                    Canonical0917EmotionAugmentMigration.CanonicalRoot + "/",
                    StringComparison.Ordinal))
            .ToList();

        if (outsideCanonical.Count == 0)
            pass.Add("Catalog ownership = Canonical0916 only (legacy/stale refs excluded)");
        else
            fail.Add(
                "Catalog contains non-canonical refs: " +
                string.Join(", ", outsideCanonical));

        bool allShape = true;
        foreach (EmotionType emotion in CanonicalEmotionOrder)
        {
            int emotionTotal = 0;
            List<string> tierParts = new List<string>();

            for (int tier = 1; tier <= 3; tier++)
            {
                int serializedCount = entries.Count(x =>
                    x.Emotion == emotion && x.Tier == tier);
                int runtimeCount = catalog.GetCandidateCount(emotion, tier);

                List<EmotionAugmentDefinition> offer =
                    new List<EmotionAugmentDefinition>();
                bool offerOk = catalog.TryBuildCanonicalOffer(
                    emotion,
                    tier,
                    offer,
                    out string reason);

                bool tierOk =
                    serializedCount == 3 &&
                    runtimeCount == 3 &&
                    offerOk &&
                    offer.Count == 3;

                emotionTotal += serializedCount;
                allShape &= tierOk;
                tierParts.Add(
                    $"T{tier}={serializedCount}/runtime{runtimeCount}" +
                    (offerOk ? string.Empty : $" ({reason})"));

                string expectedFolder =
                    $"{Canonical0917EmotionAugmentMigration.CanonicalRoot}/{emotion}/Tier{tier}/";
                foreach (EmotionAugmentDefinition definition in
                         entries.Where(x => x.Emotion == emotion && x.Tier == tier))
                {
                    string path = AssetDatabase.GetAssetPath(definition);
                    if (!path.StartsWith(expectedFolder, StringComparison.Ordinal))
                    {
                        allShape = false;
                        fail.Add(
                            $"Folder classification mismatch: {definition.AugmentId} -> {path}; " +
                            $"expected under {expectedFolder}");
                    }
                }
            }

            allShape &= emotionTotal == 9;
            details.Add(
                $"{emotion}: {string.Join(" | ", tierParts)} | total={emotionTotal}");
        }

        if (allShape)
            pass.Add("7 emotions × 3 tiers × 3 cards = runtime reachable 63/63");
        else
            fail.Add("7-emotion tier shape/runtime offer contract mismatch");

        Log(pass, fail, details);
    }

    private static void Log(
        List<string> pass,
        List<string> fail,
        List<string> details)
    {
        string message =
            "[0917 Emotion Roster Verification]\n" +
            $"PASS={pass.Count} FAIL={fail.Count}\n\n" +
            "PASS\n- " + string.Join("\n- ", pass) + "\n\n" +
            "DETAILS\n- " + string.Join("\n- ", details) + "\n\n" +
            "FAIL\n- " + string.Join("\n- ", fail);

        if (fail.Count > 0)
            Debug.LogError(message);
        else
            Debug.Log(message);
    }
}
#endif
