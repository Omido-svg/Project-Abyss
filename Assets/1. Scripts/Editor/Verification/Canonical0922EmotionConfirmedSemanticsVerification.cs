#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0923 verification coverage patch.
///
/// The 0922 canonical has exactly 50 emotion augment cards whose design status is
/// confirmed (확정). This verifier validates every one of those 50 cards by ID,
/// including the concrete runtime effect type and the canonical parameters that
/// give the effect its runtime meaning.
///
/// It intentionally does not promote or validate unresolved TEMP_BALANCE cards as
/// canonical. Pending design values remain outside this gate.
/// </summary>
public static class Canonical0922EmotionConfirmedSemanticsVerification
{
    private const string CatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    private const string ReportPath =
        "Logs/GameSystemVerification/0922_EmotionConfirmed50Semantics.md";

    private const float Epsilon = 0.0001f;

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    private sealed class CardSpec
    {
        public string Id;
        public string Label;
        public Func<EmotionAugmentDefinition, string> Validate;
    }

    private static readonly CardSpec[] Specs =
    {
        // -----------------------------------------------------------------
        // Compassion / 연민 — 9 confirmed
        // -----------------------------------------------------------------
        Old(
            "emotion.compassion.t1.01", "연민 T1 재생",
            EmotionAugment0922EffectKind.TimedHpRecovery,
            e => e.Turns == 3 && e.Amount == 8,
            "TimedHpRecovery / Turns=3 / Amount=8"),

        E33(
            "emotion.compassion.t1.02", "연민 T1 즉발 회복",
            Canonical0922Emotion33EffectKind.CompassionImmediateHeal,
            amount: 40),

        Old(
            "emotion.compassion.t1.03", "연민 T1 흐트러짐 재생",
            EmotionAugment0922EffectKind.TimedStaggerRecovery,
            e => e.Turns == 3 && e.Amount == 5,
            "TimedStaggerRecovery / Turns=3 / Amount=5"),

        E33(
            "emotion.compassion.t2.01", "연민 T2 교환 승리 회복",
            Canonical0922Emotion33EffectKind.CompassionExchangeWinRecovery,
            amount: 4, secondary: 2),

        E33(
            "emotion.compassion.t2.02", "연민 T2 흡혈",
            Canonical0922Emotion33EffectKind.CompassionLifesteal,
            ratio: .10f),

        E33(
            "emotion.compassion.t2.03", "연민 T2 열세 회복",
            Canonical0922Emotion33EffectKind.CompassionLastStandRecovery,
            amount: 12),

        E33(
            "emotion.compassion.t3.01", "연민 T3 전환",
            Canonical0922Emotion33EffectKind.CompassionHealingConversion),

        E33(
            "emotion.compassion.t3.02", "연민 T3 오버힐",
            Canonical0922Emotion33EffectKind.CompassionOverhealToBlock,
            maximumTotal: 50),

        Rule(
            "emotion.compassion.t3.03", "연민 T3 부위 재생",
            new RuleExpected(
                EmotionRulebreakerOperation.RegenerateBrokenPartAsWeakened,
                anyPart: true)),

        // -----------------------------------------------------------------
        // Faith / 신의 — 8 confirmed (T3 복구 is pending and excluded)
        // -----------------------------------------------------------------
        E33(
            "emotion.faith.t1.01", "신의 T1 기본",
            Canonical0922Emotion33EffectKind.FaithTurnStartBlock,
            amount: 3),

        E33(
            "emotion.faith.t1.02", "신의 T1 자세 대응",
            Canonical0922Emotion33EffectKind.FaithEnemyAttackSlotBlock,
            amount: 1),

        E33(
            "emotion.faith.t1.03", "신의 T1 반사",
            Canonical0922Emotion33EffectKind.FaithDamageReflection,
            ratio: .05f),

        E33(
            "emotion.faith.t2.01", "신의 T2 누적",
            Canonical0922Emotion33EffectKind.FaithEscalatingBlockGain,
            amount: 2),

        E33(
            "emotion.faith.t2.02", "신의 T2 완전 방어",
            Canonical0922Emotion33EffectKind.FaithFirstClashDamageNullify),

        E33(
            "emotion.faith.t2.03", "신의 T2 반사(상위)",
            Canonical0922Emotion33EffectKind.FaithDamageReflection,
            ratio: .12f),

        E33(
            "emotion.faith.t3.02", "신의 T3 회복 파생",
            Canonical0922Emotion33EffectKind.FaithTurnEndBlockToHeal),

        E33(
            "emotion.faith.t3.03", "신의 T3 전환",
            Canonical0922Emotion33EffectKind.FaithBlockConversion,
            multiplier: 3f),

        // -----------------------------------------------------------------
        // Detachment / 초연 — 8 confirmed (T3 몸을 놓는다 is pending)
        // -----------------------------------------------------------------
        E33(
            "emotion.detachment.t1.01", "초연 T1 체력 그릇",
            Canonical0922Emotion33EffectKind.DetachmentPartMaximumHp,
            ratio: .05f),

        E33(
            "emotion.detachment.t1.02", "초연 T1 자세 그릇",
            Canonical0922Emotion33EffectKind.DetachmentStaggerMaximum,
            amount: 50),

        E33(
            "emotion.detachment.t1.03", "초연 T1 약화 완화",
            Canonical0922Emotion33EffectKind.DetachmentSingleWeakenedPenaltySuppression),

        E33(
            "emotion.detachment.t2.01", "초연 T2 처치 성장",
            Canonical0922Emotion33EffectKind.DetachmentKillMaximumHpGrowth,
            amount: 2),

        E33(
            "emotion.detachment.t2.02", "초연 T2 나눠 받기",
            Canonical0922Emotion33EffectKind.DetachmentDamageDeferral),

        Rule(
            "emotion.detachment.t2.03", "초연 T2 슬롯 유지",
            new RuleExpected(
                EmotionRulebreakerOperation.PreserveNextBrokenPartSlots,
                amount: 1)),

        Rule(
            "emotion.detachment.t3.01", "초연 T3 평정",
            new RuleExpected(
                EmotionRulebreakerOperation.OverrideHpResistance,
                value: 1f)),

        Rule(
            "emotion.detachment.t3.02", "초연 T3 신의 육체",
            new RuleExpected(
                EmotionRulebreakerOperation.DisableStaggerGauge,
                booleanValue: true)),

        // -----------------------------------------------------------------
        // Impression / 감복 — 1 confirmed
        // -----------------------------------------------------------------
        Old(
            "emotion.impression.t3.01", "감복 T3 곳간",
            EmotionAugment0922EffectKind.GoldThresholdStrength,
            e =>
                e.PrimaryStatus == StatusEffectId.Strength &&
                e.PrimaryValue == 1 &&
                e.PrimaryDuration == StatusEffect.InfiniteDuration &&
                e.GoldThreshold == 500,
            "GoldThresholdStrength / Strength1·∞ / GoldThreshold=500"),

        // -----------------------------------------------------------------
        // Awe / 경외 — 8 confirmed (T3 제물(시간) is pending)
        // -----------------------------------------------------------------
        E33(
            "emotion.awe.t1.01", "경외 T1 강림",
            Canonical0922Emotion33EffectKind.AweFirstTurnStrength,
            amount: 2, duration: 1),

        Old(
            "emotion.awe.t1.02", "경외 T1 가호",
            EmotionAugment0922EffectKind.TurnStartGuardStrength,
            e =>
                e.PrimaryStatus == StatusEffectId.Strength &&
                e.PrimaryValue == 1 &&
                e.PrimaryDuration == StatusEffect.InfiniteDuration,
            "TurnStartGuardStrength / Strength1·∞"),

        E33(
            "emotion.awe.t1.03", "경외 T1 걸음",
            Canonical0922Emotion33EffectKind.AweNextTurnSwift,
            amount: 1, duration: 3),

        E33(
            "emotion.awe.t2.01", "경외 T2 머리 무한",
            Canonical0922Emotion33EffectKind.AweInfiniteHeadSpeed,
            amount: 1),

        E33(
            "emotion.awe.t2.02", "경외 T2 응답",
            Canonical0922Emotion33EffectKind.AweLastStandNextTurnStrength,
            amount: 2, duration: 1),

        E33(
            "emotion.awe.t2.03", "경외 T2 순풍",
            Canonical0922Emotion33EffectKind.AweOverwhelmNextTurnStrength,
            amount: 1, duration: 1),

        RuleMany(
            "emotion.awe.t3.02", "경외 T3 제물(공간)",
            new RuleExpected(
                EmotionRulebreakerOperation.AddPartSlots,
                targetPartType: PartType.HEAD,
                amount: 1),
            new RuleExpected(
                EmotionRulebreakerOperation.SuppressPartSlots,
                targetPartType: PartType.LEGS,
                booleanValue: true)),

        E33(
            "emotion.awe.t3.03", "경외 T3 제물(자원)",
            Canonical0922Emotion33EffectKind.AweInfiniteArmsSpeed,
            amount: 1),

        // -----------------------------------------------------------------
        // Admiration / 동경 — 9 confirmed
        // -----------------------------------------------------------------
        E33(
            "emotion.admiration.t1.01", "동경 T1 때림",
            Canonical0922Emotion33EffectKind.AdmirationHitPrestige,
            amount: 1),

        E33(
            "emotion.admiration.t1.02", "동경 T1 맞음",
            Canonical0922Emotion33EffectKind.AdmirationTakenPrestige,
            amount: 1),

        E33(
            "emotion.admiration.t1.03", "동경 T1 합",
            Canonical0922Emotion33EffectKind.AdmirationClashPrestige,
            amount: 2),

        Old(
            "emotion.admiration.t2.01", "동경 T2 균열",
            EmotionAugment0922EffectKind.PrestigeTargetRuptureInfinite,
            e =>
                e.PrimaryStatus == StatusEffectId.Rupture &&
                e.PrimaryValue == 3 &&
                e.PrimaryDuration == StatusEffect.InfiniteDuration,
            "PrestigeTargetRuptureInfinite / Rupture3·∞"),

        PrototypePrestige50(
            "emotion.admiration.t2.02", "동경 T2 즉시 풀 충전"),

        E33(
            "emotion.admiration.t2.03", "동경 T2 누적",
            Canonical0922Emotion33EffectKind.AdmirationElapsedClashPrestige,
            amount: 6),

        E33(
            "emotion.admiration.t3.01", "동경 T3 비축",
            Canonical0922Emotion33EffectKind.AdmirationPrestigeStockpile,
            amount: 2),

        E33(
            "emotion.admiration.t3.02", "동경 T3 환전",
            Canonical0922Emotion33EffectKind.AdmirationPrestigeFromStagger),

        Old(
            "emotion.admiration.t3.03", "동경 T3 전환",
            EmotionAugment0922EffectKind.PermanentSelfStatus,
            e =>
                e.PrimaryStatus == StatusEffectId.Strength &&
                e.PrimaryValue == 1 &&
                e.PrimaryDuration == StatusEffect.InfiniteDuration &&
                e.HasSecondaryStatus &&
                e.SecondaryStatus == StatusEffectId.Swift &&
                e.SecondaryValue == 1 &&
                e.SecondaryDuration == StatusEffect.InfiniteDuration &&
                e.BlockPrestige,
            "PermanentSelfStatus / Strength1·∞ / Swift1·∞ / BlockPrestige"),

        // -----------------------------------------------------------------
        // Longing / 그리움 — 7 confirmed (T1 몰이/버팀 are pending)
        // -----------------------------------------------------------------
        E33(
            "emotion.longing.t1.03", "그리움 T1 저울",
            Canonical0922Emotion33EffectKind.LongingNeutralThreshold,
            amount: 40),

        Rule(
            "emotion.longing.t2.01", "그리움 T2 유지",
            new RuleExpected(
                EmotionRulebreakerOperation.CarryPositiveMomentum,
                booleanValue: true)),

        Rule(
            "emotion.longing.t2.02", "그리움 T2 잔류",
            new RuleExpected(
                EmotionRulebreakerOperation.CarryNegativeMomentum,
                booleanValue: true)),

        E33(
            "emotion.longing.t2.03", "그리움 T2 호흡",
            Canonical0922Emotion33EffectKind.LongingNeutralTurnEndEnergy,
            amount: 1),

        Rule(
            "emotion.longing.t3.01", "그리움 T3 왕귀(짓누름)",
            new RuleExpected(
                EmotionRulebreakerOperation.ConfigureOverwhelmAscension,
                value: 2f,
                requiredConsecutiveTurns: 2)),

        Rule(
            "emotion.longing.t3.02", "그리움 T3 왕귀(짓눌림)",
            new RuleExpected(
                EmotionRulebreakerOperation.ConfigureLastStandRevive,
                amount: 1,
                value: .30f,
                requiredConsecutiveTurns: 2)),

        Rule(
            "emotion.longing.t3.03", "그리움 T3 왕귀(중립)",
            new RuleExpected(
                EmotionRulebreakerOperation.ConfigureBalanceResolutionRepeat,
                requiredConsecutiveTurns: 3,
                booleanValue: true))
    };

    [MenuItem(
        "Game System Verification/0922 Canonical/" +
        "Emotion 50 - Verify Confirmed Semantics")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = BuildChecks();
        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;
        string result =
            fail == 0
                ? "PASS_0922_EMOTION_CONFIRMED_50"
                : "FAIL";

        WriteReport(checks, pass, fail, result);

        StringBuilder summary = new StringBuilder();
        summary.AppendLine("[0922 Emotion 50 · Confirmed Runtime Semantics]");
        summary.AppendLine($"PASS={pass} FAIL={fail} ConfirmedCards={Specs.Length}");
        summary.AppendLine();
        summary.AppendLine("FAIL");

        foreach (Check check in checks.Where(x => !x.Passed))
        {
            summary.AppendLine(
                $"- {check.Id} | {check.Name} | {check.Actual}");
        }

        if (fail == 0)
            summary.AppendLine("- NONE");

        summary.AppendLine();
        summary.AppendLine("EMOTION50_RESULT=" + result);

        if (fail == 0)
            Debug.Log(summary.ToString());
        else
            Debug.LogError(summary.ToString());
    }

    /// <summary>
    /// Live, report-independent evaluation for Phase 10/13 gates.
    /// This deliberately rebuilds the checks from the current assets and current
    /// runtime source instead of trusting a previously generated markdown report.
    /// </summary>
    public static bool EvaluateForGate(out string actual)
    {
        List<Check> checks = BuildChecks();
        int fail = checks.Count(x => !x.Passed);
        int cardFail = checks.Count(x =>
            !x.Passed &&
            x.Id.StartsWith("E50-CARD-", StringComparison.Ordinal));
        int runtimeFail = checks.Count(x =>
            !x.Passed &&
            x.Id.StartsWith("E50-RUNTIME-", StringComparison.Ordinal));

        actual =
            $"ExpectedConfirmed=50, Checks={checks.Count}, Fail={fail}, " +
            $"CardMeaningFail={cardFail}, RuntimeContractFail={runtimeFail}";

        if (fail > 0)
        {
            string first = checks
                .Where(x => !x.Passed)
                .Take(4)
                .Select(x => x.Id + ":" + x.Actual)
                .Aggregate(
                    string.Empty,
                    (current, item) =>
                        string.IsNullOrEmpty(current)
                            ? item
                            : current + " | " + item);

            actual += "; First=" + first;
        }

        return fail == 0;
    }

    private static List<Check> BuildChecks()
    {
        List<Check> checks = new List<Check>();

        Add(
            checks,
            "E50-META-01",
            "Verifier contains exactly 50 unique confirmed card specs",
            Specs.Length == 50 &&
            Specs.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == 50,
            $"Specs={Specs.Length}, Unique={Specs.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count()}");

        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(CatalogPath);

        if (catalog?.Entries == null)
        {
            Add(
                checks,
                "E50-META-02",
                "EmotionAugmentCatalog is loadable",
                false,
                "Catalog missing");
            return checks;
        }

        List<EmotionAugmentDefinition> entries =
            catalog.Entries.Where(x => x != null).ToList();

        Dictionary<string, EmotionAugmentDefinition> byId =
            entries
                .Where(x => !string.IsNullOrWhiteSpace(x.AugmentId))
                .GroupBy(x => x.AugmentId, StringComparer.Ordinal)
                .ToDictionary(
                    x => x.Key,
                    x => x.First(),
                    StringComparer.Ordinal);

        List<EmotionAugmentDefinition> confirmed =
            entries
                .Where(x =>
                    string.Equals(
                        x.DesignStatus,
                        "확정",
                        StringComparison.Ordinal))
                .ToList();

        int confirmedUnique = confirmed
            .Where(x => !string.IsNullOrWhiteSpace(x.AugmentId))
            .Select(x => x.AugmentId)
            .Distinct(StringComparer.Ordinal)
            .Count();

        Add(
            checks,
            "E50-META-02",
            "Live catalog has exactly 50 confirmed cards",
            confirmed.Count == 50 && confirmedUnique == 50,
            $"Confirmed={confirmed.Count}, UniqueConfirmedIds={confirmedUnique}");

        int tempOnConfirmed = confirmed.Count(card =>
            card.Effects != null &&
            card.Effects.Any(effect =>
                effect is TempBalanceEmotionProxyEffectDefinition));

        Add(
            checks,
            "E50-META-03",
            "No confirmed card executes TEMP_BALANCE proxy",
            tempOnConfirmed == 0,
            $"ConfirmedTempProxy={tempOnConfirmed}");

        HashSet<string> expectedIds =
            new HashSet<string>(
                Specs.Select(x => x.Id),
                StringComparer.Ordinal);

        List<string> unexpectedConfirmed = confirmed
            .Select(x => x.AugmentId)
            .Where(id =>
                !string.IsNullOrWhiteSpace(id) &&
                !expectedIds.Contains(id))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        List<string> missingExpected = Specs
            .Where(x => !byId.ContainsKey(x.Id))
            .Select(x => x.Id)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        bool exactIdSet =
            unexpectedConfirmed.Count == 0 &&
            missingExpected.Count == 0;

        Add(
            checks,
            "E50-META-04",
            "Confirmed card ID set matches the 0922 50-card specification",
            exactIdSet,
            exactIdSet
                ? "Expected 50 IDs present"
                : "Missing=" + string.Join(", ", missingExpected) +
                  "; Unexpected=" + string.Join(", ", unexpectedConfirmed));

        for (int i = 0; i < Specs.Length; i++)
        {
            CardSpec spec = Specs[i];
            string id = $"E50-CARD-{i + 1:00}";

            if (!byId.TryGetValue(spec.Id, out EmotionAugmentDefinition card) ||
                card == null)
            {
                Add(checks, id, spec.Label, false, spec.Id + " missing");
                continue;
            }

            if (!string.Equals(card.DesignStatus, "확정", StringComparison.Ordinal))
            {
                Add(
                    checks,
                    id,
                    spec.Label,
                    false,
                    $"DesignStatus={card.DesignStatus}");
                continue;
            }

            if (card.RuntimeReadiness !=
                EmotionAugmentRuntimeReadiness.RuntimeConnected)
            {
                Add(
                    checks,
                    id,
                    spec.Label,
                    false,
                    $"RuntimeReadiness={card.RuntimeReadiness}");
                continue;
            }

            string error = spec.Validate?.Invoke(card);
            Add(
                checks,
                id,
                spec.Label,
                string.IsNullOrEmpty(error),
                string.IsNullOrEmpty(error)
                    ? spec.Id + " semantic mapping PASS"
                    : spec.Id + " | " + error);
        }

        AddRuntimeContractChecks(checks);
        return checks;
    }

    private static void AddRuntimeContractChecks(ICollection<Check> checks)
    {
        string emotion33 = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/Canonical0922Emotion33EffectDefinition.cs");

        string[] kindNames =
            Enum.GetNames(typeof(Canonical0922Emotion33EffectKind));

        List<string> missingKinds = kindNames
            .Where(name => CountOccurrences(emotion33, name) < 2)
            .ToList();

        Add(
            checks,
            "E50-RUNTIME-01",
            "All Emotion33 canonical kinds have runtime mechanic handling",
            missingKinds.Count == 0,
            missingKinds.Count == 0
                ? $"{kindNames.Length}/{kindNames.Length} unique kind names occur in enum + runtime mechanic"
                : "MissingRuntimeKinds=" + string.Join(", ", missingKinds));

        string hooks = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/Canonical0922EmotionRuntimeHooks.cs");
        string character = ReadSource(
            "Assets/1. Scripts/Runtime/Characters/CharacterBase/Character.cs");
        string damage = ReadSource(
            "Assets/1. Scripts/Runtime/Battle/Damage/DamagePipeline.cs");
        string clash = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Resolution/ClashPowerPipeline.cs");
        string momentum = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Stats/MomentumManager.cs");
        string skillCost = ReadSource(
            "Assets/1. Scripts/Runtime/Skills/Base/SkillCostService.cs");
        string estimator = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/AI/PlayerAutoPlanEstimator.cs");
        string stagger = ReadSource(
            "Assets/1. Scripts/Runtime/Characters/CharacterBase/StaggerGaugeMechanic.cs");
        string bodyPart = ReadSource(
            "Assets/1. Scripts/Runtime/Characters/BodyParts/BodyPart.cs");
        string runProgression = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Progression/RunProgressionSystem.cs");

        bool commonHooks =
            hooks.Contains("ModifyHealing", StringComparison.Ordinal) &&
            hooks.Contains("ModifyBlockGain", StringComparison.Ordinal) &&
            hooks.Contains("ModifyDamageBeforeGuard", StringComparison.Ordinal) &&
            hooks.Contains("ModifyDamageAfterGuard", StringComparison.Ordinal) &&
            hooks.Contains("TryResolveClashSpeedModifierOverride", StringComparison.Ordinal) &&
            hooks.Contains("SuppressSingleWeakenedPenalty", StringComparison.Ordinal) &&
            hooks.Contains("ResolveMomentumAdvantageThreshold", StringComparison.Ordinal) &&
            hooks.Contains("TryGetPrestigeStockpileThreshold", StringComparison.Ordinal);

        Add(
            checks,
            "E50-RUNTIME-02",
            "Emotion33 common hook surface is complete",
            commonHooks,
            commonHooks ? "8/8 hook families present" : "one or more hook families missing");

        bool coreIntegration =
            character.Contains("Canonical0922EmotionRuntimeHooks.ModifyHealing", StringComparison.Ordinal) &&
            character.Contains("Canonical0922EmotionRuntimeHooks.ModifyBlockGain", StringComparison.Ordinal) &&
            damage.Contains("Canonical0922EmotionRuntimeHooks.ModifyDamageBeforeGuard", StringComparison.Ordinal) &&
            damage.Contains("Canonical0922EmotionRuntimeHooks.ModifyDamageAfterGuard", StringComparison.Ordinal) &&
            clash.Contains("TryResolveClashSpeedModifierOverride", StringComparison.Ordinal) &&
            momentum.Contains("ResolveMomentumAdvantageThreshold", StringComparison.Ordinal) &&
            skillCost.Contains("TryGetPrestigeStockpileThreshold", StringComparison.Ordinal) &&
            estimator.Contains("TryResolveClashSpeedModifierOverride", StringComparison.Ordinal);

        Add(
            checks,
            "E50-RUNTIME-03",
            "Core battle paths consume the canonical emotion hooks",
            coreIntegration,
            coreIntegration
                ? "healing/block/damage/speed/momentum/prestige/autoplan wired"
                : "one or more core integrations missing");

        bool supportApis =
            character.Contains("AdjustEnergyMaximum", StringComparison.Ordinal) &&
            runProgression.Contains("AddMaximumHpBonus", StringComparison.Ordinal) &&
            stagger.Contains("AdjustMaximum", StringComparison.Ordinal) &&
            stagger.Contains("SacrificeGauge", StringComparison.Ordinal) &&
            bodyPart.Contains("IncreaseMaxHPPercent", StringComparison.Ordinal);

        Add(
            checks,
            "E50-RUNTIME-04",
            "Capacity/deferral support APIs required by confirmed cards exist",
            supportApis,
            supportApis
                ? "energy/maxHP/stagger/partHP support APIs present"
                : "one or more support APIs missing");

        string rulebreaker = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Progression/EmotionRulebreakerEffectDefinition.cs");

        EmotionRulebreakerOperation[] requiredOperations =
        {
            EmotionRulebreakerOperation.PreserveNextBrokenPartSlots,
            EmotionRulebreakerOperation.AddPartSlots,
            EmotionRulebreakerOperation.SuppressPartSlots,
            EmotionRulebreakerOperation.RegenerateBrokenPartAsWeakened,
            EmotionRulebreakerOperation.OverrideHpResistance,
            EmotionRulebreakerOperation.DisableStaggerGauge,
            EmotionRulebreakerOperation.CarryPositiveMomentum,
            EmotionRulebreakerOperation.CarryNegativeMomentum,
            EmotionRulebreakerOperation.ConfigureLastStandRevive,
            EmotionRulebreakerOperation.ConfigureBalanceResolutionRepeat,
            EmotionRulebreakerOperation.ConfigureOverwhelmAscension
        };

        List<string> missingOperations = requiredOperations
            .Where(operation =>
                !rulebreaker.Contains(
                    "case EmotionRulebreakerOperation." + operation,
                    StringComparison.Ordinal))
            .Select(x => x.ToString())
            .ToList();

        Add(
            checks,
            "E50-RUNTIME-05",
            "Confirmed rulebreaker operations have runtime dispatch",
            missingOperations.Count == 0,
            missingOperations.Count == 0
                ? "11/11 rulebreaker operations dispatched"
                : "Missing=" + string.Join(", ", missingOperations));

        string prototype = ReadSource(
            "Assets/1. Scripts/Runtime/Systems/Progression/EmotionAugmentPrototypeEffectDefinition.cs");

        bool prestige50Runtime =
            prototype.Contains(
                "if (GainPrestige > 0) owner.AddPrestige(GainPrestige);",
                StringComparison.Ordinal);

        Add(
            checks,
            "E50-RUNTIME-06",
            "Admiration instant full-charge prototype has live prestige application",
            prestige50Runtime,
            prestige50Runtime
                ? "GainPrestige -> owner.AddPrestige"
                : "prestige application path missing");
    }

    private sealed class RuleExpected
    {
        public EmotionRulebreakerOperation Operation;
        public PartType TargetPartType;
        public bool AnyPart;
        public int Amount;
        public float Value;
        public int RequiredConsecutiveTurns;
        public bool BooleanValue;

        public RuleExpected(
            EmotionRulebreakerOperation operation,
            PartType targetPartType = PartType.HEAD,
            bool anyPart = false,
            int amount = 1,
            float value = 1f,
            int requiredConsecutiveTurns = 1,
            bool booleanValue = true)
        {
            Operation = operation;
            TargetPartType = targetPartType;
            AnyPart = anyPart;
            Amount = amount;
            Value = value;
            RequiredConsecutiveTurns = requiredConsecutiveTurns;
            BooleanValue = booleanValue;
        }
    }

    private sealed class RuleSnapshot
    {
        public EmotionRulebreakerOperation Operation;
        public PartType TargetPartType;
        public bool AnyPart;
        public int Amount;
        public float Value;
        public int RequiredConsecutiveTurns;
        public bool BooleanValue;
        public string AssetPath;
    }

    private static CardSpec E33(
        string id,
        string label,
        Canonical0922Emotion33EffectKind kind,
        int amount = 0,
        int secondary = 0,
        int duration = 1,
        int maximumTotal = 0,
        float ratio = 0f,
        float multiplier = 1f)
    {
        return new CardSpec
        {
            Id = id,
            Label = label,
            Validate = card =>
            {
                if (card.Effects == null || card.Effects.Count != 1)
                    return $"Effects.Count={card.Effects?.Count ?? 0}, expected 1";

                if (card.Effects[0] is not Canonical0922Emotion33EffectDefinition effect)
                {
                    return "EffectType=" +
                        (card.Effects[0] != null
                            ? card.Effects[0].GetType().Name
                            : "null") +
                        ", expected Canonical0922Emotion33EffectDefinition";
                }

                bool values =
                    effect.Kind == kind &&
                    effect.Amount == amount &&
                    effect.SecondaryAmount == secondary &&
                    effect.Duration == duration &&
                    effect.MaximumTotal == maximumTotal &&
                    Approximately(effect.Ratio, ratio) &&
                    Approximately(effect.Multiplier, multiplier);

                return values
                    ? null
                    : $"Kind={effect.Kind}, Amount={effect.Amount}, Secondary={effect.SecondaryAmount}, " +
                      $"Duration={effect.Duration}, Max={effect.MaximumTotal}, Ratio={effect.Ratio:0.###}, " +
                      $"Multiplier={effect.Multiplier:0.###}; expected {kind}/{amount}/{secondary}/" +
                      $"{duration}/{maximumTotal}/{ratio:0.###}/{multiplier:0.###}";
            }
        };
    }

    private static CardSpec Old(
        string id,
        string label,
        EmotionAugment0922EffectKind kind,
        Func<EmotionAugmentCanonical0922EffectDefinition, bool> predicate,
        string expectation)
    {
        return new CardSpec
        {
            Id = id,
            Label = label,
            Validate = card =>
            {
                if (card.Effects == null || card.Effects.Count != 1)
                    return $"Effects.Count={card.Effects?.Count ?? 0}, expected 1";

                if (card.Effects[0] is not EmotionAugmentCanonical0922EffectDefinition effect)
                {
                    return "EffectType=" +
                        (card.Effects[0] != null
                            ? card.Effects[0].GetType().Name
                            : "null") +
                        ", expected EmotionAugmentCanonical0922EffectDefinition";
                }

                if (effect.Kind != kind || (predicate != null && !predicate(effect)))
                    return $"Kind={effect.Kind}; expected {expectation}";

                return null;
            }
        };
    }

    private static CardSpec Rule(
        string id,
        string label,
        RuleExpected expected)
    {
        return RuleMany(id, label, expected);
    }

    private static CardSpec RuleMany(
        string id,
        string label,
        params RuleExpected[] expected)
    {
        return new CardSpec
        {
            Id = id,
            Label = label,
            Validate = card =>
            {
                if (card.Effects == null || card.Effects.Count != expected.Length)
                {
                    return $"Effects.Count={card.Effects?.Count ?? 0}, expected {expected.Length}";
                }

                // Unity 6 can preserve ScriptableObject identity through
                // m_EditorClassIdentifier even when a legacy-authored asset does
                // not behave reliably with LINQ OfType<T>() in an Editor gate.
                // Validate the actual serialized runtime contract instead of
                // treating an OfType miss as a semantic failure.
                List<RuleSnapshot> actual =
                    new List<RuleSnapshot>();

                for (int index = 0; index < card.Effects.Count; index++)
                {
                    EmotionAugmentEffectDefinition effect =
                        card.Effects[index];

                    if (!TryReadRulebreakerSnapshot(
                            effect,
                            out RuleSnapshot snapshot,
                            out string reason))
                    {
                        return $"Effect[{index}] {reason}";
                    }

                    actual.Add(snapshot);
                }

                foreach (RuleExpected item in expected)
                {
                    RuleSnapshot match =
                        actual.FirstOrDefault(
                            x => x.Operation == item.Operation);

                    if (match == null)
                    {
                        return "MissingOperation=" + item.Operation +
                            "; Actual=" +
                            string.Join(
                                ",",
                                actual.Select(x => x.Operation.ToString()));
                    }

                    if (match.TargetPartType != item.TargetPartType)
                        return $"{item.Operation}.TargetPartType={match.TargetPartType}, expected {item.TargetPartType}";

                    if (match.AnyPart != item.AnyPart)
                        return $"{item.Operation}.AnyPart={match.AnyPart}, expected {item.AnyPart}";

                    if (match.Amount != item.Amount)
                        return $"{item.Operation}.Amount={match.Amount}, expected {item.Amount}";

                    if (!Approximately(match.Value, item.Value))
                        return $"{item.Operation}.Value={match.Value}, expected {item.Value}";

                    if (match.RequiredConsecutiveTurns != item.RequiredConsecutiveTurns)
                    {
                        return $"{item.Operation}.RequiredConsecutiveTurns={match.RequiredConsecutiveTurns}, " +
                            $"expected {item.RequiredConsecutiveTurns}";
                    }

                    if (match.BooleanValue != item.BooleanValue)
                        return $"{item.Operation}.BooleanValue={match.BooleanValue}, expected {item.BooleanValue}";
                }

                return null;
            }
        };
    }

    private static bool TryReadRulebreakerSnapshot(
        EmotionAugmentEffectDefinition effect,
        out RuleSnapshot snapshot,
        out string reason)
    {
        snapshot = null;
        reason = string.Empty;

        if (effect == null)
        {
            reason = "Reference=null (expected EmotionRulebreakerEffectDefinition)";
            return false;
        }

        SerializedObject serialized =
            new SerializedObject(effect);
        serialized.UpdateIfRequiredOrScript();

        SerializedProperty classIdentifier =
            serialized.FindProperty("m_EditorClassIdentifier");

        string classId =
            classIdentifier != null
                ? classIdentifier.stringValue
                : string.Empty;

        bool runtimeTypeMatches =
            string.Equals(
                effect.GetType().Name,
                nameof(EmotionRulebreakerEffectDefinition),
                StringComparison.Ordinal);

        bool serializedTypeMatches =
            !string.IsNullOrEmpty(classId) &&
            classId.EndsWith(
                "::" + nameof(EmotionRulebreakerEffectDefinition),
                StringComparison.Ordinal);

        if (!runtimeTypeMatches && !serializedTypeMatches)
        {
            reason =
                $"Type={effect.GetType().FullName}, ClassId={classId}, " +
                "expected EmotionRulebreakerEffectDefinition";
            return false;
        }

        SerializedProperty operation = serialized.FindProperty("Operation");
        SerializedProperty targetPartType = serialized.FindProperty("TargetPartType");
        SerializedProperty anyPart = serialized.FindProperty("AnyPart");
        SerializedProperty amount = serialized.FindProperty("Amount");
        SerializedProperty value = serialized.FindProperty("Value");
        SerializedProperty required = serialized.FindProperty("RequiredConsecutiveTurns");
        SerializedProperty booleanValue = serialized.FindProperty("BooleanValue");

        if (operation == null ||
            targetPartType == null ||
            anyPart == null ||
            amount == null ||
            value == null ||
            required == null ||
            booleanValue == null)
        {
            reason =
                "Serialized rulebreaker fields are incomplete at " +
                AssetDatabase.GetAssetPath(effect);
            return false;
        }

        snapshot =
            new RuleSnapshot
            {
                Operation =
                    (EmotionRulebreakerOperation)operation.intValue,
                TargetPartType =
                    (PartType)targetPartType.intValue,
                AnyPart = anyPart.boolValue,
                Amount = amount.intValue,
                Value = value.floatValue,
                RequiredConsecutiveTurns = required.intValue,
                BooleanValue = booleanValue.boolValue,
                AssetPath = AssetDatabase.GetAssetPath(effect)
            };

        return true;
    }

    private static CardSpec PrototypePrestige50(
        string id,
        string label)
    {
        return new CardSpec
        {
            Id = id,
            Label = label,
            Validate = card =>
            {
                if (card.Effects == null || card.Effects.Count != 1)
                    return $"Effects.Count={card.Effects?.Count ?? 0}, expected 1";

                if (card.Effects[0] is not EmotionAugmentPrototypeEffectDefinition effect)
                {
                    return "EffectType=" +
                        (card.Effects[0] != null
                            ? card.Effects[0].GetType().Name
                            : "null") +
                        ", expected EmotionAugmentPrototypeEffectDefinition";
                }

                bool exact =
                    effect.GainPrestige == 50 &&
                    effect.GainEnergy == 0 &&
                    Approximately(effect.RecoverStaggerRatio, 0f) &&
                    effect.RollBonus == 0 &&
                    Approximately(effect.DamageDealtMultiplier, 1f) &&
                    Approximately(effect.DamageTakenMultiplier, 1f) &&
                    effect.PositiveMomentumBonus == 0 &&
                    Approximately(effect.PrestigeGainMultiplier, 1f);

                return exact
                    ? null
                    : $"GainPrestige={effect.GainPrestige}; expected pure GainPrestige=50";
            }
        };
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= Epsilon;
    }

    private static int CountOccurrences(string source, string token)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(token))
            return 0;

        int count = 0;
        int index = 0;

        while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string ReadSource(string assetPath)
    {
        string root =
            Directory.GetParent(Application.dataPath)?.FullName ??
            Directory.GetCurrentDirectory();
        string absolute = Path.GetFullPath(Path.Combine(root, assetPath));
        return File.Exists(absolute) ? File.ReadAllText(absolute) : string.Empty;
    }

    private static void Add(
        ICollection<Check> checks,
        string id,
        string name,
        bool passed,
        string actual)
    {
        checks.Add(
            new Check
            {
                Id = id,
                Name = name,
                Passed = passed,
                Actual = actual ?? string.Empty
            });
    }

    private static void WriteReport(
        IReadOnlyList<Check> checks,
        int pass,
        int fail,
        string result)
    {
        string root =
            Directory.GetParent(Application.dataPath)?.FullName ??
            Directory.GetCurrentDirectory();
        string path = Path.GetFullPath(Path.Combine(root, ReportPath));
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        StringBuilder writer = new StringBuilder();
        writer.AppendLine("# 0922 Emotion Augment — 50 Confirmed Runtime Semantics");
        writer.AppendLine();
        writer.AppendLine($"- PASS: {pass}");
        writer.AppendLine($"- FAIL: {fail}");
        writer.AppendLine("- CONFIRMED_CARD_SPECS: 50");
        writer.AppendLine($"- RESULT: {result}");
        writer.AppendLine();

        foreach (Check check in checks)
        {
            writer.AppendLine(
                $"- [{(check.Passed ? "x" : " ")}] `{check.Id}` " +
                $"{check.Name} — {check.Actual}");
        }

        writer.AppendLine();
        writer.AppendLine("EMOTION50_RESULT=" + result);

        File.WriteAllText(
            path,
            writer.ToString(),
            new UTF8Encoding(false));

        Debug.Log("[0922 Emotion50] Report written: " + path);
    }
}
#endif
