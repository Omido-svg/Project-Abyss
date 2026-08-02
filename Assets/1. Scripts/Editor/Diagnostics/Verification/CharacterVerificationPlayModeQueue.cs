#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterVerificationPlayModeQueue
{
    private const string QueueKey =
        "ProjectAbyss.CharacterVerification.Queue";

    private const string StartedKey =
        "ProjectAbyss.CharacterVerification.Started";

    private static bool running;
    private static double readyAt;
    private static double deadlineAt;

    static CharacterVerificationPlayModeQueue()
    {
        EditorApplication.playModeStateChanged +=
            OnPlayModeStateChanged;

        EditorApplication.update +=
            Update;
    }

    public static void QueueProfiles(
        IEnumerable<CharacterVerificationProfile> profiles)
    {
        if (profiles == null)
            return;

        if (!EditorApplication.isPlaying)
        {
            CharacterVerificationProjectRepair
                .RepairAll(logResult: false);

            AssetDatabase.SaveAssets();
        }

        List<string> paths =
            profiles
                .Where(profile => profile != null)
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .ToList();

        if (paths.Count == 0)
        {
            Debug.LogWarning(
                "[CharacterVerification] Queue할 Profile이 없습니다.");
            return;
        }

        SessionState.SetString(
            QueueKey,
            string.Join(
                "\n",
                paths));

        SessionState.SetBool(
            StartedKey,
            false);

        if (EditorApplication.isPlaying)
        {
            running = true;
            readyAt =
                EditorApplication.timeSinceStartup +
                0.5d;

            deadlineAt =
                EditorApplication.timeSinceStartup +
                6d;
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
            if (!string.IsNullOrWhiteSpace(
                    SessionState.GetString(
                        QueueKey,
                        string.Empty)))
            {
                running = true;
                readyAt =
                    EditorApplication.timeSinceStartup +
                    0.75d;

                deadlineAt =
                    EditorApplication.timeSinceStartup +
                    6d;
            }
        }
        else if (state ==
                 PlayModeStateChange.EnteredEditMode)
        {
            running = false;
        }
    }

    private static void Update()
    {
        if (!running ||
            !EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup <
            readyAt)
        {
            return;
        }

        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<
                BattleManager>();

        if (manager != null &&
            !manager.IsInitialized &&
            EditorApplication.timeSinceStartup <
            deadlineAt)
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

        string raw =
            SessionState.GetString(
                QueueKey,
                string.Empty);

        SessionState.EraseString(
            QueueKey);

        string[] paths =
            raw.Split(
                new[]
                {
                    '\n'
                },
                StringSplitOptions.RemoveEmptyEntries);

        List<CharacterVerificationProfile> profiles =
            new List<CharacterVerificationProfile>();

        for (int i = 0;
             i < paths.Length;
             i++)
        {
            CharacterVerificationProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    CharacterVerificationProfile>(
                        paths[i]);

            if (profile != null)
                profiles.Add(profile);
        }

        try
        {
            List<CharacterVerificationReport> reports =
                CharacterVerificationRunner.RunAll(
                    profiles,
                    includeRuntime: true,
                    includeLiveScene: true,
                    writeReports: true);

            int pass =
                reports.Sum(
                    report => report?.PassCount ?? 0);

            int fail =
                reports.Sum(
                    report => report?.FailCount ?? 0);

            int skip =
                reports.Sum(
                    report => report?.SkipCount ?? 0);

            int error =
                reports.Sum(
                    report => report?.ErrorCount ?? 0);

            Debug.Log(
                "[CharacterVerification] Queued Full Verification 완료 / " +
                $"Profiles={reports.Count}, " +
                $"PASS={pass}, FAIL={fail}, " +
                $"SKIP={skip}, ERROR={error}");

            CharacterVerificationWindow.ShowReport(
                reports.LastOrDefault());
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            running = false;
            SessionState.SetBool(
                StartedKey,
                false);
        }
    }
}
#endif
