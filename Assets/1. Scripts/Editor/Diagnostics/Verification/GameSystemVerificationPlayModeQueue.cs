#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit Mode에서 Full Game System Coverage를 눌렀을 때 Play Mode로 진입한 뒤
/// BattleManager 초기화를 기다려 동일 검증 Runner를 한 번 실행한다.
/// 전투 행동을 자동 진행하지 않으며 현재 전투 상태를 변경하지 않는다.
/// </summary>
[InitializeOnLoad]
public static class GameSystemVerificationPlayModeQueue
{
    private const string QueueKey =
        "ProjectAbyss.GameSystemVerification.FullCoverageQueued";

    private const string StartedKey =
        "ProjectAbyss.GameSystemVerification.Started";

    private static bool running;
    private static double readyAt;
    private static double deadlineAt;

    static GameSystemVerificationPlayModeQueue()
    {
        EditorApplication.playModeStateChanged +=
            OnPlayModeStateChanged;

        EditorApplication.update +=
            Update;
    }

    public static void QueueFullCoverage()
    {
        SessionState.SetBool(
            QueueKey,
            true);

        SessionState.SetBool(
            StartedKey,
            false);

        if (EditorApplication.isPlaying)
        {
            running = true;
            readyAt =
                EditorApplication.timeSinceStartup +
                0.25d;

            deadlineAt =
                EditorApplication.timeSinceStartup +
                12d;
        }
        else
        {
            EditorApplication.isPlaying = true;
        }
    }

    private static void OnPlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (state ==
            PlayModeStateChange.EnteredPlayMode)
        {
            if (SessionState.GetBool(
                    QueueKey,
                    false))
            {
                running = true;
                readyAt =
                    EditorApplication.timeSinceStartup +
                    0.75d;

                deadlineAt =
                    EditorApplication.timeSinceStartup +
                    12d;
            }
        }
        else if (state ==
                 PlayModeStateChange.EnteredEditMode)
        {
            running = false;
            SessionState.SetBool(
                StartedKey,
                false);
        }
    }

    private static void Update()
    {
        if (!running ||
            !EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < readyAt)
        {
            return;
        }

        if (!SessionState.GetBool(
                QueueKey,
                false))
        {
            running = false;
            return;
        }

        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<
                BattleManager>();

        bool ready =
            manager != null &&
            manager.IsInitialized &&
            manager.BattleContext?.Player != null &&
            manager.SpeedManager != null &&
            manager.ActionManager != null &&
            manager.ClashBuilder != null;

        if (!ready &&
            EditorApplication.timeSinceStartup < deadlineAt)
        {
            return;
        }

        if (SessionState.GetBool(
                StartedKey,
                false))
        {
            running = false;
            return;
        }

        SessionState.SetBool(
            StartedKey,
            true);

        try
        {
            GameSystemVerificationReport report =
                GameSystemVerificationRunner.RunFull(
                    manager);

            GameSystemVerificationReportWriter.Write(
                report);

            GameSystemVerificationWindow.ShowReport(
                report);

            LogSummary(report);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            running = false;
            SessionState.SetBool(
                QueueKey,
                false);

            SessionState.SetBool(
                StartedKey,
                false);
        }
    }

    private static void LogSummary(
        GameSystemVerificationReport report)
    {
        if (report == null)
        {
            Debug.LogError(
                "[GameSystemVerification] Report=NULL");

            return;
        }

        string summary =
            "[GameSystemVerification] Full Coverage 완료 / " +
            $"Result={(report.Succeeded ? "PASSED" : "FAILED")}, " +
            $"PASS={report.PassCount}, FAIL={report.FailCount}, " +
            $"SKIP={report.SkipCount}, ERROR={report.ErrorCount}, " +
            $"Output={report.OutputDirectory}";

        if (report.Succeeded)
            Debug.Log(summary);
        else
            Debug.LogError(summary);
    }
}
#endif
