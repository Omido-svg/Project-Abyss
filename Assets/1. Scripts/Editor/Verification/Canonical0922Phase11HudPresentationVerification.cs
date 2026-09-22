#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Phase11HudPresentationVerification
{
    private const string Phase10ReportPath =
        "Logs/GameSystemVerification/0922_Phase10_EmotionAugment.md";
    private const string ReportPath =
        "Logs/GameSystemVerification/0922_Phase11_HudPresentation.md";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 11 - Verify HUD Presentation")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new();

        AddPrerequisites(checks);
        AddNumericChecks(checks);
        AddPresenceChecks(checks);
        AddBespokeChecks(checks);
        AddProjectionPurityChecks(checks);
        AddMotionChecks(checks);

        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;
        string result = fail == 0
            ? "PASS_HUD_PRESENTATION_0922"
            : "FAIL";

        WriteReport(checks, pass, fail, result);

        string summary =
            "[0922 Phase 11 · HUD / Presentation]\n" +
            $"PASS={pass} FAIL={fail}\n\n" +
            "PASS\n- " +
            string.Join("\n- ", checks.Where(x => x.Passed)
                .Select(x => $"{x.Id} | {x.Name} | {x.Actual}")) +
            "\n\nFAIL\n- " +
            (fail == 0
                ? "NONE"
                : string.Join("\n- ", checks.Where(x => !x.Passed)
                    .Select(x => $"{x.Id} | {x.Name} | {x.Actual}"))) +
            $"\n\nPHASE11_RESULT={result}";

        if (fail == 0) Debug.Log(summary);
        else Debug.LogError(summary);
    }

    private static void AddPrerequisites(List<Check> checks)
    {
        Add(checks, "P11-H00", "0922 canonical identity",
            Canonical0922BaselineSpec.CanonicalVersion == "0922",
            $"Canonical={Canonical0922BaselineSpec.CanonicalVersion}");

        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string path = Path.Combine(root, Phase10ReportPath);
        bool exists = File.Exists(path);
        string report = exists ? File.ReadAllText(path) : string.Empty;
        bool passed =
            report.Contains("PHASE10_RESULT=PASS_EMOTION_AUGMENT_0922", StringComparison.Ordinal) ||
            report.Contains("RESULT: PASS_EMOTION_AUGMENT_0922", StringComparison.Ordinal);

        Add(checks, "P11-H01", "Phase 10 Emotion Augment closed",
            exists && passed,
            !exists ? "Phase10 report missing" : $"ReportPresent=True, ResultPass={passed}");
    }

    private static void AddNumericChecks(List<Check> checks)
    {
        List<StatusEffect> numeric = new()
        {
            new StrengthStatus(5, 1),
            new StrengthStatus(5, 3)
        };

        BattleStatusChipModel model =
            BattleStatusPresentationProjector.BuildFromEffects(null, numeric)
                .SingleOrDefault(x => x.Representative is StrengthStatus);

        bool aggregate =
            model != null &&
            model.PresentationKind == BattleStatusPresentationKind.NumericTimed &&
            model.EntryCount == 2 &&
            model.CurrentTotalValue == 10 &&
            model.NextTurnProjectedValue == 5 &&
            model.MaxRemainingDuration == 3 &&
            model.HasProjectionChange;

        Add(checks, "P11-N01", "Numeric entries aggregate and project next turn",
            aggregate,
            model == null
                ? "MODEL_MISSING"
                : $"Current={model.CurrentTotalValue}, Next={model.NextTurnProjectedValue}, MaxT={model.MaxRemainingDuration}, Entries={model.EntryCount}");

        List<StatusEffect> infinite = new()
        {
            new StrengthStatus(2, StatusEffect.InfiniteDuration),
            new StrengthStatus(1, 2)
        };

        BattleStatusChipModel inf =
            BattleStatusPresentationProjector.BuildFromEffects(null, infinite)
                .SingleOrDefault(x => x.Representative is StrengthStatus);

        string infText =
            inf != null
                ? BattleStatusUiText.BuildChipLabel(inf, projected: false, reducedMotion: false)
                : string.Empty;

        bool infiniteDisplay =
            inf != null &&
            inf.CurrentTotalValue == 3 &&
            inf.NextTurnProjectedValue == 3 &&
            inf.HasInfiniteDuration &&
            inf.MaxRemainingDuration == StatusEffect.InfiniteDuration &&
            infText.Contains("∞", StringComparison.Ordinal);

        Add(checks, "P11-N02", "Finite + infinite Numeric chip shows ∞",
            infiniteDisplay,
            inf == null
                ? "MODEL_MISSING"
                : $"Current={inf.CurrentTotalValue}, Next={inf.NextTurnProjectedValue}, Duration={(inf.HasInfiniteDuration ? "∞" : inf.MaxRemainingDuration.ToString())}");
    }

    private static void AddPresenceChecks(List<Check> checks)
    {
        List<StatusEffect> effects = new()
        {
            new PainStatus(1)
        };

        BattleStatusChipModel pain =
            BattleStatusPresentationProjector.BuildFromEffects(null, effects)
                .SingleOrDefault();

        string current = pain != null
            ? BattleStatusUiText.BuildChipLabel(pain, projected: false, reducedMotion: false)
            : string.Empty;
        string next = pain != null
            ? BattleStatusUiText.BuildChipLabel(pain, projected: true, reducedMotion: false)
            : string.Empty;

        bool presence =
            pain != null &&
            pain.PresentationKind == BattleStatusPresentationKind.PresenceTimed &&
            pain.CurrentTotalValue == 0 &&
            pain.NextTurnProjectedValue == 0 &&
            pain.CurrentActive &&
            !pain.NextTurnActive &&
            !current.Contains("×1", StringComparison.Ordinal) &&
            next.Contains("다음 턴 해제", StringComparison.Ordinal);

        Add(checks, "P11-P01", "Presence chip has T only and no fake N",
            presence,
            pain == null
                ? "MODEL_MISSING"
                : $"Active={pain.CurrentActive}->{pain.NextTurnActive}, TextHasFakeN={current.Contains("×1", StringComparison.Ordinal)}");
    }

    private static void AddBespokeChecks(List<Check> checks)
    {
        List<StatusEffect> effects = new()
        {
            new Bleeding(5)
        };

        BattleStatusChipModel bleeding =
            BattleStatusPresentationProjector.BuildFromEffects(null, effects)
                .SingleOrDefault();

        string text = bleeding != null
            ? BattleStatusUiText.BuildChipLabel(bleeding, projected: false, reducedMotion: false)
            : string.Empty;

        bool bespoke =
            bleeding != null &&
            bleeding.PresentationKind == BattleStatusPresentationKind.Bespoke &&
            bleeding.BespokeCurrentValue == 5 &&
            text.Contains("혈상", StringComparison.Ordinal) &&
            text.Contains("5", StringComparison.Ordinal);

        Add(checks, "P11-B01", "Bleeding uses bespoke stack presentation",
            bespoke,
            bleeding == null
                ? "MODEL_MISSING"
                : $"Current={bleeding.BespokeCurrentValue}, Text={StripRichTextForLog(text)}");

        string hudSource = ReadSource(
            "Assets/1. Scripts/Runtime/Presentation/UI/Panels/CharacterMechanicHudPresenters.cs");

        bool markProgress =
            hudSource.Contains("YujinMechanic.MarkIgnitionThreshold", StringComparison.Ordinal) &&
            hudSource.Contains("mechanic?.GetMark(enemy, part)", StringComparison.Ordinal) &&
            hudSource.Contains("includeCharacterAnchorWhenNoParts: true", StringComparison.Ordinal) &&
            hudSource.Contains("int maxValue = 0", StringComparison.Ordinal) &&
            hudSource.Contains("builder.Append('/')", StringComparison.Ordinal);

        Add(checks, "P11-B02", "Yujin Mark HUD renders current / 44 threshold",
            markProgress,
            markProgress ? "Mark value / MarkIgnitionThreshold" : "threshold presentation missing");
    }

    private static void AddProjectionPurityChecks(List<Check> checks)
    {
        StrengthStatus first = new(4, 1);
        StrengthStatus second = new(7, 3);
        int firstStack = first.Stack;
        int firstDuration = first.Duration;
        int secondStack = second.Stack;
        int secondDuration = second.Duration;

        BattleStatusPresentationProjector.BuildFromEffects(
            null,
            new StatusEffect[] { first, second });

        bool pure =
            first.Stack == firstStack &&
            first.Duration == firstDuration &&
            second.Stack == secondStack &&
            second.Duration == secondDuration;

        Add(checks, "P11-X01", "Projection is pure and does not mutate runtime status",
            pure,
            $"A={first.Stack}/{first.Duration}, B={second.Stack}/{second.Duration}");
    }

    private static void AddMotionChecks(List<Check> checks)
    {
        List<StatusEffect> effects = new()
        {
            new StrengthStatus(5, 1),
            new StrengthStatus(5, 3)
        };

        BattleStatusChipModel model =
            BattleStatusPresentationProjector.BuildFromEffects(null, effects)
                .Single();
        string reduced = BattleStatusUiText.BuildReducedMotionChipLabel(model);

        bool reducedText =
            reduced.Contains("10 → 5", StringComparison.Ordinal);

        Add(checks, "P11-M01", "Reduced motion shows static current -> next value",
            reducedText,
            StripRichTextForLog(reduced));

        string rowSource = ReadSource(
            "Assets/1. Scripts/Runtime/Presentation/UI/Components/BattleStatusRowUI.cs");
        string listSource = ReadSource(
            "Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleStatusListUI.cs");

        bool motionPolicy =
            rowSource.Contains("currentDwellSeconds = 1.35f", StringComparison.Ordinal) &&
            rowSource.Contains("projectedDwellSeconds = 0.65f", StringComparison.Ordinal) &&
            rowSource.Contains("reducedMotion || !boundModel.HasProjectionChange", StringComparison.Ordinal) &&
            listSource.Contains("i * phaseStaggerSeconds", StringComparison.Ordinal) &&
            listSource.Contains("BattleStatusPresentationProjector.Build(owner)", StringComparison.Ordinal);

        Add(checks, "P11-M02", "Change-only blink, current dwell priority and phase staggering are wired",
            motionPolicy,
            motionPolicy
                ? "change-only / current 1.35s > projected 0.65s / staggered rows"
                : "motion policy wiring missing");
    }

    private static string ReadSource(string assetPath)
    {
        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string absolute = Path.Combine(root, assetPath);
        return File.Exists(absolute) ? File.ReadAllText(absolute) : string.Empty;
    }

    private static string StripRichTextForLog(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("\n", " / ")
            .Replace("<b>", string.Empty)
            .Replace("</b>", string.Empty);
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
        int pass,
        int fail,
        string result)
    {
        string root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        string path = Path.Combine(root, ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);

        using StreamWriter writer = new(path, false);
        writer.WriteLine("# 0922 Phase 11 — HUD / Presentation");
        writer.WriteLine();
        writer.WriteLine($"- PASS: {pass}");
        writer.WriteLine($"- FAIL: {fail}");
        writer.WriteLine($"- RESULT: {result}");
        writer.WriteLine();
        writer.WriteLine("## Checks");
        writer.WriteLine();
        foreach (Check check in checks)
            writer.WriteLine($"- [{(check.Passed ? "x" : " ")}] `{check.Id}` {check.Name} — {check.Actual}");

        Debug.Log("[0922 Phase11] Report written: " + path);
    }
}
#endif
