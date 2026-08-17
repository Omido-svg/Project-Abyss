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
    private static bool awaitingAutoBattle;
    private static bool coveragePrepared;
    private static int nextCoverageProfileIndex;
    private static double readyAt;
    private static double deadlineAt;
    private static double nextCoverageProfileAt;

    private static List<CharacterVerificationProfile>
        pendingProfiles;

    private static List<CharacterVerificationReport>
        pendingReports;

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

        awaitingAutoBattle = false;
        coveragePrepared = false;
        nextCoverageProfileIndex = 0;
        pendingProfiles = null;
        pendingReports = null;

        if (EditorApplication.isPlaying)
        {
            running = true;
            readyAt =
                EditorApplication.timeSinceStartup +
                0.5d;

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
            if (!string.IsNullOrWhiteSpace(
                    SessionState.GetString(
                        QueueKey,
                        string.Empty)))
            {
                running = true;
                awaitingAutoBattle = false;
                coveragePrepared = false;
                nextCoverageProfileIndex = 0;
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
            awaitingAutoBattle = false;
            coveragePrepared = false;
            nextCoverageProfileIndex = 0;
            pendingProfiles = null;
            pendingReports = null;

            SessionState.SetBool(
                StartedKey,
                false);
        }
    }

    private static void Update()
    {
        if (!running ||
            !EditorApplication.isPlaying ||
            awaitingAutoBattle ||
            EditorApplication.timeSinceStartup <
            readyAt)
        {
            return;
        }

        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<
                BattleManager>();

        if (!coveragePrepared)
        {
            if ((manager == null ||
                 !manager.IsInitialized ||
                 manager.TurnManager == null) &&
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

            pendingProfiles =
                LoadQueuedProfiles();

            pendingReports =
                new List<CharacterVerificationReport>();

            coveragePrepared = true;
            nextCoverageProfileIndex = 0;
            nextCoverageProfileAt =
                EditorApplication.timeSinceStartup;

            Debug.Log(
                "[CharacterVerification] Full Coverage를 Profile 단위로 " +
                "분할 실행합니다. Editor 한 frame에 모든 Fixture를 " +
                "생성하지 않습니다.");

            return;
        }

        if (EditorApplication.timeSinceStartup <
            nextCoverageProfileAt)
        {
            return;
        }

        if (pendingProfiles != null &&
            nextCoverageProfileIndex <
            pendingProfiles.Count)
        {
            CharacterVerificationProfile profile =
                pendingProfiles[
                    nextCoverageProfileIndex];

            nextCoverageProfileIndex++;

            try
            {
                CharacterVerificationReport report =
                    CharacterVerificationRunner.Run(
                        profile,
                        includeRuntime: true,
                        includeLiveScene: true,
                        writeReport: false);

                if (report != null)
                    pendingReports.Add(report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            nextCoverageProfileAt =
                EditorApplication.timeSinceStartup +
                0.05d;

            Debug.Log(
                "[CharacterVerification] Coverage 진행 / " +
                $"{nextCoverageProfileIndex}/" +
                $"{pendingProfiles.Count}");

            return;
        }

        try
        {
            if (manager == null ||
                !manager.IsInitialized ||
                manager.TurnManager == null)
            {
                CompleteQueuedVerification(
                    CreateStartFailure(
                        "BattleManager 또는 TurnManager가 준비되지 않아 " +
                        "자동 실전 분석을 시작하지 못했습니다."));

                return;
            }

            if (!CharacterVerificationAutoBattleDriver
                    .TryStart(
                        manager,
                        OnAutoBattleCompleted,
                        out string failure))
            {
                CompleteQueuedVerification(
                    CreateStartFailure(failure));

                return;
            }

            awaitingAutoBattle = true;

            Debug.Log(
                "[CharacterVerification] 정적·격리 검증 완료. " +
                "승률 자동계획으로 실제 한 판 분석을 시작합니다.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            CompleteQueuedVerification(
                CreateStartFailure(
                    exception.Message));
        }
    }

    private static List<CharacterVerificationProfile>
        LoadQueuedProfiles()
    {
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

        return profiles;
    }

    private static void OnAutoBattleCompleted(
        CharacterVerificationAutoBattleResult result)
    {
        awaitingAutoBattle = false;
        CompleteQueuedVerification(result);
    }

    private static CharacterVerificationAutoBattleResult
        CreateStartFailure(
            string failure)
    {
        return new CharacterVerificationAutoBattleResult
        {
            CompletedNormally = false,
            Outcome = "START_FAILED",
            FailureMessage =
                string.IsNullOrWhiteSpace(failure)
                    ? "자동 실전 분석 시작 실패"
                    : failure
        };
    }

    private static void CompleteQueuedVerification(
        CharacterVerificationAutoBattleResult autoResult)
    {
        try
        {
            pendingReports ??=
                new List<CharacterVerificationReport>();

            for (int i = 0;
                 i < pendingReports.Count;
                 i++)
            {
                CharacterVerificationReport report =
                    pendingReports[i];

                CharacterVerificationProfile profile =
                    pendingProfiles != null &&
                    i < pendingProfiles.Count
                        ? pendingProfiles[i]
                        : null;

                if (report == null)
                    continue;

                report.Results ??=
                    new List<CharacterVerificationCaseResult>();

                report.Results.Add(
                    BuildAutoBattleCase(
                        profile,
                        autoResult));

                CharacterVerificationCaseResult
                    skillObservation =
                        BuildPlayerSkillObservationCase(
                            profile,
                            autoResult);

                if (skillObservation != null)
                    report.Results.Add(skillObservation);

                report.FinishedAt =
                    DateTime.Now.ToString("O");

                report.RecalculateCounts();

                report.OutputDirectory =
                    CharacterVerificationReportWriter.Write(
                        report);
            }

            int pass =
                pendingReports.Sum(
                    report => report?.PassCount ?? 0);

            int fail =
                pendingReports.Sum(
                    report => report?.FailCount ?? 0);

            int skip =
                pendingReports.Sum(
                    report => report?.SkipCount ?? 0);

            int error =
                pendingReports.Sum(
                    report => report?.ErrorCount ?? 0);

            string autoOutcome =
                string.IsNullOrWhiteSpace(autoResult?.Outcome)
                    ? "NULL"
                    : autoResult.Outcome;

            bool allPassed =
                fail == 0 &&
                error == 0 &&
                autoResult?.CompletedNormally == true;

            string overallStatus =
                allPassed
                    ? "PASSED"
                    : "FAILED";

            string summary =
                "[CharacterVerification] Full Character Coverage 완료 / " +
                $"Result={overallStatus}, " +
                $"Profiles={pendingReports.Count}, " +
                $"Outcome={autoOutcome}, " +
                $"Turns={autoResult?.Turns ?? 0}, " +
                $"PASS={pass}, FAIL={fail}, " +
                $"SKIP={skip}, ERROR={error}";

            if (allPassed)
                Debug.Log(summary);
            else
                Debug.LogError(summary);

            CharacterVerificationReport displayReport =
                pendingReports.FirstOrDefault(
                    report =>
                        report != null &&
                        (report.FailCount > 0 ||
                         report.ErrorCount > 0)) ??
                pendingReports.FirstOrDefault(
                    report =>
                        IsReportForAutoBattlePlayer(
                            report,
                            autoResult)) ??
                pendingReports.FirstOrDefault(
                    report =>
                        report?.Results != null &&
                        report.Results.Any(
                            result =>
                                result?.CaseId ==
                                    "live.autobattle.winrate.full_match" &&
                                result.Status !=
                                    CharacterVerificationStatus.Skip)) ??
                pendingReports.LastOrDefault();

            CharacterVerificationWindow.ShowReport(
                displayReport);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            running = false;
            awaitingAutoBattle = false;
            coveragePrepared = false;
            nextCoverageProfileIndex = 0;
            pendingProfiles = null;
            pendingReports = null;

            SessionState.SetBool(
                StartedKey,
                false);
        }
    }

    private static CharacterVerificationCaseResult
        BuildAutoBattleCase(
            CharacterVerificationProfile profile,
            CharacterVerificationAutoBattleResult result)
    {
        bool participates =
            IsProfileParticipant(
                profile,
                result);

        CharacterVerificationStatus status;
        string actual;
        string details;

        bool rosterCaptured =
            result?.ParticipantNames != null &&
            result.ParticipantNames.Count > 0;

        if (!rosterCaptured &&
            result?.CompletedNormally != true)
        {
            status =
                CharacterVerificationStatus.Error;

            actual =
                result?.Outcome ??
                "Character Coverage 통합 전투 시작 실패";

            details =
                result?.BuildDetails() ??
                "CharacterVerificationAutoBattleResult가 null입니다.";
        }
        else if (!participates)
        {
            status =
                CharacterVerificationStatus.Skip;

            actual =
                "현재 전투 Roster에 해당 캐릭터 없음";

            details =
                "자동 실전 분석은 현재 Scene의 실제 Roster 한 판을 사용합니다.\n" +
                (result?.BuildDetails() ??
                 "Character Coverage 통합 전투 결과 없음");
        }
        else if (result?.CompletedNormally == true)
        {
            status =
                CharacterVerificationStatus.Pass;

            actual =
                result.AnalysisTurnBudgetReached
                    ? $"실전 스모크 구간 정상 완료 · {result.Turns}턴"
                    : $"정상 종료 · {result.Outcome} · {result.Turns}턴";

            details =
                result.BuildDetails();
        }
        else if (result?.TurnLimitReached == true)
        {
            status =
                CharacterVerificationStatus.Fail;

            actual =
                $"턴 제한 도달 · {result.Turns}턴";

            details =
                result.BuildDetails();
        }
        else
        {
            status =
                CharacterVerificationStatus.Error;

            actual =
                result?.Outcome ??
                "Character Coverage 통합 전투 결과 없음";

            details =
                result?.BuildDetails() ??
                "CharacterVerificationAutoBattleResult가 null입니다.";
        }

        return new CharacterVerificationCaseResult
        {
            CaseId =
                "live.autobattle.winrate.full_match",
            DisplayName =
                "승률 자동계획 실전 스모크 전투",
            Category =
                CharacterVerificationCategory.Boundary,
            ExecutionMode =
                CharacterVerificationExecutionMode.LiveScene,
            Status = status,
            Expected =
                "매 턴 승률 자동계획을 적용하고 실제 전투가 " +
                "승리·패배로 종료되거나 검증용 5턴 구간을 정상 완주",
            Actual = actual,
            Details = details,
            ElapsedMilliseconds =
                result?.ElapsedMilliseconds ?? 0
        };
    }

    private static CharacterVerificationCaseResult
        BuildPlayerSkillObservationCase(
            CharacterVerificationProfile profile,
            CharacterVerificationAutoBattleResult result)
    {
        if (profile?.Bundle == null ||
            result == null ||
            string.IsNullOrWhiteSpace(result.PlayerName) ||
            !MatchesAnyProfileIdentity(
                profile,
                new[]
                {
                    result.PlayerName
                }))
        {
            return null;
        }

        CharacterVerificationStatus status;
        string actual;

        if (!result.CompletedNormally)
        {
            status = CharacterVerificationStatus.Error;
            actual =
                "통합 전투가 정상 완료되지 않아 실전 스킬 관찰도 무효";
        }
        else if (result.PlayerRuntimeSkillCount <= 0)
        {
            status = CharacterVerificationStatus.Error;
            actual = "플레이어 RuntimeSkill이 0개로 기록됨";
        }
        else if (result.UsedPlayerSkills.Count == 0)
        {
            status = CharacterVerificationStatus.Fail;
            actual = "ActionStart에서 플레이어 스킬 실행이 관찰되지 않음";
        }
        else
        {
            status = CharacterVerificationStatus.Pass;
            actual =
                $"{result.UsedPlayerSkills.Count}/" +
                $"{result.PlayerRuntimeSkillCount}개 고유 스킬 실행 관찰";
        }

        string used =
            result.UsedPlayerSkills.Count == 0
                ? "NONE"
                : string.Join(
                    ", ",
                    result.UsedPlayerSkills);

        return new CharacterVerificationCaseResult
        {
            CaseId =
                "live.autobattle.player_skill_observation",
            DisplayName =
                "한 판 실전 스킬 실행 관찰",
            Category =
                CharacterVerificationCategory.Boundary,
            ExecutionMode =
                CharacterVerificationExecutionMode.LiveScene,
            Status = status,
            Expected =
                "승률 자동계획 전투에서 플레이어 Skill ActionStart가 " +
                "실제로 발생하고 사용 스킬이 기록됨",
            Actual = actual,
            Details =
                $"Used={used}\n" +
                "이 항목은 한 판에서 실제 사용된 스킬만 관찰합니다. " +
                "모든 장착·후보 스킬의 등록 여부는 Data/Isolated " +
                "skill coverage Case가 별도로 검증합니다.",
            ElapsedMilliseconds =
                result.ElapsedMilliseconds
        };
    }

    private static bool IsProfileParticipant(
        CharacterVerificationProfile profile,
        CharacterVerificationAutoBattleResult result)
    {
        if (profile?.Bundle == null || result == null)
            return false;

        IReadOnlyList<string> participantIdentities =
            result.ParticipantIdentities != null &&
            result.ParticipantIdentities.Count > 0
                ? result.ParticipantIdentities
                : result.ParticipantNames;

        return MatchesAnyProfileIdentity(
            profile,
            participantIdentities);
    }

    private static bool IsReportForAutoBattlePlayer(
        CharacterVerificationReport report,
        CharacterVerificationAutoBattleResult result)
    {
        if (report == null ||
            result == null ||
            string.IsNullOrWhiteSpace(result.PlayerName))
        {
            return false;
        }

        CharacterVerificationProfile profile =
            pendingProfiles?.FirstOrDefault(
                candidate =>
                    candidate != null &&
                    string.Equals(
                        candidate.ProfileId,
                        report.ProfileId,
                        StringComparison.Ordinal));

        if (profile == null)
            return false;

        return MatchesAnyProfileIdentity(
            profile,
            new[]
            {
                result.PlayerName
            });
    }

    private static bool MatchesAnyProfileIdentity(
        CharacterVerificationProfile profile,
        IReadOnlyList<string> runtimeIdentities)
    {
        if (profile?.Bundle == null ||
            runtimeIdentities == null ||
            runtimeIdentities.Count == 0)
        {
            return false;
        }

        List<string> identities =
            new List<string>
            {
                profile.Bundle.DisplayName,
                profile.Bundle.name,
                profile.Bundle.CharacterData?.CharacterName,
                profile.Bundle.CharacterData?.name,
                profile.Bundle.CharacterPrefab?.Data?.CharacterName,
                profile.Bundle.CharacterPrefab?.Data?.name
            };

        foreach (string identity in identities)
        {
            if (string.IsNullOrWhiteSpace(identity))
                continue;

            for (int i = 0;
                 i < runtimeIdentities.Count;
                 i++)
            {
                if (string.Equals(
                        identity.Trim(),
                        runtimeIdentities[i]?.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

}
#endif