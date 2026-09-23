#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Emotion33Verification
{
    private const string CatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    private static readonly Dictionary<string, Canonical0922Emotion33EffectKind>
        Expected = new(StringComparer.Ordinal)
        {
            ["emotion.compassion.t1.02"] = Canonical0922Emotion33EffectKind.CompassionImmediateHeal,
            ["emotion.compassion.t2.01"] = Canonical0922Emotion33EffectKind.CompassionExchangeWinRecovery,
            ["emotion.compassion.t2.02"] = Canonical0922Emotion33EffectKind.CompassionLifesteal,
            ["emotion.compassion.t2.03"] = Canonical0922Emotion33EffectKind.CompassionLastStandRecovery,
            ["emotion.compassion.t3.01"] = Canonical0922Emotion33EffectKind.CompassionHealingConversion,
            ["emotion.compassion.t3.02"] = Canonical0922Emotion33EffectKind.CompassionOverhealToBlock,

            ["emotion.faith.t1.01"] = Canonical0922Emotion33EffectKind.FaithTurnStartBlock,
            ["emotion.faith.t1.02"] = Canonical0922Emotion33EffectKind.FaithEnemyAttackSlotBlock,
            ["emotion.faith.t1.03"] = Canonical0922Emotion33EffectKind.FaithDamageReflection,
            ["emotion.faith.t2.01"] = Canonical0922Emotion33EffectKind.FaithEscalatingBlockGain,
            ["emotion.faith.t2.02"] = Canonical0922Emotion33EffectKind.FaithFirstClashDamageNullify,
            ["emotion.faith.t2.03"] = Canonical0922Emotion33EffectKind.FaithDamageReflection,
            ["emotion.faith.t3.02"] = Canonical0922Emotion33EffectKind.FaithTurnEndBlockToHeal,
            ["emotion.faith.t3.03"] = Canonical0922Emotion33EffectKind.FaithBlockConversion,

            ["emotion.detachment.t1.01"] = Canonical0922Emotion33EffectKind.DetachmentPartMaximumHp,
            ["emotion.detachment.t1.02"] = Canonical0922Emotion33EffectKind.DetachmentStaggerMaximum,
            ["emotion.detachment.t1.03"] = Canonical0922Emotion33EffectKind.DetachmentSingleWeakenedPenaltySuppression,
            ["emotion.detachment.t2.01"] = Canonical0922Emotion33EffectKind.DetachmentKillMaximumHpGrowth,
            ["emotion.detachment.t2.02"] = Canonical0922Emotion33EffectKind.DetachmentDamageDeferral,

            ["emotion.awe.t1.01"] = Canonical0922Emotion33EffectKind.AweFirstTurnStrength,
            ["emotion.awe.t1.03"] = Canonical0922Emotion33EffectKind.AweNextTurnSwift,
            ["emotion.awe.t2.01"] = Canonical0922Emotion33EffectKind.AweInfiniteHeadSpeed,
            ["emotion.awe.t2.02"] = Canonical0922Emotion33EffectKind.AweLastStandNextTurnStrength,
            ["emotion.awe.t2.03"] = Canonical0922Emotion33EffectKind.AweOverwhelmNextTurnStrength,
            ["emotion.awe.t3.03"] = Canonical0922Emotion33EffectKind.AweInfiniteArmsSpeed,

            ["emotion.admiration.t1.01"] = Canonical0922Emotion33EffectKind.AdmirationHitPrestige,
            ["emotion.admiration.t1.02"] = Canonical0922Emotion33EffectKind.AdmirationTakenPrestige,
            ["emotion.admiration.t1.03"] = Canonical0922Emotion33EffectKind.AdmirationClashPrestige,
            ["emotion.admiration.t2.03"] = Canonical0922Emotion33EffectKind.AdmirationElapsedClashPrestige,
            ["emotion.admiration.t3.01"] = Canonical0922Emotion33EffectKind.AdmirationPrestigeStockpile,
            ["emotion.admiration.t3.02"] = Canonical0922Emotion33EffectKind.AdmirationPrestigeFromStagger,

            ["emotion.longing.t1.03"] = Canonical0922Emotion33EffectKind.LongingNeutralThreshold,
            ["emotion.longing.t2.03"] = Canonical0922Emotion33EffectKind.LongingNeutralTurnEndEnergy
        };

    [MenuItem("Game System Verification/0922 Canonical/Emotion 33 - Verify Canonical Runtime Patch")]
    public static void VerifyFromMenu()
    {
        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(CatalogPath);

        if (catalog?.Entries == null)
        {
            Debug.LogError("[0923 Emotion33 Verify] Catalog missing.");
            return;
        }

        Dictionary<string, EmotionAugmentDefinition> byId =
            catalog.Entries
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.AugmentId))
                .GroupBy(x => x.AugmentId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        List<string> failures = new();
        int canonical = 0;
        int tempProxy = 0;

        foreach (KeyValuePair<string, Canonical0922Emotion33EffectKind> pair in Expected)
        {
            if (!byId.TryGetValue(pair.Key, out EmotionAugmentDefinition card) ||
                card == null)
            {
                failures.Add($"{pair.Key}: card missing");
                continue;
            }

            if (card.DesignStatus != "확정")
                failures.Add($"{pair.Key}: DesignStatus={card.DesignStatus}");

            if (card.RuntimeReadiness != EmotionAugmentRuntimeReadiness.RuntimeConnected)
                failures.Add($"{pair.Key}: RuntimeReadiness={card.RuntimeReadiness}");

            if (card.Effects == null || card.Effects.Count != 1)
            {
                failures.Add($"{pair.Key}: Effects.Count={card.Effects?.Count ?? 0}, expected 1");
                continue;
            }

            EmotionAugmentEffectDefinition effect = card.Effects[0];

            if (effect is TempBalanceEmotionProxyEffectDefinition)
            {
                tempProxy++;
                failures.Add($"{pair.Key}: still TEMP_BALANCE_V1 proxy");
                continue;
            }

            if (effect is not Canonical0922Emotion33EffectDefinition canonicalEffect)
            {
                failures.Add($"{pair.Key}: effect type={effect?.GetType().Name ?? "null"}");
                continue;
            }

            canonical++;
            if (canonicalEffect.Kind != pair.Value)
            {
                failures.Add(
                    $"{pair.Key}: Kind={canonicalEffect.Kind}, expected={pair.Value}");
            }
        }

        ValidateCoreValues(byId, failures);

        bool pass =
            failures.Count == 0 &&
            canonical == 33 &&
            tempProxy == 0;

        string summary =
            $"Cards={Expected.Count}, Canonical={canonical}, TEMP={tempProxy}, Failures={failures.Count}";

        if (pass)
        {
            Debug.Log(
                "[0923 Emotion33 Verify] PASS. " + summary +
                ". 확정 33장에 TEMP proxy 연결이 없습니다.");
        }
        else
        {
            Debug.LogError(
                "[0923 Emotion33 Verify] FAIL. " + summary + "\n- " +
                string.Join("\n- ", failures));
        }
    }

    private static void ValidateCoreValues(
        IReadOnlyDictionary<string, EmotionAugmentDefinition> byId,
        List<string> failures)
    {
        Expect(byId, failures, "emotion.compassion.t1.02", e => e.Amount == 40, "Amount=40");
        Expect(byId, failures, "emotion.compassion.t2.01", e => e.Amount == 4 && e.SecondaryAmount == 2, "4/2");
        Expect(byId, failures, "emotion.compassion.t2.02", e => Mathf.Approximately(e.Ratio, .10f), "Ratio=.10");
        Expect(byId, failures, "emotion.compassion.t3.02", e => e.MaximumTotal == 50, "Cap=50");

        Expect(byId, failures, "emotion.faith.t1.01", e => e.Amount == 3, "Amount=3");
        Expect(byId, failures, "emotion.faith.t1.03", e => Mathf.Approximately(e.Ratio, .05f), "Ratio=.05");
        Expect(byId, failures, "emotion.faith.t2.01", e => e.Amount == 2, "Amount=2");
        Expect(byId, failures, "emotion.faith.t2.03", e => Mathf.Approximately(e.Ratio, .12f), "Ratio=.12");
        Expect(byId, failures, "emotion.faith.t3.03", e => Mathf.Approximately(e.Multiplier, 3f), "Multiplier=3");

        Expect(byId, failures, "emotion.detachment.t1.01", e => Mathf.Approximately(e.Ratio, .05f), "Ratio=.05");
        Expect(byId, failures, "emotion.detachment.t1.02", e => e.Amount == 50, "Amount=50");
        Expect(byId, failures, "emotion.detachment.t2.01", e => e.Amount == 2, "Amount=2");

        Expect(byId, failures, "emotion.awe.t1.01", e => e.Amount == 2 && e.Duration == 1, "2·1");
        Expect(byId, failures, "emotion.awe.t1.03", e => e.Amount == 1 && e.Duration == 3, "1·3");
        Expect(byId, failures, "emotion.awe.t2.01", e => e.Amount == 1, "MaxLight -1");
        Expect(byId, failures, "emotion.awe.t2.02", e => e.Amount == 2 && e.Duration == 1, "2·1");
        Expect(byId, failures, "emotion.awe.t2.03", e => e.Amount == 1 && e.Duration == 1, "1·1");
        Expect(byId, failures, "emotion.awe.t3.03", e => e.Amount == 1, "MaxLight -1");

        Expect(byId, failures, "emotion.admiration.t1.01", e => e.Amount == 1, "+1");
        Expect(byId, failures, "emotion.admiration.t1.02", e => e.Amount == 1, "+1");
        Expect(byId, failures, "emotion.admiration.t1.03", e => e.Amount == 2, "+2");
        Expect(byId, failures, "emotion.admiration.t2.03", e => e.Amount == 6, "×6");
        Expect(byId, failures, "emotion.admiration.t3.01", e => e.Amount == 2, "×2 max");

        Expect(byId, failures, "emotion.longing.t1.03", e => e.Amount == 40, "threshold=40");
        Expect(byId, failures, "emotion.longing.t2.03", e => e.Amount == 1, "Energy +1");
    }

    private static void Expect(
        IReadOnlyDictionary<string, EmotionAugmentDefinition> byId,
        List<string> failures,
        string id,
        Func<Canonical0922Emotion33EffectDefinition, bool> predicate,
        string expected)
    {
        if (!byId.TryGetValue(id, out EmotionAugmentDefinition card) ||
            card?.Effects == null ||
            card.Effects.Count != 1 ||
            card.Effects[0] is not Canonical0922Emotion33EffectDefinition effect)
        {
            return;
        }

        if (!predicate(effect))
            failures.Add($"{id}: value mismatch, expected {expected}");
    }
}
#endif
