#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies the 0918 analysis-button hotfix against the patched Runner source.
/// This intentionally checks the regression mechanism itself rather than battle balance/content.
/// </summary>
public static class BattleAnalysisButtons0918HotfixVerification
{
    private const string RunnerPath =
        "Assets/1. Scripts/Runtime/Diagnostics/Simulation/BattleBatchSimulationRunner.cs";

    [MenuItem("Game System Verification/Battle Analysis/Verify 0918 Analysis Button Hotfix")]
    public static void VerifyFromMenu()
    {
        List<string> pass = new();
        List<string> fail = new();
        List<string> details = new();

        if (!File.Exists(RunnerPath))
        {
            Debug.LogError(
                "[0918 Analysis Button Hotfix Verification]\nPASS=0 FAIL=1\n\nFAIL\n- Runner source missing");
            return;
        }

        string source = File.ReadAllText(RunnerPath);

        bool bindGuard =
            source.Contains(
                "[0918_ANALYSIS_BUTTON_HOTFIX:NO_PERSISTENT_LOCAL_BIND]",
                StringComparison.Ordinal) &&
            source.Contains(
                "BindSceneLocalAnalysisControls();",
                StringComparison.Ordinal) &&
            source.Contains(
                "persistentOverlayRoot == null &&",
                StringComparison.Ordinal) &&
            source.Contains(
                "persistentPanelToggle == null",
                StringComparison.Ordinal);

        bool localBinder =
            source.Contains(
                "[0918_ANALYSIS_BUTTON_HOTFIX:SCENE_LOCAL_BIND]",
                StringComparison.Ordinal) &&
            source.Contains(
                "FindObjectsByType<BattleAnalysisDebugPanel>",
                StringComparison.Ordinal) &&
            source.Contains(
                "RegisterPanel(candidate);",
                StringComparison.Ordinal);

        bool disableGuard =
            source.Contains(
                "[0918_ANALYSIS_BUTTON_HOTFIX:GUARD_DISABLE]",
                StringComparison.Ordinal);

        string bindBlock = ExtractMethodBlock(
            source,
            "private IEnumerator BindPanelNextFrame()",
            "private void EnsurePersistentControlOverlay()");

        bool noUnconditionalDisable =
            bindBlock.Contains(
                "if (persistentOverlayRoot == null &&",
                StringComparison.Ordinal) &&
            bindBlock.IndexOf(
                "BindSceneLocalAnalysisControls();",
                StringComparison.Ordinal) <
            bindBlock.IndexOf(
                "DisableSceneLocalAnalysisControls();",
                StringComparison.Ordinal);

        if (bindGuard && localBinder && disableGuard && noUnconditionalDisable)
        {
            pass.Add(
                "Scene-local 승률/피해량 분석 UI가 persistent overlay 부재 시 유지되고 Runner에 재등록됨");
        }
        else
        {
            fail.Add(
                "BattleBatchSimulationRunner scene-local analysis control regression fix incomplete");
        }

        details.Add(
            $"bindGuard={bindGuard}, localBinder={localBinder}, disableGuard={disableGuard}, noUnconditionalDisable={noUnconditionalDisable}");

        if (Application.isPlaying)
        {
            BattleBatchSimulationRunner runner =
                BattleBatchSimulationRunner.Instance ??
                UnityEngine.Object.FindFirstObjectByType<BattleBatchSimulationRunner>(
                    FindObjectsInactive.Include);

            BattleAnalysisDebugPanel panel =
                UnityEngine.Object.FindFirstObjectByType<BattleAnalysisDebugPanel>(
                    FindObjectsInactive.Include);

            details.Add(
                $"PlayMode runtime: runner={(runner != null ? "FOUND" : "NULL")}, scenePanel={(panel != null ? "FOUND" : "NULL")}, running={(runner != null && runner.IsRunning)}");
        }

        string summary =
            "[0918 Analysis Button Hotfix Verification]\n" +
            $"PASS={pass.Count} FAIL={fail.Count}\n\n" +
            "PASS\n- " + string.Join("\n- ", pass) +
            "\n\nDETAILS\n- " + string.Join("\n- ", details) +
            "\n\nFAIL\n- " + string.Join("\n- ", fail);

        if (fail.Count > 0)
            Debug.LogError(summary);
        else
            Debug.Log(summary);
    }

    private static string ExtractMethodBlock(
        string source,
        string startToken,
        string endToken)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        int start = source.IndexOf(startToken, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        int end = source.IndexOf(endToken, start, StringComparison.Ordinal);
        if (end < 0)
            end = source.Length;

        return source.Substring(start, end - start);
    }
}
#endif
