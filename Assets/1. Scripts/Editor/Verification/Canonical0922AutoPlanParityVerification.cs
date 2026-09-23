#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0922 canonical AutoPlan parity regression gate.
///
/// Scope:
/// - speed-gap judgment modifier parity
/// - Whole HP expectation on normal/weakened part targeting
/// - Chinchiro forced outcome + forced judgment/failure prediction path
///
/// This gate does not mutate battle state.
/// </summary>
public static class Canonical0922AutoPlanParityVerification
{
    private const string EstimatorPath =
        "Assets/1. Scripts/Runtime/Systems/AI/PlayerAutoPlanEstimator.cs";

    private const string ServicePath =
        "Assets/1. Scripts/Runtime/Systems/AI/PlayerAutoPlanService.cs";

    private const string ReportPath =
        "Logs/GameSystemVerification/0922_AutoPlanParity.md";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem(
        "Game System Verification/0922 Canonical/" +
        "AutoPlan - Verify Canonical Parity")]
    public static void VerifyFromMenu()
    {
        List<Check> checks =
            new List<Check>();

        AddSpeedChecks(checks);
        AddWholeHpChecks(checks);
        AddSourceIntegrationChecks(checks);

        int pass = 0;

        foreach (Check check in checks)
        {
            if (check.Passed)
                pass++;
        }

        int fail =
            checks.Count - pass;

        string result =
            fail == 0
                ? "PASS_0922_AUTOPLAN_PARITY"
                : "FAIL";

        WriteReport(
            checks,
            pass,
            fail,
            result);

        StringBuilder summary =
            new StringBuilder();

        summary.AppendLine(
            "[0922 AutoPlan Canonical Parity]");
        summary.AppendLine(
            $"PASS={pass} FAIL={fail}");
        summary.AppendLine();

        summary.AppendLine("PASS");
        foreach (Check check in checks)
        {
            if (check.Passed)
            {
                summary.AppendLine(
                    $"- {check.Id} | {check.Name} | {check.Actual}");
            }
        }

        summary.AppendLine();
        summary.AppendLine("FAIL");

        bool anyFail = false;
        foreach (Check check in checks)
        {
            if (check.Passed)
                continue;

            anyFail = true;
            summary.AppendLine(
                $"- {check.Id} | {check.Name} | {check.Actual}");
        }

        if (!anyFail)
            summary.AppendLine("- NONE");

        summary.AppendLine();
        summary.AppendLine(
            "AUTOPLAN_PARITY_RESULT=" +
            result);

        if (fail == 0)
            Debug.Log(summary.ToString());
        else
            Debug.LogError(summary.ToString());
    }

    private static void AddSpeedChecks(
        ICollection<Check> checks)
    {
        MethodInfo method =
            typeof(PlayerAutoPlanEstimator)
                .GetMethod(
                    "ResolveCanonicalSpeedModifier",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

        if (method == null)
        {
            Add(
                checks,
                "AP-S00",
                "Canonical speed modifier helper exists",
                false,
                "method missing");
            return;
        }

        int equal =
            InvokeInt(
                method,
                4,
                4,
                1);

        int gap1 =
            InvokeInt(
                method,
                5,
                4,
                1);

        int gap5 =
            InvokeInt(
                method,
                9,
                4,
                1);

        int gap6 =
            InvokeInt(
                method,
                10,
                4,
                1);

        int disabled =
            InvokeInt(
                method,
                10,
                4,
                0);

        Add(
            checks,
            "AP-S01",
            "Speed gap 1~5 => +1",
            gap1 == 1 &&
            gap5 == 1,
            $"gap1={gap1}, gap5={gap5}");

        Add(
            checks,
            "AP-S02",
            "Speed gap 6+ => +2",
            gap6 == 2,
            $"gap6={gap6}");

        Add(
            checks,
            "AP-S03",
            "No bonus for tie/disabled speed weighting",
            equal == 0 &&
            disabled == 0,
            $"tie={equal}, disabled={disabled}");
    }

    private static void AddWholeHpChecks(
        ICollection<Check> checks)
    {
        MethodInfo method =
            typeof(PlayerAutoPlanEstimator)
                .GetMethod(
                    "LimitWholeHpExpectedDamage",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);

        if (method == null)
        {
            Add(
                checks,
                "AP-D00",
                "Whole HP damage limiter exists",
                false,
                "method missing");
            return;
        }

        float normal =
            InvokeFloat(
                method,
                10f,
                100,
                0);

        float guarded =
            InvokeFloat(
                method,
                10f,
                100,
                2);

        float lethalCap =
            InvokeFloat(
                method,
                10f,
                5,
                0);

        Add(
            checks,
            "AP-D01",
            "Expected HP damage is not part-HP clamped",
            Mathf.Approximately(
                normal,
                10f),
            $"damage10/hp100={normal:0.###}");

        Add(
            checks,
            "AP-D02",
            "Guard is applied once at aggregate HP layer",
            Mathf.Approximately(
                guarded,
                8f),
            $"damage10/guard2={guarded:0.###}");

        Add(
            checks,
            "AP-D03",
            "Expected HP damage is capped only by current Whole HP",
            Mathf.Approximately(
                lethalCap,
                5f),
            $"damage10/hp5={lethalCap:0.###}");
    }

    private static void AddSourceIntegrationChecks(
        ICollection<Check> checks)
    {
        string estimator =
            ReadProjectText(
                EstimatorPath);

        string service =
            ReadProjectText(
                ServicePath);

        bool speedOldPathGone =
            !estimator.Contains(
                "selfSpeed - opponentSpeed >= 6 &&",
                StringComparison.Ordinal);

        Add(
            checks,
            "AP-I01",
            "Estimator no longer carries the old +0/+1 speed formula",
            speedOldPathGone,
            speedOldPathGone
                ? "old formula absent"
                : "old formula still present");

        bool weakenedZeroGone =
            !estimator.Contains(
                "if (targetPart.IsWeakened)\n            return 0f;",
                StringComparison.Ordinal) &&
            !service.Contains(
                "targetPart?.IsWeakened == true",
                StringComparison.Ordinal);

        Add(
            checks,
            "AP-I02",
            "Weakened target is not forced to ExpectedDamage=0",
            weakenedZeroGone,
            weakenedZeroGone
                ? "weakened zero path absent"
                : "legacy weakened zero path remains");

        bool forcedOutcomeWired =
            estimator.Contains(
                "ChinchiroOutcomeOverrideResolver.TryResolve",
                StringComparison.Ordinal) &&
            estimator.Contains(
                "BuildHifumiChinchiroOutcomes",
                StringComparison.Ordinal) &&
            estimator.Contains(
                "ResolveForcedAwareComparison",
                StringComparison.Ordinal) &&
            estimator.Contains(
                "ResolveForcedRollFailure",
                StringComparison.Ordinal);

        Add(
            checks,
            "AP-I03",
            "Chinchiro prediction consumes live override/judgment/failure contracts",
            forcedOutcomeWired,
            forcedOutcomeWired
                ? "override + forced judgment/failure wired"
                : "forced Chinchiro prediction path incomplete");

        bool hifumiPureBuilder =
            estimator.Contains(
                "HifumiChinchiroRuntime",
                StringComparison.Ordinal) &&
            estimator.Contains(
                "BuildResultForVerification",
                StringComparison.Ordinal);

        Add(
            checks,
            "AP-I04",
            "Hifumi prediction uses canonical pure Chinchiro builder",
            hifumiPureBuilder,
            hifumiPureBuilder
                ? "canonical Hifumi distribution builder reused"
                : "Hifumi canonical builder not reused");
    }

    private static int InvokeInt(
        MethodInfo method,
        params object[] args)
    {
        object value =
            method?.Invoke(
                null,
                args);

        return value is int result
            ? result
            : int.MinValue;
    }

    private static float InvokeFloat(
        MethodInfo method,
        params object[] args)
    {
        object value =
            method?.Invoke(
                null,
                args);

        return value is float result
            ? result
            : float.NaN;
    }

    private static string ReadProjectText(
        string projectRelativePath)
    {
        string projectRoot =
            Directory.GetParent(
                Application.dataPath)?.FullName ??
            Directory.GetCurrentDirectory();

        string fullPath =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    projectRelativePath));

        return File.Exists(fullPath)
            ? File.ReadAllText(fullPath)
            : string.Empty;
    }

    private static void WriteReport(
        IReadOnlyList<Check> checks,
        int pass,
        int fail,
        string result)
    {
        string projectRoot =
            Directory.GetParent(
                Application.dataPath)?.FullName ??
            Directory.GetCurrentDirectory();

        string fullPath =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    ReportPath));

        string directory =
            Path.GetDirectoryName(
                fullPath);

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        StringBuilder writer =
            new StringBuilder();

        writer.AppendLine(
            "# 0922 AutoPlan Canonical Parity");
        writer.AppendLine();
        writer.AppendLine(
            $"- PASS: {pass}");
        writer.AppendLine(
            $"- FAIL: {fail}");
        writer.AppendLine(
            $"- RESULT: {result}");
        writer.AppendLine();

        foreach (Check check in checks)
        {
            writer.AppendLine(
                $"- [{(check.Passed ? "x" : " ")}] " +
                $"`{check.Id}` {check.Name} — {check.Actual}");
        }

        writer.AppendLine();
        writer.AppendLine(
            "AUTOPLAN_PARITY_RESULT=" +
            result);

        File.WriteAllText(
            fullPath,
            writer.ToString(),
            new UTF8Encoding(false));

        Debug.Log(
            "[0922 AutoPlanParity] Report written: " +
            fullPath);
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
}
#endif
