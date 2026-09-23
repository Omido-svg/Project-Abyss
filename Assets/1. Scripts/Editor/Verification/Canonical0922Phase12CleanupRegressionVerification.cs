#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Phase12CleanupRegressionVerification
{
    private const string ReportPath =
        "Logs/GameSystemVerification/0922_Phase12_CleanupRegression.md";

    private const string Phase6ReportPath =
        "Logs/GameSystemVerification/0922_Phase6_CharacterKeywords.md";
    private const string Phase7ReportPath =
        "Logs/GameSystemVerification/0922_Phase7_OlafYujinSkillMigration.md";
    private const string Phase8ReportPath =
        "Logs/GameSystemVerification/0922_Phase8_Hifumi.md";
    private const string Phase9ReportPath =
        "Logs/GameSystemVerification/0922_Phase9_EnemyEncounter.md";
    private const string Phase10ReportPath =
        "Logs/GameSystemVerification/0922_Phase10_EmotionAugment.md";
    private const string Phase11ReportPath =
        "Logs/GameSystemVerification/0922_Phase11_HudPresentation.md";

    private const string CommonStatusesPath =
        "Assets/1. Scripts/Runtime/Status/Common/CommonCombatStatuses.cs";
    private const string CharacterStatusControllerPath =
        "Assets/1. Scripts/Runtime/Characters/CharacterBase/CharacterStatusController.cs";
    private const string CharacterPath =
        "Assets/1. Scripts/Runtime/Characters/CharacterBase/Character.cs";
    private const string YujinMechanicPath =
        "Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs";
    private const string HifumiMechanicPath =
        "Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs";
    private const string PhaseBModulePath =
        "Assets/1. Scripts/Runtime/Diagnostics/Verification/PhaseBGameSystemVerificationModule.cs";
    private const string StatusVisualDirectorPath =
        "Assets/1. Scripts/Runtime/Presentation/Visual/Director/BattleStatusVisualDirector.cs";
    private const string BattleVfxTimingPath =
        "Assets/1. Scripts/Runtime/Presentation/Visual/VFX/Core/BattleVfxTiming.cs";
    private const string EmotionEffectPath =
        "Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/EmotionAugmentCanonical0922EffectDefinition.cs";
    private const string AutoPlanPath =
        "Assets/1. Scripts/Runtime/Systems/AI/PlayerAutoPlanService.cs";
    private const string HifumiAutoPlanPath =
        "Assets/1. Scripts/Runtime/Systems/AI/Advisors/HifumiAutoPlanAdvisor.cs";

    private const string RegistryPath =
        "Assets/Resources/ProjectAbyss/RunFlowLiveContentRegistry.asset";
    private const string EmotionCatalogPath =
        "Assets/2. Data/Progression/EmotionAugments/EmotionAugmentCatalog.asset";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 12 - Verify Cleanup Regression")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new();

        AddPrerequisiteChecks(checks);
        AddLegacyLeakChecks(checks);
        AddStatusSemanticChecks(checks);
        AddEventVfxChecks(checks);
        AddHifumiRegressionChecks(checks);
        AddIntegrationRegressionChecks(checks);
        AddCleanupChecks(checks);

        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;
        string result = fail == 0
            ? "PASS_CLEANUP_REGRESSION_0922"
            : "FAIL";

        WriteReport(checks, pass, fail, result);

        string summary =
            "[0922 Phase 12 · Event / VFX / Legacy Cleanup / Regression]\n" +
            $"PASS={pass} FAIL={fail}\n\n" +
            "PASS\n- " +
            string.Join(
                "\n- ",
                checks.Where(x => x.Passed)
                    .Select(x => $"{x.Id} | {x.Name} | {x.Actual}")) +
            "\n\nFAIL\n- " +
            (fail == 0
                ? "NONE"
                : string.Join(
                    "\n- ",
                    checks.Where(x => !x.Passed)
                        .Select(x => $"{x.Id} | {x.Name} | {x.Actual}"))) +
            $"\n\nPHASE12_RESULT={result}";

        if (fail == 0)
            Debug.Log(summary);
        else
            Debug.LogError(summary);
    }

    private static void AddPrerequisiteChecks(
        ICollection<Check> checks)
    {
        Add(
            checks,
            "P12-H00",
            "0922 canonical identity",
            Canonical0922BaselineSpec.CanonicalVersion == "0922",
            $"Canonical={Canonical0922BaselineSpec.CanonicalVersion}");

        AddReportCheck(
            checks,
            "P12-H01",
            "Phase 6 Character Keywords regression artifact",
            Phase6ReportPath,
            "PASS_CHARACTER_KEYWORDS");

        AddReportCheck(
            checks,
            "P12-H02",
            "Phase 7 Olaf/Yujin skill regression artifact",
            Phase7ReportPath,
            "PASS_OLAF_YUJIN_SKILLS");

        AddReportCheck(
            checks,
            "P12-H03",
            "Phase 8 Hifumi regression artifact",
            Phase8ReportPath,
            "PASS_HIFUMI_0922");

        AddReportCheck(
            checks,
            "P12-H04",
            "Phase 9 Enemy/Encounter/Boss regression artifact",
            Phase9ReportPath,
            "PASS_ENEMY_ENCOUNTER_0922");

        AddReportCheck(
            checks,
            "P12-H05",
            "Phase 10 EmotionAugment regression artifact",
            Phase10ReportPath,
            "PASS_EMOTION_AUGMENT_0922");

        AddReportCheck(
            checks,
            "P12-H06",
            "Phase 11 HUD regression artifact",
            Phase11ReportPath,
            "PASS_HUD_PRESENTATION_0922");
    }

    private static void AddLegacyLeakChecks(
        ICollection<Check> checks)
    {
        string common = ReadSource(CommonStatusesPath);
        string controller = ReadSource(CharacterStatusControllerPath);
        string yujin = ReadSource(YujinMechanicPath);
        string phaseB = ReadSource(PhaseBModulePath);

        bool noOneTurnAlias =
            !common.Contains(
                "class OneTurnCommonStatus",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-L01",
            "OneTurnCommonStatus runtime alias removed",
            noOneTurnAlias,
            noOneTurnAlias
                ? "No runtime OneTurnCommonStatus symbol"
                : "Legacy alias still exists");

        bool noDestructiveOpposite =
            !controller.Contains(
                "ResolveOppositeCharacterStatus",
                StringComparison.Ordinal) &&
            !controller.Contains(
                "ResolveOppositePartStatus",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-L02",
            "No destructive opposite-status cancellation path",
            noDestructiveOpposite,
            noDestructiveOpposite
                ? "Opposite statuses are preserved in storage"
                : "Legacy destructive opposite resolver detected");

        PainStatus pain = new PainStatus(3);
        OlafFearStatus fear = new OlafFearStatus(3);
        BattleAction fearProbe =
            new BattleAction
            {
                Slot = new ActionSlot()
            };

        bool presenceCanonical =
            pain.StorageKind == StatusEffectStorageKind.PresenceTimed &&
            pain.NumericValue == 0 &&
            fear.StorageKind == StatusEffectStorageKind.PresenceTimed &&
            fear.NumericValue == 0 &&
            fear.ModifyRoll(fearProbe, 10) == 9;

        Add(
            checks,
            "P12-L03",
            "Pain/Fear remain T-only presence statuses",
            presenceCanonical,
            $"PainN={pain.NumericValue}, FearN={fear.NumericValue}, Fear10->{fear.ModifyRoll(fearProbe, 10)}");

        RegenerationStatus regenA =
            new RegenerationStatus(
                3,
                4,
                RegenerationRecoveryChannel.HitPoints);

        RegenerationStatus regenB =
            new RegenerationStatus(
                2,
                3,
                RegenerationRecoveryChannel.Stagger);

        int regenTotal =
            CommonStatusAlgebra.GetRegenerationTotal(
                new StatusEffect[]
                {
                    regenA,
                    regenB
                });

        bool regenCanonical =
            regenTotal == 7 &&
            !controller.Contains(
                "RegenerationRecoveryChannel",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-L04",
            "Regeneration gameplay has no HP/Stagger channel split",
            regenCanonical,
            $"RegenTotal={regenTotal}, ControllerChannelBranch={!regenCanonical && controller.Contains("RegenerationRecoveryChannel", StringComparison.Ordinal)}");

        int prestigeDelta =
            CommonStatusAlgebra.GetTurnEndPrestigeDelta(
                new StatusEffect[]
                {
                    new HeatStatus(3, 2),
                    new StagnationStatus(1, 3)
                });

        bool heatCanonical =
            prestigeDelta == 2 &&
            !common.Contains(
                "Owner.AddPrestige(Stack)",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-L05",
            "Heat/Stagnation only resolve through TurnEnd aggregate",
            heatCanonical,
            $"Heat3-Stagnation1={prestigeDelta}, ImmediatePayoutPattern={!heatCanonical}");

        bool designationCanonical =
            yujin.Contains(
                "GetDesignationTotal",
                StringComparison.Ordinal) &&
            yujin.Contains(
                "ApplyMarkGain",
                StringComparison.Ordinal) &&
            !yujin.Contains(
                "amount += 2;",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-L06",
            "Designation uses current total N, not legacy fixed +2",
            designationCanonical,
            designationCanonical
                ? "GetDesignationTotal -> ApplyMarkGain"
                : "Legacy fixed designation path detected");

        StrengthStatus uncapped =
            new StrengthStatus(
                500,
                25);

        bool noCommonCap =
            uncapped.NumericValue == 500 &&
            uncapped.Duration == 25;

        Add(
            checks,
            "P12-L07",
            "No legacy common Value/Duration hard cap",
            noCommonCap,
            $"Strength={uncapped.NumericValue}·{uncapped.Duration}");

        bool phaseBCanonicalized =
            phaseB.Contains(
                "[0922_PHASE12_CANONICALIZED_REGRESSION]",
                StringComparison.Ordinal) &&
            !phaseB.Contains(
                "[LEGACY BASELINE]",
                StringComparison.Ordinal) &&
            !phaseB.Contains(
                "Strength1 / Weakness 제거",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-L08",
            "Legacy PhaseB status expectations replaced by 0922 regression",
            phaseBCanonicalized,
            phaseBCanonicalized
                ? "C-27/C-48/C-49 canonicalized"
                : "Stale pre-0922 expectation remains");
    }

    private static void AddStatusSemanticChecks(
        ICollection<Check> checks)
    {
        GameObject go = null;

        try
        {
            go = new GameObject(
                "__0922_PHASE12_STATUS_EVENT_PROBE__");

            NormalEnemy owner =
                go.AddComponent<NormalEnemy>();

            CharacterStatusController controller =
                new CharacterStatusController(owner);

            StatusEffectApplyResult numericA =
                controller.AddStatus(
                    new StrengthStatus(2, 3),
                    owner);

            StatusEffectApplyResult numericB =
                controller.AddStatus(
                    new StrengthStatus(3, 1),
                    owner);

            int numericEntries =
                controller.CharacterStatuses
                    .OfType<StrengthStatus>()
                    .Count();

            bool numericSemantic =
                numericA?.CanonicalSemantic ==
                    CanonicalStatusApplySemanticKind.NumericEntryApplied &&
                numericB?.CanonicalSemantic ==
                    CanonicalStatusApplySemanticKind.NumericEntryApplied &&
                numericEntries == 2;

            Add(
                checks,
                "P12-E01",
                "Numeric grant emits new-entry semantic",
                numericSemantic,
                $"A={numericA?.CanonicalSemantic}, B={numericB?.CanonicalSemantic}, Entries={numericEntries}");

            StatusEffectApplyResult painA =
                controller.AddStatus(
                    new PainStatus(2),
                    owner);

            StatusEffectApplyResult painB =
                controller.AddStatus(
                    new PainStatus(5),
                    owner);

            PainStatus mergedPain =
                controller.CharacterStatuses
                    .OfType<PainStatus>()
                    .SingleOrDefault();

            bool presenceSemantic =
                painA?.CanonicalSemantic ==
                    CanonicalStatusApplySemanticKind.PresenceApplied &&
                painB?.CanonicalSemantic ==
                    CanonicalStatusApplySemanticKind.PresenceRefreshed &&
                mergedPain != null &&
                mergedPain.Duration == 5 &&
                controller.CharacterStatuses
                    .OfType<PainStatus>()
                    .Count() == 1;

            Add(
                checks,
                "P12-E02",
                "Presence regrant emits refresh semantic",
                presenceSemantic,
                $"A={painA?.CanonicalSemantic}, B={painB?.CanonicalSemantic}, T={mergedPain?.Duration}");

            StatusEffectApplyResult bleedingA =
                controller.AddStatus(
                    new Bleeding(2),
                    owner);

            StatusEffectApplyResult bleedingB =
                controller.AddStatus(
                    new Bleeding(3),
                    owner);

            Bleeding bleeding =
                controller.CharacterStatuses
                    .OfType<Bleeding>()
                    .SingleOrDefault();

            bool bespokeSemantic =
                bleedingA?.CanonicalSemantic ==
                    CanonicalStatusApplySemanticKind.BespokeApplied &&
                bleedingB?.CanonicalSemantic ==
                    CanonicalStatusApplySemanticKind.BespokeStacked &&
                bleeding != null &&
                bleeding.Stack == 5;

            Add(
                checks,
                "P12-E03",
                "Bespoke regrant emits stacked semantic",
                bespokeSemantic,
                $"A={bleedingA?.CanonicalSemantic}, B={bleedingB?.CanonicalSemantic}, Stack={bleeding?.Stack}");
        }
        catch (Exception exception)
        {
            Add(
                checks,
                "P12-E99",
                "Status semantic probe executes without exception",
                false,
                exception.GetType().Name + ": " + exception.Message);
        }
        finally
        {
            if (go != null)
                UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void AddEventVfxChecks(
        ICollection<Check> checks)
    {
        string director =
            ReadSource(StatusVisualDirectorPath);

        string timing =
            ReadSource(BattleVfxTimingPath);

        bool semanticRouting =
            director.Contains(
                "CanonicalStatusApplySemanticKind.PresenceRefreshed",
                StringComparison.Ordinal) &&
            director.Contains(
                "CanonicalStatusApplySemanticKind.BespokeStacked",
                StringComparison.Ordinal) &&
            director.Contains(
                "StatusEffectVisualPhase.Refreshed",
                StringComparison.Ordinal) &&
            director.Contains(
                "StatusEffectVisualPhase.Stacked",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-V01",
            "Status VFX routes refresh and stack separately",
            semanticRouting,
            semanticRouting
                ? "PresenceRefreshed / BespokeStacked routed"
                : "Canonical status lifecycle VFX routing missing");

        bool timings =
            timing.Contains(
                "OnStatusApplied",
                StringComparison.Ordinal) &&
            timing.Contains(
                "OnStatusRefreshed",
                StringComparison.Ordinal) &&
            timing.Contains(
                "OnStatusStacked",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-V02",
            "VFX timing catalog distinguishes applied/refreshed/stacked",
            timings,
            timings
                ? "Applied / Refreshed / Stacked present"
                : "Lifecycle timing enum incomplete");
    }

    private static void AddHifumiRegressionChecks(
        ICollection<Check> checks)
    {
        string[] canonical =
            HifumiSkillIds.CanonicalDuel;

        bool duelPool =
            canonical != null &&
            canonical.Length == 11 &&
            !canonical.Contains(HifumiSkillIds.RiskLife) &&
            !canonical.Contains(HifumiSkillIds.RecoverPrincipal) &&
            canonical.Contains(HifumiSkillIds.RollCompendium) &&
            canonical.Contains(HifumiSkillIds.FlipTable) &&
            canonical.Contains(HifumiSkillIds.Provoke) &&
            canonical.Contains(HifumiSkillIds.CleanseAll);

        Add(
            checks,
            "P12-HF01",
            "Hifumi canonical Duel pool excludes legacy D/F",
            duelPool,
            canonical == null
                ? "NULL"
                : $"Count={canonical.Length}, D={canonical.Contains(HifumiSkillIds.RiskLife)}, F={canonical.Contains(HifumiSkillIds.RecoverPrincipal)}");

        HifumiMechanic mechanic =
            new HifumiMechanic();

        mechanic.SetBoneForVerification(200);
        mechanic.ResolveAllInForVerification(
            ChinchiroCombination.Arashi,
            ChinchiroCombination.Blank,
            ChinchiroCombination.Moku);
        int arashi = mechanic.Bone;

        mechanic.SetBoneForVerification(200);
        mechanic.ResolveAllInForVerification(
            ChinchiroCombination.Moku,
            ChinchiroCombination.Blank,
            ChinchiroCombination.Blank);
        int moku = mechanic.Bone;

        mechanic.SetBoneForVerification(200);
        mechanic.ResolveAllInForVerification(
            ChinchiroCombination.Arashi,
            ChinchiroCombination.Hifumi,
            ChinchiroCombination.Moku);
        int catastrophe = mechanic.Bone;

        bool allIn =
            arashi == HifumiMechanic.MaxBone &&
            moku == 300 &&
            catastrophe == 0;

        Add(
            checks,
            "P12-HF02",
            "Hifumi All-In uses 0922 payout",
            allIn,
            $"Arashi={arashi}, Moku={moku}, Catastrophe={catastrophe}");
    }

    private static void AddIntegrationRegressionChecks(
        ICollection<Check> checks)
    {
        // [0922_PHASE12_CATALOG_REPAIR_PREFLIGHT]
        bool catalogRepairReady =
            Canonical0922Phase12EmotionCatalogRepair
                .EnsureCanonicalCatalogWiring(logResult: false);

        RunFlowLiveContentRegistry registry =
            AssetDatabase.LoadAssetAtPath<RunFlowLiveContentRegistry>(
                RegistryPath);

        EmotionAugmentCatalog catalog =
            AssetDatabase.LoadAssetAtPath<EmotionAugmentCatalog>(
                EmotionCatalogPath);

        bool runFlow =
            registry != null &&
            catalog != null &&
            ReferenceEquals(
                registry.EmotionAugmentCatalog,
                catalog);

        Add(
            checks,
            "P12-R01",
            "RunFlow registry keeps canonical EmotionAugment catalog",
            runFlow,
            registry == null
                ? "Registry=MISSING"
                : catalog == null
                    ? "Catalog=MISSING"
                    : $"RegistryCatalog={registry.EmotionAugmentCatalog?.name}");

        string autoPlan = ReadSource(AutoPlanPath);
        string hifumiAutoPlan = ReadSource(HifumiAutoPlanPath);

        bool autoPlanPresent =
            !string.IsNullOrWhiteSpace(autoPlan) &&
            !string.IsNullOrWhiteSpace(hifumiAutoPlan) &&
            autoPlan.Contains(
                "PlayerAutoPlanService",
                StringComparison.Ordinal) &&
            hifumiAutoPlan.Contains(
                "HifumiAutoPlanAdvisor",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-R02",
            "AutoPlan integration source remains present",
            autoPlanPresent,
            autoPlanPresent
                ? "PlayerAutoPlanService + HifumiAutoPlanAdvisor"
                : "AutoPlan integration source missing");

        int catalogSlots =
            catalog?.Entries?.Count ?? 0;

        int nonNullEntries =
            catalog?.Entries?.Count(x => x != null) ?? 0;

        int uniqueIds =
            catalog?.Entries?
                .Where(x => x != null)
                .Select(x => x.AugmentId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Count() ?? 0;

        bool emotion63 =
            catalogRepairReady &&
            catalogSlots == 63 &&
            nonNullEntries == 63 &&
            uniqueIds == 63;

        Add(
            checks,
            "P12-R03",
            "EmotionAugment catalog still has 63 unique entries",
            emotion63,
            $"RepairReady={catalogRepairReady}, Slots={catalogSlots}, " +
            $"NonNull={nonNullEntries}, UniqueIds={uniqueIds}");
    }

    private static void AddCleanupChecks(
        ICollection<Check> checks)
    {
        string emotion =
            ReadSource(EmotionEffectPath);

        bool augmentCleanup =
            emotion.Contains(
                "battleEvent.OnBattleEnded += OnBattleEnded",
                StringComparison.Ordinal) &&
            emotion.Contains(
                "private void OnBattleEnded()",
                StringComparison.Ordinal) &&
            emotion.Contains(
                "RemoveOwnedStatuses();",
                StringComparison.Ordinal) &&
            emotion.Contains(
                "public override void OnUnregister()",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-X01",
            "Augment-owned infinite statuses clean up at battle end",
            augmentCleanup,
            augmentCleanup
                ? "OnBattleEnded / OnUnregister -> RemoveOwnedStatuses"
                : "Infinite augment cleanup path missing");

        string character =
            ReadSource(CharacterPath);

        bool runtimeCleanup =
            character.Contains(
                "statusController?.ClearAll(",
                StringComparison.Ordinal) &&
            character.Contains(
                "ShutdownRuntime(",
                StringComparison.Ordinal) &&
            character.Contains(
                "eventBinder.UnbindAll();",
                StringComparison.Ordinal);

        Add(
            checks,
            "P12-X02",
            "Character runtime shutdown clears status/event state",
            runtimeCleanup,
            runtimeCleanup
                ? "ClearAll + UnbindAll"
                : "Runtime shutdown cleanup path incomplete");
    }

    private static void AddReportCheck(
        ICollection<Check> checks,
        string id,
        string name,
        string relativePath,
        string expectedToken)
    {
        string root =
            Path.GetDirectoryName(
                Application.dataPath) ??
            string.Empty;

        string path =
            Path.Combine(
                root,
                relativePath);

        bool exists =
            File.Exists(path);

        string report =
            exists
                ? File.ReadAllText(path)
                : string.Empty;

        bool passed =
            exists &&
            report.Contains(
                expectedToken,
                StringComparison.Ordinal);

        Add(
            checks,
            id,
            name,
            passed,
            !exists
                ? "Report missing: " + relativePath
                : passed
                    ? expectedToken
                    : "Result token missing: " + expectedToken);
    }

    private static string ReadSource(
        string assetPath)
    {
        string root =
            Path.GetDirectoryName(
                Application.dataPath) ??
            string.Empty;

        string absolute =
            Path.Combine(
                root,
                assetPath);

        return File.Exists(absolute)
            ? File.ReadAllText(absolute)
            : string.Empty;
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
                Actual = actual
            });
    }

    private static void WriteReport(
        IReadOnlyList<Check> checks,
        int pass,
        int fail,
        string result)
    {
        string root =
            Path.GetDirectoryName(
                Application.dataPath) ??
            string.Empty;

        string path =
            Path.Combine(
                root,
                ReportPath);

        Directory.CreateDirectory(
            Path.GetDirectoryName(path) ??
            root);

        using StreamWriter writer =
            new StreamWriter(
                path,
                false);

        writer.WriteLine(
            "# 0922 Phase 12 — Event / VFX / Legacy Cleanup / Regression");
        writer.WriteLine();
        writer.WriteLine($"- PASS: {pass}");
        writer.WriteLine($"- FAIL: {fail}");
        writer.WriteLine($"- RESULT: {result}");
        writer.WriteLine();
        writer.WriteLine("## Checks");
        writer.WriteLine();

        foreach (Check check in checks)
        {
            writer.WriteLine(
                $"- [{(check.Passed ? "x" : " ")}] " +
                $"`{check.Id}` {check.Name} — {check.Actual}");
        }

        writer.WriteLine();
        writer.WriteLine(
            fail == 0
                ? "PHASE12_RESULT=PASS_CLEANUP_REGRESSION_0922"
                : "PHASE12_RESULT=FAIL");

        Debug.Log(
            "[0922 Phase12] Report written: " +
            path);
    }
}
#endif
