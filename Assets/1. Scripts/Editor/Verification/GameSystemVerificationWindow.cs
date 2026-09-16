#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class GameSystemVerificationWindow : EditorWindow
{
    private BattleManager battleManager;
    private GameSystemVerificationReport report;
    private Vector2 scroll;
    private bool showPassed;
    private string lastOutputDirectory;

    [MenuItem("Tools/Project Abyss/Verification/Game System Verification")]
    public static void Open()
    {
        GameSystemVerificationWindow window = GetWindow<GameSystemVerificationWindow>();
        window.titleContent = new GUIContent("Game Verification");
        window.minSize = new Vector2(800f, 520f);
        window.Show();
    }

    private void OnEnable() => ResolveBattleManager();

    private void OnGUI()
    {
        DrawHeader();
        EditorGUILayout.Space(6f);
        DrawCoverage();
        EditorGUILayout.Space(6f);
        DrawControls();
        EditorGUILayout.Space(8f);
        DrawReport();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Project Abyss · Game System Verification", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Phase A/B는 기존 0915 검증을 회귀로 유지하고, Phase C는 0916 Progression/Run 정본(C-34/35/36/37/39)을 검증합니다. " +
            "Runtime 설정값을 기대값으로 재사용하지 않으며 Isolated Runtime은 Production BattleRuntimeFactory를 그대로 사용합니다.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Spec: {CanonicalGameSystemVerificationSpec.SpecId}", GUILayout.Width(120f));
            EditorGUILayout.LabelField(Application.isPlaying ? "Mode: PLAY" : "Mode: EDIT");
        }

        battleManager = (BattleManager)EditorGUILayout.ObjectField(
            "Live BattleManager", battleManager, typeof(BattleManager), true);
    }

    private void DrawCoverage()
    {
        IReadOnlyList<GameSystemVerificationCase> cases = GameSystemVerificationRunner.DiscoverCases();
        IReadOnlyDictionary<string, int> coverage = GameSystemVerificationModuleRegistry.CountRequirementCoverage(cases);
        int phaseACovered = CanonicalGameSystemVerificationSpec.PhaseARequirements.Count(id => coverage.ContainsKey(id));
        int phaseBCovered = CanonicalGameSystemVerificationSpec.PhaseBRequirements.Count(id => coverage.ContainsKey(id));
        int phaseCCovered = CanonicalGameSystemVerificationSpec.PhaseCRequirements.Count(id => coverage.ContainsKey(id));

        EditorGUILayout.LabelField("Requirement Coverage", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"Phase A: {phaseACovered}/{CanonicalGameSystemVerificationSpec.PhaseARequirements.Count}   " +
            $"Phase B: {phaseBCovered}/{CanonicalGameSystemVerificationSpec.PhaseBRequirements.Count}   " +
            $"Phase C: {phaseCCovered}/{CanonicalGameSystemVerificationSpec.PhaseCRequirements.Count}");

        List<string> missingB = CanonicalGameSystemVerificationSpec.PhaseBRequirements
            .Where(id => !coverage.ContainsKey(id)).ToList();
        if (missingB.Count > 0)
            EditorGUILayout.HelpBox("Phase B verification missing: " + string.Join(", ", missingB), MessageType.Warning);

        List<string> missingC = CanonicalGameSystemVerificationSpec.PhaseCRequirements
            .Where(id => !coverage.ContainsKey(id)).ToList();
        if (missingC.Count > 0)
            EditorGUILayout.HelpBox("Phase C verification missing: " + string.Join(", ", missingC), MessageType.Warning);
    }

    private void DrawControls()
    {
        EditorGUILayout.LabelField("Run", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Static Contracts", GUILayout.Height(30f)))
                Run(GameSystemVerificationRunner.RunContracts());

            bool canRunRuntime = Application.isPlaying && battleManager != null && battleManager.IsInitialized;
            using (new EditorGUI.DisabledScope(!canRunRuntime))
            {
                if (GUILayout.Button("Phase A Gate", GUILayout.Height(30f)))
                    Run(GameSystemVerificationRunner.RunPhaseA(battleManager));
                if (GUILayout.Button("Phase B Gate", GUILayout.Height(30f)))
                    Run(GameSystemVerificationRunner.RunPhaseB(battleManager));
                if (GUILayout.Button("Phase C Gate", GUILayout.Height(30f)))
                    Run(GameSystemVerificationRunner.RunPhaseC(battleManager));
                if (GUILayout.Button("Live Audit", GUILayout.Height(30f)))
                    Run(GameSystemVerificationRunner.RunLivePlanAudit(battleManager));
            }

            if (GUILayout.Button("Find Manager", GUILayout.Height(30f), GUILayout.Width(110f)))
                ResolveBattleManager();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            showPassed = EditorGUILayout.ToggleLeft("Show PASS", showPassed, GUILayout.Width(100f));
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(lastOutputDirectory)))
            {
                if (GUILayout.Button("Open Latest Report Folder", GUILayout.Width(180f)))
                    EditorUtility.RevealInFinder(lastOutputDirectory);
            }
        }
    }

    private void DrawReport()
    {
        if (report == null)
        {
            EditorGUILayout.HelpBox("아직 실행한 Verification Report가 없습니다.", MessageType.None);
            return;
        }

        report.RecalculateCounts();
        EditorGUILayout.HelpBox(
            $"Result={(report.Succeeded ? "PASSED" : "NOT CLOSED")}  " +
            $"PASS {report.PassCount} / FAIL {report.FailCount} / SKIP {report.SkipCount} / " +
            $"PENDING {report.PendingCount} / ERROR {report.ErrorCount}",
            report.Succeeded ? MessageType.Info : MessageType.Warning);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (report.Results != null)
        {
            foreach (GameSystemVerificationCaseResult result in report.Results)
            {
                if (result == null || (!showPassed && result.Status == GameSystemVerificationStatus.Pass))
                    continue;
                DrawResult(result);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private static void DrawResult(GameSystemVerificationCaseResult result)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"[{result.Status}] {result.CaseId}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(result.DisplayName ?? string.Empty);
            EditorGUILayout.LabelField(
                $"Module={result.ModuleId}  Requirement={result.RequirementId}  Mode={result.ExecutionMode}  " +
                $"Category={result.Category}  Required={result.Required}");
            if (!string.IsNullOrWhiteSpace(result.Expected)) EditorGUILayout.LabelField("Expected", result.Expected);
            if (!string.IsNullOrWhiteSpace(result.Actual)) EditorGUILayout.LabelField("Actual", result.Actual);
            if (!string.IsNullOrWhiteSpace(result.Details))
            {
                EditorGUILayout.LabelField("Details");
                EditorGUILayout.SelectableLabel(result.Details, EditorStyles.textArea, GUILayout.MinHeight(34f));
            }
        }
    }

    private void Run(GameSystemVerificationReport newReport)
    {
        report = newReport;
        lastOutputDirectory = GameSystemVerificationReportWriter.Write(report);
        Repaint();
    }

    private void ResolveBattleManager() => battleManager = Object.FindFirstObjectByType<BattleManager>();
}
#endif
