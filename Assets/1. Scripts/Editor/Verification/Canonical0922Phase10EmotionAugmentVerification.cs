#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Phase10EmotionAugmentVerification
{
    private const string Phase9ReportPath =
        "Logs/GameSystemVerification/0922_Phase9_EnemyEncounter.md";
    private const string ReportPath =
        "Logs/GameSystemVerification/0922_Phase10_EmotionAugment.md";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 10 - Verify Emotion Augment")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new();
        List<string> pending = BuildPendingCanonical();

        AddPrerequisites(checks);
        AddCatalogChecks(checks);
        AddProgressionChecks(checks);
        AddInfiniteStatusChecks(checks);
        AddCanonicalEffectChecks(checks);
        AddConfirmedSemanticsCoverageChecks(checks);
        AddCleanupChecks(checks);

        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;
        string result = fail == 0
            ? "PASS_EMOTION_AUGMENT_0922"
            : "FAIL";

        WriteReport(checks, pending, pass, fail, result);

        string summary =
            "[0922 Phase 10 · Emotion Augment / Progression]\n" +
            $"PASS={pass} FAIL={fail} PENDING_CANONICAL={pending.Count}\n\n" +
            "PASS\n- " +
            string.Join("\n- ", checks.Where(x => x.Passed)
                .Select(x => $"{x.Id} | {x.Name} | {x.Actual}")) +
            "\n\nFAIL\n- " +
            (fail == 0
                ? "NONE"
                : string.Join("\n- ", checks.Where(x => !x.Passed)
                    .Select(x => $"{x.Id} | {x.Name} | {x.Actual}"))) +
            "\n\nPENDING_CANONICAL\n- " +
            string.Join("\n- ", pending) +
            $"\n\nPHASE10_RESULT={result}";

        if (fail == 0) Debug.Log(summary);
        else Debug.LogError(summary);
    }

    private static void AddPrerequisites(List<Check> checks)
    {
        Add(checks, "P10-H00", "0922 canonical identity",
            Canonical0922BaselineSpec.CanonicalVersion == "0922",
            $"Canonical={Canonical0922BaselineSpec.CanonicalVersion}");

        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string path = Path.Combine(root, Phase9ReportPath);
        bool exists = File.Exists(path);
        string report = exists ? File.ReadAllText(path) : string.Empty;
        bool passed =
            report.Contains("PHASE9_RESULT=PASS_ENEMY_ENCOUNTER_0922", StringComparison.Ordinal) ||
            report.Contains("RESULT: PASS_ENEMY_ENCOUNTER_0922", StringComparison.Ordinal);

        Add(checks, "P10-H01", "Phase 9 Enemy/Encounter closed",
            exists && passed,
            !exists ? "Phase9 report missing" : $"ReportPresent=True, ResultPass={passed}");
    }

    private static void AddCatalogChecks(List<Check> checks)
    {
        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                Canonical0922Phase10EmotionAugmentMigration.CatalogPath);

        Add(checks, "P10-C00", "EmotionAugmentCatalog present",
            catalog != null,
            catalog != null ? catalog.name : "MISSING");

        if (catalog?.Entries == null)
            return;

        List<EmotionAugmentDefinition> entries =
            catalog.Entries.Where(x => x != null).ToList();
        int uniqueIds = entries
            .Where(x => !string.IsNullOrWhiteSpace(x.AugmentId))
            .Select(x => x.AugmentId)
            .Distinct(StringComparer.Ordinal)
            .Count();

        Add(checks, "P10-C01", "Canonical catalog = 63 unique entries",
            entries.Count == 63 && uniqueIds == 63 && entries.Distinct().Count() == 63,
            $"Entries={entries.Count}, UniqueIds={uniqueIds}, UniqueRefs={entries.Distinct().Count()}");

        bool exactGrid = true;
        bool deterministicOffers = true;
        int offerCount = 0;

        foreach (EmotionType emotion in Enum.GetValues(typeof(EmotionType)))
        {
            for (int tier = 1; tier <= 3; tier++)
            {
                List<EmotionAugmentDefinition> first = new();
                List<EmotionAugmentDefinition> second = new();
                bool a = catalog.TryBuildCanonicalOffer(emotion, tier, first, out _);
                bool b = catalog.TryBuildCanonicalOffer(emotion, tier, second, out _);

                exactGrid &= a && first.Count == 3;
                deterministicOffers &= b &&
                    first.Count == second.Count &&
                    first.SequenceEqual(second);
                offerCount += first.Count;
            }
        }

        Add(checks, "P10-C02", "7 emotions × 3 tiers × 3 cards",
            exactGrid && offerCount == 63,
            $"OfferSlots={offerCount}/63");

        Add(checks, "P10-C03", "Level offer is deterministic 3-choice, not weighted random",
            deterministicOffers,
            $"Deterministic={deterministicOffers}");

        RunFlowLiveContentRegistry registry =
            AssetDatabase.LoadAssetAtPath<RunFlowLiveContentRegistry>(
                Canonical0922Phase10EmotionAugmentMigration.RegistryPath);

        Add(checks, "P10-C04", "RunFlow live registry points at canonical catalog",
            registry != null && registry.EmotionAugmentCatalog == catalog,
            registry == null
                ? "Registry=MISSING"
                : $"Catalog={(registry.EmotionAugmentCatalog != null ? registry.EmotionAugmentCatalog.name : "NULL")}");
    }

    private static void AddProgressionChecks(List<Check> checks)
    {
        FervorRuleSettings settings = new();
        settings.Normalize();

        Add(checks, "P10-F01", "Fervor thresholds = 4 / 8 / 10, max level 3",
            settings.Level1Cost == 4 &&
            settings.Level2Cost == 8 &&
            settings.Level3Cost == 10 &&
            settings.MaximumLevel == 3,
            $"Costs={settings.Level1Cost}/{settings.Level2Cost}/{settings.Level3Cost}, Max={settings.MaximumLevel}");

        string systemSource = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Progression/EmotionAugmentSystem.cs");

        bool levelBinding =
            systemSource.Contains("fervor.LevelUp += OnFervorLevelUp", StringComparison.Ordinal) &&
            systemSource.Contains("int tier = Mathf.Clamp(levelUp.NewLevel, 1, 3)", StringComparison.Ordinal) &&
            systemSource.Contains("TryBuildCanonicalOffer", StringComparison.Ordinal) &&
            systemSource.Contains("queuedOffers.Enqueue", StringComparison.Ordinal);

        Add(checks, "P10-F02", "Fervor level-up binds tier and queues every offer",
            levelBinding,
            levelBinding ? "LevelUp -> Tier1/2/3 -> queued 3-choice" : "binding missing");
    }

    private static void AddInfiniteStatusChecks(List<Check> checks)
    {
        StatusEffect strength =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Strength, 1, StatusEffect.InfiniteDuration);
        StatusEffect swift =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Swift, 1, StatusEffect.InfiniteDuration);
        StatusEffect rupture =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Rupture, 3, StatusEffect.InfiniteDuration);

        bool created =
            strength is StrengthStatus && strength.IsInfiniteDuration && strength.NumericValue == 1 &&
            swift is SwiftStatus && swift.IsInfiniteDuration && swift.NumericValue == 1 &&
            rupture is RuptureStatus && rupture.IsInfiniteDuration && rupture.NumericValue == 3;

        Add(checks, "P10-S01", "Strength / Swift / Rupture support real ∞ common status entries",
            created,
            created ? "Strength1·∞ / Swift1·∞ / Rupture3·∞" : "factory mismatch");

        strength?.DecreaseDuration();
        swift?.DecreaseDuration();
        rupture?.DecreaseDuration();

        bool survives =
            strength?.Duration == StatusEffect.InfiniteDuration &&
            swift?.Duration == StatusEffect.InfiniteDuration &&
            rupture?.Duration == StatusEffect.InfiniteDuration;

        Add(checks, "P10-S02", "∞ entries survive natural duration decay",
            survives,
            $"Durations={strength?.Duration}/{swift?.Duration}/{rupture?.Duration}");
    }

    private static void AddCanonicalEffectChecks(List<Check> checks)
    {
        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                Canonical0922Phase10EmotionAugmentMigration.CatalogPath);
        if (catalog?.Entries == null)
            return;

        Dictionary<string, EmotionAugmentDefinition> byId =
            catalog.Entries
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.AugmentId))
                .ToDictionary(x => x.AugmentId, x => x, StringComparer.Ordinal);

        EmotionAugmentCanonical0922EffectDefinition grace = Effect(byId, "emotion.awe.t1.02");
        bool graceOk = grace != null &&
            grace.Kind == EmotionAugment0922EffectKind.TurnStartGuardStrength &&
            grace.PrimaryStatus == StatusEffectId.Strength &&
            grace.PrimaryValue == 1 &&
            grace.PrimaryDuration == StatusEffect.InfiniteDuration;
        Add(checks, "P10-S03", "Awe Grace owns only its Strength 1·∞ entry",
            graceOk,
            graceOk ? "TurnStart Strength1·∞ / first-loss removal" : "mapping missing");

        EmotionAugmentCanonical0922EffectDefinition granary = Effect(byId, "emotion.impression.t3.01");
        bool granaryOk = granary != null &&
            granary.Kind == EmotionAugment0922EffectKind.GoldThresholdStrength &&
            granary.GoldThreshold == 500 &&
            granary.PrimaryValue == 1;
        Add(checks, "P10-S04", "Impression Granary = conditional Strength 1·∞ at Gold500",
            granaryOk,
            granaryOk ? "Gold>=500 -> Strength1·∞" : "mapping missing");

        EmotionAugmentCanonical0922EffectDefinition rupture = Effect(byId, "emotion.admiration.t2.01");
        bool ruptureOk = rupture != null &&
            rupture.Kind == EmotionAugment0922EffectKind.PrestigeTargetRuptureInfinite &&
            rupture.PrimaryStatus == StatusEffectId.Rupture &&
            rupture.PrimaryValue == 3 &&
            rupture.PrimaryDuration == StatusEffect.InfiniteDuration;
        Add(checks, "P10-S05", "Admiration prestige card = independent Rupture 3·∞ per activation",
            ruptureOk,
            ruptureOk ? "Prestige -> Rupture3·∞" : "mapping missing");

        EmotionAugmentCanonical0922EffectDefinition conversion = Effect(byId, "emotion.admiration.t3.03");
        bool conversionOk = conversion != null &&
            conversion.Kind == EmotionAugment0922EffectKind.PermanentSelfStatus &&
            conversion.PrimaryStatus == StatusEffectId.Strength && conversion.PrimaryValue == 1 &&
            conversion.HasSecondaryStatus &&
            conversion.SecondaryStatus == StatusEffectId.Swift && conversion.SecondaryValue == 1 &&
            conversion.PrimaryDuration == StatusEffect.InfiniteDuration &&
            conversion.SecondaryDuration == StatusEffect.InfiniteDuration &&
            conversion.BlockPrestige;
        Add(checks, "P10-S06", "Admiration conversion = Strength1·∞ + Swift1·∞ and blocks Prestige",
            conversionOk,
            conversionOk ? "Strength1·∞ / Swift1·∞ / Prestige blocked" : "mapping missing");

        EmotionAugmentCanonical0922EffectDefinition hpRegen = Effect(byId, "emotion.compassion.t1.01");
        EmotionAugmentCanonical0922EffectDefinition staggerRegen = Effect(byId, "emotion.compassion.t1.03");
        bool distinct =
            hpRegen?.Kind == EmotionAugment0922EffectKind.TimedHpRecovery &&
            hpRegen.Turns == 3 && hpRegen.Amount == 8 &&
            staggerRegen?.Kind == EmotionAugment0922EffectKind.TimedStaggerRecovery &&
            staggerRegen.Turns == 3 && staggerRegen.Amount == 5;
        Add(checks, "P10-S07", "Compassion regeneration cards remain augment-owned effects, not common Regeneration",
            distinct,
            distinct ? "HP +8×3 / Stagger +5×3 custom mechanic" : "classification mismatch");

        bool pendingTimeSacrifice =
            byId.TryGetValue("emotion.awe.t3.01", out EmotionAugmentDefinition sacrifice) &&
            sacrifice != null &&
            sacrifice.RuntimeReadiness == EmotionAugmentRuntimeReadiness.DesignValuePending &&
            (sacrifice.Effects == null || sacrifice.Effects.Count == 0);
        Add(checks, "P10-S08", "Awe Time Sacrifice unknown Strength N is not invented",
            pendingTimeSacrifice,
            pendingTimeSacrifice ? "PENDING_CANONICAL / no TEMP proxy" : "unknown value was activated");
    }

    private static void AddConfirmedSemanticsCoverageChecks(
        List<Check> checks)
    {
        bool passed =
            Canonical0922EmotionConfirmedSemanticsVerification
                .EvaluateForGate(out string actual);

        Add(
            checks,
            "P10-S09",
            "All 50 confirmed emotion cards match canonical runtime semantics",
            passed,
            actual);
    }

    private static void AddCleanupChecks(List<Check> checks)
    {
        string source = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/EmotionAugmentCanonical0922EffectDefinition.cs");

        bool cleanup =
            source.Contains("battleEvent.OnBattleEnded += OnBattleEnded", StringComparison.Ordinal) &&
            source.Contains("private void OnBattleEnded()", StringComparison.Ordinal) &&
            source.Contains("RemoveOwnedStatuses();", StringComparison.Ordinal) &&
            source.Contains("public override void OnUnregister()", StringComparison.Ordinal);

        Add(checks, "P10-X01", "Battle-end cleanup owns/removes augment-created ∞ entries",
            cleanup,
            cleanup ? "OnBattleEnded + OnUnregister -> RemoveOwnedStatuses" : "cleanup path missing");
    }

    private static EmotionAugmentCanonical0922EffectDefinition Effect(
        IReadOnlyDictionary<string, EmotionAugmentDefinition> byId,
        string id)
    {
        if (byId == null || !byId.TryGetValue(id, out EmotionAugmentDefinition card) || card?.Effects == null)
            return null;

        return card.Effects
            .OfType<EmotionAugmentCanonical0922EffectDefinition>()
            .FirstOrDefault();
    }

    private static List<string> BuildPendingCanonical()
    {
        return new List<string>
        {
            "Faith T3 복구 — 방어도 소모 시 33% 복구는 0922에서도 (미정)",
            "Detachment T3 몸을 놓는다 — 최대체력 감소율 -10%는 (미정)",
            "Impression 카드의 Gold 보상/비용 N 대부분은 (미정)",
            "Impression T3 낙수/거둠의 실제 item/augment 확률·보상 세부는 일부 (미정)",
            "Awe T3 제물(시간) — Strength N의 N이 (미정); 무한 상태를 임의 생성하지 않음",
            "Longing T1 고조 충전 ±3은 0922 표에서 (미정) 표시를 유지",
            "Emotion augment의 미정 수치들은 TEMP proxy를 canonical 값으로 승격하지 않음"
        };
    }

    private static string ReadSource(string assetPath)
    {
        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string absolute = Path.Combine(root, assetPath);
        return File.Exists(absolute) ? File.ReadAllText(absolute) : string.Empty;
    }

    private static void Add(
        ICollection<Check> checks,
        string id,
        string name,
        bool passed,
        string actual)
    {
        checks.Add(new Check
        {
            Id = id,
            Name = name,
            Passed = passed,
            Actual = actual
        });
    }

    private static void WriteReport(
        IReadOnlyList<Check> checks,
        IReadOnlyList<string> pending,
        int pass,
        int fail,
        string result)
    {
        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string path = Path.Combine(root, ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);

        using StreamWriter writer = new(path, false);
        writer.WriteLine("# 0922 Phase 10 — Emotion Augment / Progression");
        writer.WriteLine();
        writer.WriteLine($"- PASS: {pass}");
        writer.WriteLine($"- FAIL: {fail}");
        writer.WriteLine($"- PENDING_CANONICAL: {pending.Count}");
        writer.WriteLine($"- RESULT: {result}");
        writer.WriteLine();
        writer.WriteLine("## Checks");
        writer.WriteLine();
        foreach (Check check in checks)
            writer.WriteLine($"- [{(check.Passed ? "x" : " ")}] `{check.Id}` {check.Name} — {check.Actual}");
        writer.WriteLine();
        writer.WriteLine("## PENDING_CANONICAL");
        writer.WriteLine();
        foreach (string item in pending)
            writer.WriteLine("- " + item);

        Debug.Log("[0922 Phase10] Report written: " + path);
    }
}
#endif