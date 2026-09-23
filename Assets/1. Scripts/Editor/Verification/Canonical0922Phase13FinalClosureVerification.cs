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
/// [0922_PHASE13_FINAL_CLOSURE_GATE]
///
/// Final closure gate for the confirmed 0922 canonical.
///
/// This verifier does not add gameplay behavior. It consumes the Phase 1~12
/// verification artifacts and performs final live project sanity checks:
/// - canonical identity / explicit pending inventory
/// - status / calculation / timing / character / encounter / progression / HUD
/// - required assets and catalog wiring
/// - missing scripts in Assets scenes/prefabs
/// - Phase 12 full regression closure
///
/// PENDING_CANONICAL is reported separately and is not treated as a patch gap.
/// </summary>
public static class Canonical0922Phase13FinalClosureVerification
{
    private const string ReportPath =
        "Logs/GameSystemVerification/0922_Phase13_FinalClosure.md";

    private const string RegistryPath =
        "Assets/Resources/ProjectAbyss/RunFlowLiveContentRegistry.asset";

    private const string EmotionCatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    private const string RunItemCatalogPath =
        "Assets/2. Data/Progression/RunItems/RunItemCatalog.asset";

    private const string EnemyCatalogPath =
        "Assets/Resources/Canonical0922/EnemyEncounterCatalog.asset";

    private const string RunScenePath =
        "Assets/5. Scenes/Run Flow Test.unity";

    private const string BattleScenePath =
        "Assets/5. Scenes/Battle Test Scene.unity";

    private sealed class Check
    {
        public string Gate;
        public string Id;
        public string Name;
        public string Metric;
        public bool Passed;
        public string Actual;
    }

    private sealed class ReportRequirement
    {
        public string Phase;
        public string Path;
        public string Token;

        public ReportRequirement(
            string phase,
            string path,
            string token)
        {
            Phase = phase;
            Path = path;
            Token = token;
        }
    }

    private sealed class ReportState
    {
        public ReportRequirement Requirement;
        public bool Exists;
        public bool Passed;
        public string Text;
    }

    private static readonly ReportRequirement[] PhaseReports =
    {
        new ReportRequirement(
            "Phase 1",
            "Logs/GameSystemVerification/0922_Phase1_Baseline.md",
            "PHASE1_RESULT=PASS_BASELINE_CAPTURED"),

        new ReportRequirement(
            "Phase 2",
            "Logs/GameSystemVerification/0922_Phase2_CoreStatusStorage.md",
            "PHASE2_RESULT=PASS_CORE_STORAGE"),

        new ReportRequirement(
            "Phase 3",
            "Logs/GameSystemVerification/0922_Phase3_CalculationTurnEnd.md",
            "PHASE3_RESULT=PASS_CALCULATION_TURNEND"),

        new ReportRequirement(
            "Phase 4",
            "Logs/GameSystemVerification/0922_Phase4_TimingDeferred.md",
            "PHASE4_RESULT=PASS_TIMING_DEFERRED"),

        new ReportRequirement(
            "Phase 5",
            "Logs/GameSystemVerification/0922_Phase5_FactoryAuthoring.md",
            "PHASE5_RESULT=PASS_FACTORY_AUTHORING"),

        new ReportRequirement(
            "Phase 6",
            "Logs/GameSystemVerification/0922_Phase6_CharacterKeywords.md",
            "PHASE6_RESULT=PASS_CHARACTER_KEYWORDS"),

        new ReportRequirement(
            "Phase 7",
            "Logs/GameSystemVerification/0922_Phase7_OlafYujinSkillMigration.md",
            "PHASE7_RESULT=PASS_OLAF_YUJIN_SKILLS"),

        new ReportRequirement(
            "Phase 8",
            "Logs/GameSystemVerification/0922_Phase8_Hifumi.md",
            "PHASE8_RESULT=PASS_HIFUMI_0922"),

        new ReportRequirement(
            "Phase 9",
            "Logs/GameSystemVerification/0922_Phase9_EnemyEncounter.md",
            "PHASE9_RESULT=PASS_ENEMY_ENCOUNTER_0922"),

        new ReportRequirement(
            "Phase 10",
            "Logs/GameSystemVerification/0922_Phase10_EmotionAugment.md",
            "PHASE10_RESULT=PASS_EMOTION_AUGMENT_0922"),

        new ReportRequirement(
            "Phase 11",
            "Logs/GameSystemVerification/0922_Phase11_HudPresentation.md",
            "PHASE11_RESULT=PASS_HUD_PRESENTATION_0922"),

        new ReportRequirement(
            "Phase 12",
            "Logs/GameSystemVerification/0922_Phase12_CleanupRegression.md",
            "PHASE12_RESULT=PASS_CLEANUP_REGRESSION_0922")
    };

    private static readonly string[] MetricOrder =
    {
        "ConfirmedCanonicalMismatch",
        "RuntimeModelMismatch",
        "CalculationMismatch",
        "TimingMismatch",
        "DataMismatch",
        "CharacterMismatch",
        "EncounterMismatch",
        "ProgressionMismatch",
        "HudMismatch",
        "VerificationFailure",
        "LegacyLeak"
    };

    [MenuItem(
        "Game System Verification/0922 Canonical/" +
        "Phase 13 - Verify Final Closure Gate")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new List<Check>();
        Dictionary<string, ReportState> reports =
            LoadPhaseReports();

        AddGateACanonical(checks, reports);
        AddGateBStatusRuntime(checks, reports);
        AddGateCCalculation(checks, reports);
        AddGateDCharacter(checks, reports);
        AddGateEEncounterProgression(checks, reports);
        AddGateFHud(checks, reports);
        AddGateGUnity(checks, reports);

        Dictionary<string, int> metrics =
            BuildMetricCounts(checks);

        int unauthorizedHardcodedPending =
            checks.Count(x =>
                x.Metric == "UnauthorizedHardcodedPending" &&
                !x.Passed);

        int confirmedRequiredPatchCount =
            MetricOrder.Sum(metric =>
                metrics.TryGetValue(metric, out int value)
                    ? value
                    : 0);

        int pass =
            checks.Count(x => x.Passed);

        int fail =
            checks.Count - pass;

        int pendingCanonical =
            Canonical0922BaselineSpec.PendingCanonicalDomains?.Count ?? 0;

        bool closed =
            confirmedRequiredPatchCount == 0 &&
            unauthorizedHardcodedPending == 0 &&
            fail == 0;

        string result =
            closed
                ? "PASS_FINAL_CLOSURE_0922"
                : "FAIL";

        WriteReport(
            checks,
            reports,
            metrics,
            unauthorizedHardcodedPending,
            confirmedRequiredPatchCount,
            pendingCanonical,
            pass,
            fail,
            result);

        string summary =
            "[0922 Phase 13 · Final Closure Gate]\n" +
            $"PASS={pass} FAIL={fail} " +
            $"PENDING_CANONICAL={pendingCanonical}\n" +
            $"ConfirmedRequiredPatchCount={confirmedRequiredPatchCount}\n" +
            $"UnauthorizedHardcodedPending={unauthorizedHardcodedPending}\n\n" +
            BuildMetricSummary(
                metrics,
                unauthorizedHardcodedPending) +
            "\n\nPASS\n- " +
            string.Join(
                "\n- ",
                checks
                    .Where(x => x.Passed)
                    .Select(x =>
                        $"{x.Id} | {x.Name} | {x.Actual}")) +
            "\n\nFAIL\n- " +
            (fail == 0
                ? "NONE"
                : string.Join(
                    "\n- ",
                    checks
                        .Where(x => !x.Passed)
                        .Select(x =>
                            $"{x.Id} | {x.Name} | {x.Actual}"))) +
            "\n\nPHASE13_RESULT=" +
            result;

        if (closed)
            Debug.Log(summary);
        else
            Debug.LogError(summary);
    }

    [MenuItem(
        "Game System Verification/0922 Canonical/" +
        "Phase 13 - Open Final Closure Report")]
    public static void OpenReportFromMenu()
    {
        string fullPath =
            ToProjectAbsolutePath(ReportPath);

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning(
                "[0922 Phase13] Final Closure report가 없습니다. " +
                "먼저 Phase 13 - Verify Final Closure Gate를 실행하세요.");
            return;
        }

        EditorUtility.RevealInFinder(fullPath);
    }

    // ---------------------------------------------------------------------
    // Gate A — Canonical
    // ---------------------------------------------------------------------

    private static void AddGateACanonical(
        ICollection<Check> checks,
        IReadOnlyDictionary<string, ReportState> reports)
    {
        Add(
            checks,
            "A",
            "P13-A01",
            "Canonical identity is 0922",
            "ConfirmedCanonicalMismatch",
            Canonical0922BaselineSpec.CanonicalVersion == "0922",
            $"Canonical={Canonical0922BaselineSpec.CanonicalVersion}");

        bool statusModelLocked =
            Canonical0922BaselineSpec.NumericStatusesUseIndependentEntries &&
            Canonical0922BaselineSpec.NumericStatusesAllowInfiniteDuration &&
            Canonical0922BaselineSpec.PresenceStatusesUseMaxDurationRefresh &&
            !Canonical0922BaselineSpec.CommonStatusCapsEnabled &&
            !Canonical0922BaselineSpec.SealIsCommonStatus;

        Add(
            checks,
            "A",
            "P13-A02",
            "Phase 0 canonical status contract remains locked",
            "ConfirmedCanonicalMismatch",
            statusModelLocked,
            "NumericIndependent=" +
            Canonical0922BaselineSpec.NumericStatusesUseIndependentEntries +
            ", Infinite=" +
            Canonical0922BaselineSpec.NumericStatusesAllowInfiniteDuration +
            ", PresenceMaxRefresh=" +
            Canonical0922BaselineSpec.PresenceStatusesUseMaxDurationRefresh +
            ", CommonCap=" +
            Canonical0922BaselineSpec.CommonStatusCapsEnabled +
            ", SealCommon=" +
            Canonical0922BaselineSpec.SealIsCommonStatus);

        IReadOnlyList<string> pendingDomains =
            Canonical0922BaselineSpec.PendingCanonicalDomains;

        int pendingCount =
            pendingDomains?.Count ?? 0;

        Add(
            checks,
            "A",
            "P13-A03",
            "Unresolved canonical domains remain explicit",
            "UnauthorizedHardcodedPending",
            pendingDomains != null,
            $"PENDING_CANONICAL={pendingCount}");

        bool contentPendingProtected =
            ReportPassed(reports, "Phase 7") &&
            ReportPassed(reports, "Phase 8") &&
            ReportPassed(reports, "Phase 9") &&
            ReportPassed(reports, "Phase 10");

        Add(
            checks,
            "A",
            "P13-A04",
            "Confirmed content closed without inventing pending values",
            "UnauthorizedHardcodedPending",
            contentPendingProtected,
            "Phase7/8/9/10 canonical content gates=" +
            (contentPendingProtected ? "PASS" : "INCOMPLETE"));
    }

    // ---------------------------------------------------------------------
    // Gate B — Status Runtime
    // ---------------------------------------------------------------------

    private static void AddGateBStatusRuntime(
        ICollection<Check> checks,
        IReadOnlyDictionary<string, ReportState> reports)
    {
        Add(
            checks,
            "B",
            "P13-B01",
            "Numeric independent entry runtime closed",
            "RuntimeModelMismatch",
            ReportPassed(reports, "Phase 2") &&
            Phase12CheckPassed(reports, "P12-E01"),
            "Phase2=" + BoolText(ReportPassed(reports, "Phase 2")) +
            ", P12-E01=" +
            BoolText(Phase12CheckPassed(reports, "P12-E01")));

        Add(
            checks,
            "B",
            "P13-B02",
            "Presence max-duration refresh runtime closed",
            "RuntimeModelMismatch",
            ReportPassed(reports, "Phase 2") &&
            Phase12CheckPassed(reports, "P12-E02"),
            "Phase2=" + BoolText(ReportPassed(reports, "Phase 2")) +
            ", P12-E02=" +
            BoolText(Phase12CheckPassed(reports, "P12-E02")));

        Add(
            checks,
            "B",
            "P13-B03",
            "Infinite duration runtime closed",
            "RuntimeModelMismatch",
            ReportPassed(reports, "Phase 2") &&
            ReportPassed(reports, "Phase 10"),
            "Phase2=" + BoolText(ReportPassed(reports, "Phase 2")) +
            ", Phase10=" +
            BoolText(ReportPassed(reports, "Phase 10")));

        Add(
            checks,
            "B",
            "P13-B04",
            "Bespoke status regression closed",
            "RuntimeModelMismatch",
            ReportPassed(reports, "Phase 6") &&
            Phase12CheckPassed(reports, "P12-E03"),
            "Phase6=" + BoolText(ReportPassed(reports, "Phase 6")) +
            ", P12-E03=" +
            BoolText(Phase12CheckPassed(reports, "P12-E03")));
    }

    // ---------------------------------------------------------------------
    // Gate C — Calculation / Timing
    // ---------------------------------------------------------------------

    private static void AddGateCCalculation(
        ICollection<Check> checks,
        IReadOnlyDictionary<string, ReportState> reports)
    {
        bool phase3 =
            ReportPassed(reports, "Phase 3");

        Add(
            checks,
            "C",
            "P13-C01",
            "Order-independent calculation closed",
            "CalculationMismatch",
            phase3 &&
            Phase12CheckPassed(reports, "P12-L02"),
            "Phase3=" + BoolText(phase3) +
            ", OppositePreservation=" +
            BoolText(Phase12CheckPassed(reports, "P12-L02")));

        Add(
            checks,
            "C",
            "P13-C02",
            "Heat / Stagnation aggregate closed",
            "CalculationMismatch",
            phase3 &&
            Phase12CheckPassed(reports, "P12-L05"),
            "Phase3=" + BoolText(phase3) +
            ", P12-L05=" +
            BoolText(Phase12CheckPassed(reports, "P12-L05")));

        Add(
            checks,
            "C",
            "P13-C03",
            "Regeneration aggregate closed",
            "CalculationMismatch",
            phase3 &&
            Phase12CheckPassed(reports, "P12-L04"),
            "Phase3=" + BoolText(phase3) +
            ", P12-L04=" +
            BoolText(Phase12CheckPassed(reports, "P12-L04")));

        Add(
            checks,
            "C",
            "P13-C04",
            "Healing modifier regression closed",
            "CalculationMismatch",
            phase3 &&
            Phase12CheckPassed(reports, "P12-L03"),
            "Phase3=" + BoolText(phase3) +
            ", Pain/FearPresence=" +
            BoolText(Phase12CheckPassed(reports, "P12-L03")));

        Add(
            checks,
            "C",
            "P13-C05",
            "Deferred / reservation timing closed",
            "TimingMismatch",
            ReportPassed(reports, "Phase 4"),
            "Phase4=" +
            BoolText(ReportPassed(reports, "Phase 4")));
    }

    // ---------------------------------------------------------------------
    // Gate D — Character
    // ---------------------------------------------------------------------

    private static void AddGateDCharacter(
        ICollection<Check> checks,
        IReadOnlyDictionary<string, ReportState> reports)
    {
        Add(
            checks,
            "D",
            "P13-D01",
            "Olaf confirmed 0922 content closed",
            "CharacterMismatch",
            ReportPassed(reports, "Phase 6") &&
            ReportPassed(reports, "Phase 7"),
            "Phase6=" + BoolText(ReportPassed(reports, "Phase 6")) +
            ", Phase7=" +
            BoolText(ReportPassed(reports, "Phase 7")));

        Add(
            checks,
            "D",
            "P13-D02",
            "Yujin confirmed 0922 content closed",
            "CharacterMismatch",
            ReportPassed(reports, "Phase 6") &&
            ReportPassed(reports, "Phase 7"),
            "Phase6=" + BoolText(ReportPassed(reports, "Phase 6")) +
            ", Phase7=" +
            BoolText(ReportPassed(reports, "Phase 7")));

        Add(
            checks,
            "D",
            "P13-D03",
            "Hifumi confirmed 0922 content closed",
            "CharacterMismatch",
            ReportPassed(reports, "Phase 8") &&
            Phase12CheckPassed(reports, "P12-HF01") &&
            Phase12CheckPassed(reports, "P12-HF02"),
            "Phase8=" + BoolText(ReportPassed(reports, "Phase 8")) +
            ", DuelPool=" +
            BoolText(Phase12CheckPassed(reports, "P12-HF01")) +
            ", AllIn=" +
            BoolText(Phase12CheckPassed(reports, "P12-HF02")));
    }

    // ---------------------------------------------------------------------
    // Gate E — Encounter / Progression
    // ---------------------------------------------------------------------

    private static void AddGateEEncounterProgression(
        ICollection<Check> checks,
        IReadOnlyDictionary<string, ReportState> reports)
    {
        Add(
            checks,
            "E",
            "P13-E01",
            "Enemy / Encounter / Boss confirmed rules closed",
            "EncounterMismatch",
            ReportPassed(reports, "Phase 9"),
            "Phase9=" +
            BoolText(ReportPassed(reports, "Phase 9")));

        Add(
            checks,
            "E",
            "P13-E02",
            "EmotionAugment / Progression confirmed rules closed",
            "ProgressionMismatch",
            ReportPassed(reports, "Phase 10") &&
            Phase12CheckPassed(reports, "P12-R03"),
            "Phase10=" + BoolText(ReportPassed(reports, "Phase 10")) +
            ", Catalog63=" +
            BoolText(Phase12CheckPassed(reports, "P12-R03")));

        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                EmotionCatalogPath);

        int slots =
            catalog?.Entries?.Count ?? 0;

        int nonNull =
            catalog?.Entries?.Count(x => x != null) ?? 0;

        int uniqueIds =
            catalog?.Entries?
                .Where(x => x != null)
                .Select(x => x.AugmentId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Count() ?? 0;

        Add(
            checks,
            "E",
            "P13-E03",
            "EmotionAugment live catalog remains 63 / 63 / 63",
            "DataMismatch",
            slots == 63 &&
            nonNull == 63 &&
            uniqueIds == 63,
            $"Slots={slots}, NonNull={nonNull}, UniqueIds={uniqueIds}");
    }

    // ---------------------------------------------------------------------
    // Gate F — UI
    // ---------------------------------------------------------------------

    private static void AddGateFHud(
        ICollection<Check> checks,
        IReadOnlyDictionary<string, ReportState> reports)
    {
        bool phase11 =
            ReportPassed(reports, "Phase 11");

        Add(
            checks,
            "F",
            "P13-F01",
            "HUD projection / aggregation closed",
            "HudMismatch",
            phase11 &&
            Phase11CheckPassed(reports, "P11-N01") &&
            Phase11CheckPassed(reports, "P11-N02"),
            "Phase11=" + BoolText(phase11) +
            ", Numeric=" +
            BoolText(Phase11CheckPassed(reports, "P11-N01")) +
            ", Infinite=" +
            BoolText(Phase11CheckPassed(reports, "P11-N02")));

        Add(
            checks,
            "F",
            "P13-F02",
            "HUD projection never mutates runtime",
            "HudMismatch",
            phase11 &&
            Phase11CheckPassed(reports, "P11-X01"),
            "ProjectionPure=" +
            BoolText(Phase11CheckPassed(reports, "P11-X01")));

        Add(
            checks,
            "F",
            "P13-F03",
            "Required reduced-motion presentation exists",
            "HudMismatch",
            phase11 &&
            Phase11CheckPassed(reports, "P11-M01") &&
            Phase11CheckPassed(reports, "P11-M02"),
            "ReducedMotion=" +
            BoolText(Phase11CheckPassed(reports, "P11-M01")) +
            ", ChangeOnly=" +
            BoolText(Phase11CheckPassed(reports, "P11-M02")));
    }

    // ---------------------------------------------------------------------
    // Gate G — Unity / Verification
    // ---------------------------------------------------------------------

    private static void AddGateGUnity(
        ICollection<Check> checks,
        IReadOnlyDictionary<string, ReportState> reports)
    {
        bool editorStable =
            !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating &&
            !EditorApplication.isPlayingOrWillChangePlaymode;

        Add(
            checks,
            "G",
            "P13-G01",
            "Final gate executes in a stable compiled editor",
            "VerificationFailure",
            editorStable,
            "Compiling=" + EditorApplication.isCompiling +
            ", Updating=" + EditorApplication.isUpdating +
            ", PlayTransition=" +
            EditorApplication.isPlayingOrWillChangePlaymode);

        List<string> reportFailures =
            reports.Values
                .Where(x => !x.Exists || !x.Passed)
                .Select(x =>
                    x.Requirement.Phase +
                    (x.Exists
                        ? ":RESULT_NOT_PASS"
                        : ":MISSING"))
                .ToList();

        Add(
            checks,
            "G",
            "P13-G02",
            "Phase 1~12 verification artifact chain is complete",
            "VerificationFailure",
            reportFailures.Count == 0,
            reportFailures.Count == 0
                ? "12/12 PASS artifacts"
                : string.Join(", ", reportFailures));

        List<string> missingAssets =
            ValidateRequiredAssets();

        Add(
            checks,
            "G",
            "P13-G03",
            "Required final assets are present and loadable",
            "VerificationFailure",
            missingAssets.Count == 0,
            missingAssets.Count == 0
                ? "Required assets PASS"
                : "Missing/invalid=" +
                  string.Join(", ", missingAssets));

        int missingScriptCount =
            CountMissingScenePrefabScripts(
                out List<string> missingScriptSamples);

        Add(
            checks,
            "G",
            "P13-G04",
            "Assets scenes/prefabs have no missing MonoBehaviour script",
            "VerificationFailure",
            missingScriptCount == 0,
            missingScriptCount == 0
                ? "MissingScript=0"
                : "MissingScript=" +
                  missingScriptCount +
                  "; Samples=" +
                  string.Join(", ", missingScriptSamples));

        bool phase12 =
            ReportPassed(reports, "Phase 12");

        Add(
            checks,
            "G",
            "P13-G05",
            "Full cleanup / smoke / regression gate is PASS",
            "VerificationFailure",
            phase12,
            "Phase12=" + BoolText(phase12));

        bool legacyClosed =
            phase12 &&
            Phase12CheckPassed(reports, "P12-L01") &&
            Phase12CheckPassed(reports, "P12-L02") &&
            Phase12CheckPassed(reports, "P12-L03") &&
            Phase12CheckPassed(reports, "P12-L04") &&
            Phase12CheckPassed(reports, "P12-L05") &&
            Phase12CheckPassed(reports, "P12-L06") &&
            Phase12CheckPassed(reports, "P12-L07") &&
            Phase12CheckPassed(reports, "P12-L08");

        Add(
            checks,
            "G",
            "P13-G06",
            "Known pre-0922 legacy behavior leak count is zero",
            "LegacyLeak",
            legacyClosed,
            legacyClosed
                ? "P12-L01..L08 PASS"
                : "One or more Phase12 legacy checks are not PASS");

        RunFlowLiveContentRegistry registry =
            AssetDatabase.LoadAssetAtPath<RunFlowLiveContentRegistry>(
                RegistryPath);

        bool registryHealthy =
            registry != null &&
            registry.RunItemCatalog != null &&
            registry.EmotionAugmentCatalog != null &&
            registry.OlafPrefab != null &&
            registry.YujinPrefab != null &&
            registry.HifumiPrefab != null;

        Add(
            checks,
            "G",
            "P13-G07",
            "RunFlow live registry required references are wired",
            "DataMismatch",
            registryHealthy,
            registry == null
                ? "Registry=MISSING"
                : "RunItem=" + (registry.RunItemCatalog != null) +
                  ", Emotion=" +
                  (registry.EmotionAugmentCatalog != null) +
                  ", Olaf=" + (registry.OlafPrefab != null) +
                  ", Yujin=" + (registry.YujinPrefab != null) +
                  ", Hifumi=" + (registry.HifumiPrefab != null));
    }

    // ---------------------------------------------------------------------
    // Report / helper
    // ---------------------------------------------------------------------

    private static Dictionary<string, ReportState> LoadPhaseReports()
    {
        Dictionary<string, ReportState> states =
            new Dictionary<string, ReportState>(
                StringComparer.Ordinal);

        foreach (ReportRequirement requirement in PhaseReports)
        {
            string fullPath =
                ToProjectAbsolutePath(requirement.Path);

            bool exists =
                File.Exists(fullPath);

            string text =
                exists
                    ? File.ReadAllText(fullPath)
                    : string.Empty;

            bool passed =
                exists &&
                ReportHasExpectedPassResult(
                    requirement,
                    text);

            states[requirement.Phase] =
                new ReportState
                {
                    Requirement = requirement,
                    Exists = exists,
                    Passed = passed,
                    Text = text
                };
        }

        return states;
    }

    /// <summary>
    /// [0922_PHASE13_REPORT_PARSER_R2]
    ///
    /// Historical 0922 verifiers intentionally use two result renderings:
    /// - Console summary: PHASEn_RESULT=PASS_...
    /// - Markdown report: - RESULT: PASS_...
    ///
    /// Phase 1 also writes a spaced markdown form:
    /// PHASE1_RESULT = PASS_BASELINE_CAPTURED
    ///
    /// The Final Closure Gate must accept the persisted report syntax instead
    /// of assuming that the Console-only marker was written to disk.
    /// </summary>
    private static bool ReportHasExpectedPassResult(
        ReportRequirement requirement,
        string text)
    {
        if (requirement == null ||
            string.IsNullOrWhiteSpace(text) ||
            string.IsNullOrWhiteSpace(requirement.Token))
        {
            return false;
        }

        string expectedMarker =
            requirement.Token.Trim();

        int equalsIndex =
            expectedMarker.IndexOf('=');

        string resultToken =
            equalsIndex >= 0 &&
            equalsIndex + 1 < expectedMarker.Length
                ? expectedMarker.Substring(equalsIndex + 1).Trim()
                : expectedMarker;

        bool resultPass =
            text.Contains(
                expectedMarker,
                StringComparison.Ordinal) ||
            text.Contains(
                "RESULT: " + resultToken,
                StringComparison.Ordinal) ||
            text.Contains(
                "RESULT = " + resultToken,
                StringComparison.Ordinal);

        // Phase 1's persisted markdown uses:
        // **PHASE1_RESULT = PASS_BASELINE_CAPTURED**
        if (string.Equals(
                requirement.Phase,
                "Phase 1",
                StringComparison.Ordinal))
        {
            resultPass =
                resultPass ||
                text.Contains(
                    "PHASE1_RESULT = " + resultToken,
                    StringComparison.Ordinal);

            bool harnessPass =
                text.Contains(
                    "HARNESS_FAIL=0",
                    StringComparison.Ordinal);

            return
                resultPass &&
                harnessPass;
        }

        // A report explicitly persisted as FAIL must never pass simply because
        // an explanatory sentence happens to mention the expected token.
        bool explicitFailure =
            text.Contains(
                "- RESULT: FAIL",
                StringComparison.Ordinal) ||
            text.Contains(
                "RESULT: FAIL",
                StringComparison.Ordinal) ||
            text.Contains(
                "RESULT = FAIL",
                StringComparison.Ordinal);

        return
            resultPass &&
            !explicitFailure;
    }

    private static bool ReportPassed(
        IReadOnlyDictionary<string, ReportState> reports,
        string phase)
    {
        return
            reports != null &&
            reports.TryGetValue(
                phase,
                out ReportState state) &&
            state != null &&
            state.Exists &&
            state.Passed;
    }

    private static bool Phase11CheckPassed(
        IReadOnlyDictionary<string, ReportState> reports,
        string id)
    {
        return
            ReportContainsCheckedId(
                reports,
                "Phase 11",
                id);
    }

    private static bool Phase12CheckPassed(
        IReadOnlyDictionary<string, ReportState> reports,
        string id)
    {
        return
            ReportContainsCheckedId(
                reports,
                "Phase 12",
                id);
    }

    private static bool ReportContainsCheckedId(
        IReadOnlyDictionary<string, ReportState> reports,
        string phase,
        string id)
    {
        if (reports == null ||
            !reports.TryGetValue(
                phase,
                out ReportState state) ||
            state == null ||
            !state.Passed ||
            string.IsNullOrWhiteSpace(state.Text))
        {
            return false;
        }

        string token =
            "[x] `" + id + "`";

        return state.Text.Contains(
            token,
            StringComparison.Ordinal);
    }

    private static List<string> ValidateRequiredAssets()
    {
        List<string> missing =
            new List<string>();

        if (AssetDatabase.LoadAssetAtPath<RunFlowLiveContentRegistry>(
                RegistryPath) == null)
        {
            missing.Add("RunFlowLiveContentRegistry");
        }

        if (AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                EmotionCatalogPath) == null)
        {
            missing.Add("EmotionAugmentCatalog");
        }

        if (AssetDatabase.LoadAssetAtPath<RunItemCatalog>(
                RunItemCatalogPath) == null)
        {
            missing.Add("RunItemCatalog");
        }

        if (AssetDatabase.LoadMainAssetAtPath(
                EnemyCatalogPath) == null)
        {
            missing.Add("EnemyEncounterCatalog");
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                RunScenePath) == null)
        {
            missing.Add("Run Flow Test scene");
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                BattleScenePath) == null)
        {
            missing.Add("Battle Test Scene");
        }

        return missing;
    }

    private static int CountMissingScenePrefabScripts(
        out List<string> samples)
    {
        samples =
            new List<string>();

        int count = 0;

        string assetsRoot =
            Application.dataPath;

        if (string.IsNullOrWhiteSpace(assetsRoot) ||
            !Directory.Exists(assetsRoot))
        {
            samples.Add("Assets root unavailable");
            return 1;
        }

        IEnumerable<string> files =
            Directory
                .EnumerateFiles(
                    assetsRoot,
                    "*.unity",
                    SearchOption.AllDirectories)
                .Concat(
                    Directory.EnumerateFiles(
                        assetsRoot,
                        "*.prefab",
                        SearchOption.AllDirectories));

        foreach (string physicalPath in files)
        {
            string text;

            try
            {
                text =
                    File.ReadAllText(physicalPath);
            }
            catch
            {
                // A non-readable serialized asset is a final-gate problem.
                count++;

                if (samples.Count < 8)
                    samples.Add(ToAssetPath(physicalPath) + ":unreadable");

                continue;
            }

            if (!text.Contains(
                    "--- !u!114",
                    StringComparison.Ordinal))
            {
                continue;
            }

            string[] sections =
                Regex.Split(
                    text,
                    @"(?m)^--- !u!");

            foreach (string section in sections)
            {
                if (!section.StartsWith(
                        "114 ",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!section.Contains(
                        "m_Script: {fileID: 0}",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                count++;

                if (samples.Count < 8)
                    samples.Add(ToAssetPath(physicalPath));

                break;
            }
        }

        return count;
    }

    private static string ToAssetPath(
        string physicalPath)
    {
        string normalized =
            (physicalPath ?? string.Empty)
                .Replace('\\', '/');

        string root =
            (Application.dataPath ?? string.Empty)
                .Replace('\\', '/');

        if (normalized.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Assets" +
                normalized.Substring(root.Length);
        }

        return normalized;
    }

    private static string ToProjectAbsolutePath(
        string projectRelativePath)
    {
        string projectRoot =
            Directory.GetParent(
                Application.dataPath)?.FullName ??
            Directory.GetCurrentDirectory();

        return Path.GetFullPath(
            Path.Combine(
                projectRoot,
                projectRelativePath));
    }

    private static Dictionary<string, int> BuildMetricCounts(
        IEnumerable<Check> checks)
    {
        Dictionary<string, int> result =
            new Dictionary<string, int>(
                StringComparer.Ordinal);

        foreach (string metric in MetricOrder)
            result[metric] = 0;

        foreach (Check check in checks ?? Array.Empty<Check>())
        {
            if (check == null ||
                check.Passed ||
                string.IsNullOrWhiteSpace(check.Metric) ||
                check.Metric == "UnauthorizedHardcodedPending")
            {
                continue;
            }

            if (!result.ContainsKey(check.Metric))
                result[check.Metric] = 0;

            result[check.Metric]++;
        }

        return result;
    }

    private static string BuildMetricSummary(
        IReadOnlyDictionary<string, int> metrics,
        int unauthorizedHardcodedPending)
    {
        StringBuilder builder =
            new StringBuilder();

        foreach (string metric in MetricOrder)
        {
            int value =
                metrics != null &&
                metrics.TryGetValue(
                    metric,
                    out int found)
                    ? found
                    : 0;

            builder.AppendLine(
                metric + "=" + value);
        }

        builder.Append(
            "UnauthorizedHardcodedPending=" +
            unauthorizedHardcodedPending);

        return builder.ToString();
    }

    private static void WriteReport(
        IReadOnlyList<Check> checks,
        IReadOnlyDictionary<string, ReportState> reports,
        IReadOnlyDictionary<string, int> metrics,
        int unauthorizedHardcodedPending,
        int confirmedRequiredPatchCount,
        int pendingCanonical,
        int pass,
        int fail,
        string result)
    {
        string fullPath =
            ToProjectAbsolutePath(ReportPath);

        string directory =
            Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        StringBuilder writer =
            new StringBuilder();

        writer.AppendLine(
            "# 0922 Phase 13 — Final Closure Gate");
        writer.AppendLine();
        writer.AppendLine($"- PASS: {pass}");
        writer.AppendLine($"- FAIL: {fail}");
        writer.AppendLine(
            $"- PENDING_CANONICAL: {pendingCanonical}");
        writer.AppendLine(
            "- 0922 Confirmed Canonical Required Patch Count: " +
            confirmedRequiredPatchCount);
        writer.AppendLine(
            "- UnauthorizedHardcodedPending: " +
            unauthorizedHardcodedPending);
        writer.AppendLine($"- RESULT: {result}");
        writer.AppendLine();

        writer.AppendLine(
            "## Final Closure Metrics");
        writer.AppendLine();

        foreach (string metric in MetricOrder)
        {
            int value =
                metrics != null &&
                metrics.TryGetValue(
                    metric,
                    out int found)
                    ? found
                    : 0;

            writer.AppendLine(
                $"- `{metric}` = {value}");
        }

        writer.AppendLine(
            "- `UnauthorizedHardcodedPending` = " +
            unauthorizedHardcodedPending);

        writer.AppendLine();
        writer.AppendLine(
            "## Gate Checks");
        writer.AppendLine();

        foreach (IGrouping<string, Check> gate
                 in checks.GroupBy(x => x.Gate))
        {
            writer.AppendLine(
                "### Gate " + gate.Key);
            writer.AppendLine();

            foreach (Check check in gate)
            {
                writer.AppendLine(
                    "- [" +
                    (check.Passed ? "x" : " ") +
                    "] `" +
                    check.Id +
                    "` " +
                    check.Name +
                    " — " +
                    check.Actual);
            }

            writer.AppendLine();
        }

        writer.AppendLine(
            "## Phase Artifact Chain");
        writer.AppendLine();

        foreach (ReportRequirement requirement in PhaseReports)
        {
            reports.TryGetValue(
                requirement.Phase,
                out ReportState state);

            bool artifactPass =
                state != null &&
                state.Exists &&
                state.Passed;

            writer.AppendLine(
                "- [" +
                (artifactPass ? "x" : " ") +
                "] " +
                requirement.Phase +
                " — `" +
                requirement.Token +
                "` — " +
                requirement.Path);
        }

        writer.AppendLine();
        writer.AppendLine(
            "## PENDING_CANONICAL");
        writer.AppendLine();

        IReadOnlyList<string> pending =
            Canonical0922BaselineSpec.PendingCanonicalDomains;

        if (pending == null ||
            pending.Count == 0)
        {
            writer.AppendLine(
                "- NONE (unexpected for current 0922 baseline)");
        }
        else
        {
            for (int i = 0; i < pending.Count; i++)
                writer.AppendLine("- " + pending[i]);
        }

        writer.AppendLine();
        writer.AppendLine(
            "## Final Formula");
        writer.AppendLine();
        writer.AppendLine("```text");

        foreach (string metric in MetricOrder)
        {
            int value =
                metrics != null &&
                metrics.TryGetValue(
                    metric,
                    out int found)
                    ? found
                    : 0;

            writer.AppendLine(
                metric + " = " + value);
        }

        writer.AppendLine();
        writer.AppendLine(
            "UnauthorizedHardcodedPending = " +
            unauthorizedHardcodedPending);
        writer.AppendLine();
        writer.AppendLine(
            "0922 Confirmed Canonical Required Patch Count = " +
            confirmedRequiredPatchCount);
        writer.AppendLine("```");
        writer.AppendLine();

        writer.AppendLine(
            "PHASE13_RESULT=" + result);

        File.WriteAllText(
            fullPath,
            writer.ToString(),
            new UTF8Encoding(false));

        Debug.Log(
            "[0922 Phase13] Report written: " +
            fullPath);
    }

    private static void Add(
        ICollection<Check> checks,
        string gate,
        string id,
        string name,
        string metric,
        bool passed,
        string actual)
    {
        checks.Add(
            new Check
            {
                Gate = gate,
                Id = id,
                Name = name,
                Metric = metric,
                Passed = passed,
                Actual = actual ?? string.Empty
            });
    }

    private static string BoolText(
        bool value)
    {
        return value ? "PASS" : "FAIL";
    }
}
#endif
