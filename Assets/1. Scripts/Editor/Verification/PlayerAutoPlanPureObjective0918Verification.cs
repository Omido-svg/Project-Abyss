#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0918 Pure Objective AutoPlan source-contract verification.
/// AutoPlan의 목적함수에 Character Advisor/고정 utility heuristic이 다시 섞이는 회귀를 막는다.
/// </summary>
public static class PlayerAutoPlanPureObjective0918Verification
{
    private const string ServicePath =
        "Assets/1. Scripts/Runtime/Systems/AI/PlayerAutoPlanService.cs";

    [MenuItem("Game System Verification/Battle Auto Plan/Verify 0918 Pure Objective AutoPlan")]
    public static void VerifyFromMenu()
    {
        List<string> pass = new();
        List<string> fail = new();

        if (!File.Exists(ServicePath))
        {
            Debug.LogError(
                "[0918 Pure Objective AutoPlan Verification] FAIL\n" +
                "PlayerAutoPlanService.cs not found.");
            return;
        }

        string source =
            File.ReadAllText(ServicePath)
                .Replace("\r\n", "\n");

        Check(
            source.Contains(
                "[0918_PURE_AUTOPLAN_OBJECTIVE]",
                StringComparison.Ordinal),
            "Pure objective marker present",
            pass,
            fail);

        Check(
            source.Contains(
                "BuildPureObjectivePlan(",
                StringComparison.Ordinal) &&
            source.Contains(
                "SearchPureObjectivePlan(",
                StringComparison.Ordinal),
            "Global plan combination search present",
            pass,
            fail);

        Check(
            !source.Contains(
                "CharacterAutoPlanAdvisorRegistry",
                StringComparison.Ordinal),
            "Character advisor scoring/mutation removed from PlayerAutoPlanService",
            pass,
            fail);

        Check(
            !source.Contains(
                "FindBestMandatoryUtilityCandidate",
                StringComparison.Ordinal) &&
            !source.Contains("18000f", StringComparison.Ordinal) &&
            !source.Contains("14000f", StringComparison.Ordinal),
            "Mandatory preparation fixed-score heuristic removed",
            pass,
            fail);

        Check(
            !source.Contains("50000f", StringComparison.Ordinal) &&
            !source.Contains("40000f", StringComparison.Ordinal) &&
            !source.Contains("ExpectedDamage * 450f", StringComparison.Ordinal) &&
            !source.Contains("WinRate * 10000f", StringComparison.Ordinal),
            "Legacy mixed weighted objective removed",
            pass,
            fail);

        Check(
            source.Contains(
                "preparationAutoPick=OFF",
                StringComparison.Ordinal) &&
            source.Contains(
                "skill?.DefaultPhase != ActionPhase.COMBAT",
                StringComparison.Ordinal),
            "Unmodeled preparation auto-pick disabled",
            pass,
            fail);

        Check(
            source.Contains(
                "averageWinRate /=\n                totalThreatCount;",
                StringComparison.Ordinal),
            "Win-rate summary uses all enemy clash threats as denominator",
            pass,
            fail);

        string summary =
            "[0918 Pure Objective AutoPlan Verification]\n" +
            $"PASS={pass.Count} FAIL={fail.Count}\n\n" +
            "PASS\n- " + string.Join("\n- ", pass) +
            "\n\nFAIL\n- " + string.Join("\n- ", fail);

        if (fail.Count > 0)
            Debug.LogError(summary);
        else
            Debug.Log(summary);
    }

    private static void Check(
        bool condition,
        string label,
        List<string> pass,
        List<string> fail)
    {
        if (condition)
            pass.Add(label);
        else
            fail.Add(label);
    }
}
#endif
