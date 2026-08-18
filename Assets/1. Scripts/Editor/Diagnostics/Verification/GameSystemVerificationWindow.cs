#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Character Verification과 분리된 전투 시스템 룰 검증 창.
/// 데이터 계약, 샌드박스 룰 Matrix, 현재 인게임 Planning 상태를 한 화면에서 확인한다.
/// </summary>
public sealed class GameSystemVerificationWindow :
    EditorWindow
{
    private Vector2 scroll;
    private GameSystemVerificationReport lastReport;

    [MenuItem(
        "Tools/Project Abyss/Game System Verification/Open Verification Window",
        false,
        130)]
    public static void Open()
    {
        GameSystemVerificationWindow window =
            GetWindow<GameSystemVerificationWindow>(
                "Game System Verification");

        window.minSize =
            new Vector2(
                820f,
                600f);

        window.Show();
    }

    [MenuItem(
        "Tools/Project Abyss/Game System Verification/Run Full System Coverage",
        false,
        131)]
    public static void RunFullMenu()
    {
        Open();

        GameSystemVerificationWindow window =
            GetWindow<GameSystemVerificationWindow>();

        window.RunFullCoverage();
    }

    [MenuItem(
        "Tools/Project Abyss/Game System Verification/Run Data Rule Coverage",
        false,
        132)]
    public static void RunDataMenu()
    {
        Open();

        GameSystemVerificationWindow window =
            GetWindow<GameSystemVerificationWindow>();

        window.RunDataCoverage();
    }

    public static void ShowReport(
        GameSystemVerificationReport report)
    {
        if (report == null)
            return;

        GameSystemVerificationWindow window =
            null;

        GameSystemVerificationWindow[] openWindows =
            Resources.FindObjectsOfTypeAll<
                GameSystemVerificationWindow>();

        if (openWindows != null &&
            openWindows.Length > 0)
        {
            window =
                openWindows[0];
        }

        if (window == null)
        {
            if (Application.isPlaying)
            {
                Debug.Log(
                    "[GameSystemVerification] 검증 Report가 생성되었습니다. " +
                    "결과 창이 닫혀 있어 Play Mode callback에서 새 EditorWindow를 만들지 않습니다.");
                return;
            }

            Open();
            window =
                GetWindow<GameSystemVerificationWindow>();
        }

        window.lastReport = report;

        // Full Coverage 완료 callback은 Play Mode의 EditorApplication.update에서 온다.
        // Report는 이미 disk에 저장되어 있고 Window도 다음 정상 GUI event에서 갱신된다.
        // 여기서 강제 Repaint를 호출하면 Scene이 참조하는 runtime transient object에 대해
        // Editor layout/persistence 검사가 즉시 발생할 수 있으므로 Play Mode에서는 생략한다.
        if (!Application.isPlaying)
            window.Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawDescription();
        DrawActions();
        DrawReport();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(
            EditorStyles.toolbar);

        GUILayout.Label(
            "Project Abyss / Game System Coverage",
            EditorStyles.boldLabel);

        GUILayout.FlexibleSpace();

        GUILayout.Label(
            Application.isPlaying
                ? "PLAY MODE"
                : "EDIT MODE",
            EditorStyles.miniBoldLabel);

        EditorGUILayout.EndHorizontal();
    }

    private static void DrawDescription()
    {
        EditorGUILayout.Space(8f);

        EditorGUILayout.HelpBox(
            "캐릭터 개별 스킬이 아니라 게임 룰 자체를 검사합니다. " +
            "Focused Encounter exact TargetSlot, 원래 대상 부위 대응, strict > 가로채기, " +
            "일방공격 Pairing, 같은 부위 공유 속도, 도사림 계획/취소/큐, 현재 Live Plan 일관성을 검증합니다.\n\n" +
            "Full Coverage는 실제 Roster를 복제한 숨김 Sandbox를 사용하므로 현재 HP/에너지/ActionManager를 변경하지 않습니다. " +
            "현재 플레이어가 이미 행동을 지정한 Planning 중에 다시 실행하면 Live Plan 검사까지 포함됩니다.",
            MessageType.Info);
    }

    private void DrawActions()
    {
        EditorGUILayout.Space(6f);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Run Full System Coverage",
                GUILayout.Height(34f)))
        {
            RunFullCoverage();
        }

        if (GUILayout.Button(
                "Run Data Rule Coverage",
                GUILayout.Height(34f),
                GUILayout.Width(190f)))
        {
            RunDataCoverage();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(
                   !Application.isPlaying))
        {
            if (GUILayout.Button(
                    "Audit Current In-Game Plan",
                    GUILayout.Height(30f)))
            {
                RunCurrentPlanAudit();
            }
        }

        if (GUILayout.Button(
                "Open Latest Report Folder",
                GUILayout.Height(30f),
                GUILayout.Width(210f)))
        {
            RevealLatestReport();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawReport()
    {
        EditorGUILayout.Space(10f);

        if (lastReport == null)
        {
            EditorGUILayout.HelpBox(
                "아직 실행 결과가 없습니다.",
                MessageType.None);

            return;
        }

        lastReport.RecalculateCounts();

        EditorGUILayout.LabelField(
            lastReport.Succeeded
                ? "RESULT: PASSED"
                : "RESULT: FAILED",
            EditorStyles.largeLabel);

        EditorGUILayout.LabelField(
            $"Scene: {lastReport.SceneName}  |  Player: {lastReport.PlayerName}");

        EditorGUILayout.LabelField(
            $"PASS {lastReport.PassCount} / FAIL {lastReport.FailCount} / " +
            $"SKIP {lastReport.SkipCount} / ERROR {lastReport.ErrorCount} / " +
            $"TOTAL {lastReport.Results?.Count ?? 0}");

        if (!string.IsNullOrWhiteSpace(
                lastReport.OutputDirectory))
        {
            EditorGUILayout.SelectableLabel(
                lastReport.OutputDirectory,
                EditorStyles.textField,
                GUILayout.Height(18f));
        }

        EditorGUILayout.Space(6f);

        scroll =
            EditorGUILayout.BeginScrollView(
                scroll);

        if (lastReport.Results != null)
        {
            foreach (GameSystemVerificationCaseResult result
                     in lastReport.Results)
            {
                DrawCase(result);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawCase(
        GameSystemVerificationCaseResult result)
    {
        if (result == null)
            return;

        MessageType messageType =
            result.Status switch
            {
                GameSystemVerificationStatus.Pass => MessageType.Info,
                GameSystemVerificationStatus.Skip => MessageType.Warning,
                _ => MessageType.Error
            };

        string body =
            $"[{result.Status}] {result.DisplayName}\n" +
            $"ID: {result.CaseId}\n" +
            $"Category: {result.Category} / {result.ElapsedMilliseconds}ms\n" +
            $"Expected: {result.Expected}\n" +
            $"Actual: {result.Actual}";

        if (!string.IsNullOrWhiteSpace(
                result.Details))
        {
            body +=
                "\n\n" +
                result.Details;
        }

        EditorGUILayout.HelpBox(
            body,
            messageType);
    }

    private void RunDataCoverage()
    {
        try
        {
            GameSystemVerificationReport report =
                GameSystemVerificationRunner.RunDataChecks();

            GameSystemVerificationReportWriter.Write(
                report);

            lastReport = report;
            Repaint();
            Log(report, "Data Rule Coverage");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void RunFullCoverage()
    {
        if (!Application.isPlaying)
        {
            Debug.Log(
                "[GameSystemVerification] Play Mode 진입 후 Full Coverage를 자동 실행합니다.");

            GameSystemVerificationPlayModeQueue
                .QueueFullCoverage();

            return;
        }

        RunAgainstCurrentBattle(
            "Full System Coverage");
    }

    private void RunCurrentPlanAudit()
    {
        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<
                BattleManager>();

        try
        {
            GameSystemVerificationReport report =
                GameSystemVerificationRunner.RunLivePlanAudit(
                    manager);

            GameSystemVerificationReportWriter.Write(
                report);

            lastReport = report;
            Repaint();
            Log(report, "Current In-Game Plan Audit");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void RunAgainstCurrentBattle(
        string label)
    {
        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<
                BattleManager>();

        try
        {
            GameSystemVerificationReport report =
                GameSystemVerificationRunner.RunFull(
                    manager);

            GameSystemVerificationReportWriter.Write(
                report);

            lastReport = report;
            Repaint();
            Log(report, label);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void RevealLatestReport()
    {
        string directory =
            GameSystemVerificationReportWriter
                .ReadLatestDirectory();

        if (string.IsNullOrWhiteSpace(directory) ||
            !Directory.Exists(directory))
        {
            EditorUtility.DisplayDialog(
                "Game System Verification",
                "아직 저장된 Report가 없습니다.",
                "확인");

            return;
        }

        EditorUtility.RevealInFinder(
            directory);
    }

    private static void Log(
        GameSystemVerificationReport report,
        string label)
    {
        if (report == null)
            return;

        string message =
            $"[GameSystemVerification] {label} / " +
            $"Result={(report.Succeeded ? "PASSED" : "FAILED")}, " +
            $"PASS={report.PassCount}, FAIL={report.FailCount}, " +
            $"SKIP={report.SkipCount}, ERROR={report.ErrorCount}, " +
            $"Output={report.OutputDirectory}";

        if (report.Succeeded)
            Debug.Log(message);
        else
            Debug.LogError(message);
    }
}
#endif
