#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0918 Battle Analysis button hotfix.
/// Baseline: Default-Battle-Test @ 2feb9aa4f67aede91bb8c3638be6039996f3d951 ("추가 누락본 패치").
///
/// Root cause:
/// BattleBatchSimulationRunner v4 intentionally stopped creating a persistent Game View overlay,
/// but BindPanelNextFrame still unconditionally called DisableSceneLocalAnalysisControls().
/// Therefore the only scene-local BattleAnalysisPanelToggle/DebugPanel controls were disabled
/// one frame after scene load. Win-rate / damage buttons could no longer receive clicks.
///
/// Fix:
/// - If no persistent overlay exists, KEEP scene-local analysis controls alive and register them.
/// - Disable scene-local controls only when a real persistent overlay exists.
/// - Add a guard to DisableSceneLocalAnalysisControls so future callers cannot reproduce the bug.
/// </summary>
public static class BattleAnalysisButtons0918HotfixMigration
{
    private const string RunnerPath =
        "Assets/1. Scripts/Runtime/Diagnostics/Simulation/BattleBatchSimulationRunner.cs";

    private const string BindMarker =
        "[0918_ANALYSIS_BUTTON_HOTFIX:NO_PERSISTENT_LOCAL_BIND]";

    private const string HelperMarker =
        "[0918_ANALYSIS_BUTTON_HOTFIX:SCENE_LOCAL_BIND]";

    private const string GuardMarker =
        "[0918_ANALYSIS_BUTTON_HOTFIX:GUARD_DISABLE]";

    [MenuItem("Game System Verification/Battle Analysis/Apply 0918 Analysis Button Hotfix")]
    public static void ApplyFromMenu()
    {
        if (!File.Exists(RunnerPath))
        {
            Debug.LogError(
                "[0918 Analysis Button Hotfix] FAIL\n" +
                "BattleBatchSimulationRunner.cs not found:\n" + RunnerPath);
            return;
        }

        string original = File.ReadAllText(RunnerPath);
        string source = Normalize(original);

        bool alreadyPatched =
            source.Contains(BindMarker, StringComparison.Ordinal) &&
            source.Contains(HelperMarker, StringComparison.Ordinal) &&
            source.Contains(GuardMarker, StringComparison.Ordinal);

        if (alreadyPatched)
        {
            Debug.Log(
                "[0918 Analysis Button Hotfix] PASS\n" +
                "Runner is already patched. No source changes required.");
            return;
        }

        string oldBind =
            "    private IEnumerator BindPanelNextFrame()\n" +
            "    {\n" +
            "        yield return null;\n" +
            "\n" +
            "        EnsurePersistentControlOverlay();\n" +
            "        DisableSceneLocalAnalysisControls();\n" +
            "\n" +
            "        if (persistentPanel == null &&\n" +
            "            persistentOverlayRoot != null)\n" +
            "        {\n" +
            "            persistentPanel =\n" +
            "                persistentOverlayRoot.GetComponentInChildren<\n" +
            "                    BattleAnalysisDebugPanel>(true);\n" +
            "        }\n" +
            "\n" +
            "        if (persistentPanel != null)\n" +
            "        {\n" +
            "            panel = persistentPanel;\n" +
            "            RegisterPanel(persistentPanel);\n" +
            "        }\n" +
            "    }";

        string newBind =
            "    private IEnumerator BindPanelNextFrame()\n" +
            "    {\n" +
            "        yield return null;\n" +
            "\n" +
            "        EnsurePersistentControlOverlay();\n" +
            "\n" +
            "        // " + BindMarker + "\n" +
            "        // v4는 persistent Game View overlay를 더 이상 만들지 않는다.\n" +
            "        // 이 상태에서 scene-local UI까지 끄면 승률/피해량 버튼 자체가 사라지거나 클릭 불능이 된다.\n" +
            "        // Persistent overlay가 실제로 없으면 Scene의 분석 패널을 유지하고 Runner에 다시 등록한다.\n" +
            "        if (persistentOverlayRoot == null &&\n" +
            "            persistentPanel == null &&\n" +
            "            persistentPanelToggle == null)\n" +
            "        {\n" +
            "            BindSceneLocalAnalysisControls();\n" +
            "            yield break;\n" +
            "        }\n" +
            "\n" +
            "        // Persistent overlay가 있는 구/호환 구성에서만 중복 Scene UI를 끈다.\n" +
            "        DisableSceneLocalAnalysisControls();\n" +
            "\n" +
            "        if (persistentPanel == null &&\n" +
            "            persistentOverlayRoot != null)\n" +
            "        {\n" +
            "            persistentPanel =\n" +
            "                persistentOverlayRoot.GetComponentInChildren<\n" +
            "                    BattleAnalysisDebugPanel>(true);\n" +
            "        }\n" +
            "\n" +
            "        if (persistentPanel != null)\n" +
            "        {\n" +
            "            panel = persistentPanel;\n" +
            "            RegisterPanel(persistentPanel);\n" +
            "        }\n" +
            "    }";

        if (!source.Contains(BindMarker, StringComparison.Ordinal))
        {
            if (!ReplaceUnique(ref source, oldBind, newBind, out string error))
            {
                Debug.LogError(
                    "[0918 Analysis Button Hotfix] FAIL\n" + error);
                return;
            }
        }

        string disableSignature =
            "    private void DisableSceneLocalAnalysisControls()\n" +
            "    {\n" +
            "        BattleAnalysisPanelToggle[] toggles =";

        string helperAndGuard =
            "    // " + HelperMarker + "\n" +
            "    private void BindSceneLocalAnalysisControls()\n" +
            "    {\n" +
            "        BattleAnalysisDebugPanel[] scenePanels =\n" +
            "            FindObjectsByType<BattleAnalysisDebugPanel>(\n" +
            "                FindObjectsInactive.Include,\n" +
            "                FindObjectsSortMode.None);\n" +
            "\n" +
            "        BattleAnalysisDebugPanel candidate = null;\n" +
            "\n" +
            "        foreach (BattleAnalysisDebugPanel debugPanel in scenePanels)\n" +
            "        {\n" +
            "            if (debugPanel == null ||\n" +
            "                debugPanel == persistentPanel)\n" +
            "            {\n" +
            "                continue;\n" +
            "            }\n" +
            "\n" +
            "            GameObject panelObject = debugPanel.gameObject;\n" +
            "            if (panelObject == null ||\n" +
            "                !panelObject.scene.IsValid())\n" +
            "            {\n" +
            "                continue;\n" +
            "            }\n" +
            "\n" +
            "            candidate = debugPanel;\n" +
            "\n" +
            "            // 숨김은 CanvasGroup으로 처리되므로 activeInHierarchy인 패널을 우선한다.\n" +
            "            if (panelObject.activeInHierarchy)\n" +
            "                break;\n" +
            "        }\n" +
            "\n" +
            "        if (candidate == null)\n" +
            "            return;\n" +
            "\n" +
            "        panel = candidate;\n" +
            "        RegisterPanel(candidate);\n" +
            "    }\n" +
            "\n" +
            "    private void DisableSceneLocalAnalysisControls()\n" +
            "    {\n" +
            "        // " + GuardMarker + "\n" +
            "        // Persistent overlay가 없으면 Scene UI가 유일한 조작 수단이므로 절대 끄지 않는다.\n" +
            "        if (persistentOverlayRoot == null &&\n" +
            "            persistentPanelToggle == null)\n" +
            "        {\n" +
            "            return;\n" +
            "        }\n" +
            "\n" +
            "        BattleAnalysisPanelToggle[] toggles =";

        if (!source.Contains(HelperMarker, StringComparison.Ordinal) ||
            !source.Contains(GuardMarker, StringComparison.Ordinal))
        {
            if (!ReplaceUnique(
                    ref source,
                    disableSignature,
                    helperAndGuard,
                    out string error))
            {
                Debug.LogError(
                    "[0918 Analysis Button Hotfix] FAIL\n" + error);
                return;
            }
        }

        if (string.Equals(Normalize(original), source, StringComparison.Ordinal))
        {
            Debug.LogError(
                "[0918 Analysis Button Hotfix] FAIL\n" +
                "No source change was produced.");
            return;
        }

        File.WriteAllText(RunnerPath, source);
        AssetDatabase.Refresh();

        Debug.Log(
            "[0918 Analysis Button Hotfix] PASS\n" +
            "BattleBatchSimulationRunner.cs patched.\n" +
            "Root cause fixed: scene-local analysis controls are no longer disabled when persistent overlay is absent.\n\n" +
            "Unity will recompile. After compilation, run:\n" +
            "Game System Verification > Battle Analysis > Verify 0918 Analysis Button Hotfix");
    }

    private static bool ReplaceUnique(
        ref string source,
        string oldText,
        string newText,
        out string error)
    {
        error = string.Empty;

        int first = source.IndexOf(oldText, StringComparison.Ordinal);
        if (first < 0)
        {
            error =
                "Patch anchor not found. The Runner source differs from the 2feb9aa baseline.";
            return false;
        }

        int second = source.IndexOf(
            oldText,
            first + oldText.Length,
            StringComparison.Ordinal);

        if (second >= 0)
        {
            error = "Patch anchor is ambiguous (multiple matches).";
            return false;
        }

        source =
            source.Substring(0, first) +
            newText +
            source.Substring(first + oldText.Length);

        return true;
    }

    private static string Normalize(string value) =>
        (value ?? string.Empty).Replace("\r\n", "\n");
}
#endif
