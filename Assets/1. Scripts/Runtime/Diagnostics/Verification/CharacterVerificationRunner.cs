using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public static class CharacterVerificationRunner
{
    public static CharacterVerificationReport Run(
        CharacterVerificationProfile profile,
        bool includeRuntime,
        bool includeLiveScene,
        bool writeReport = true)
    {
        DateTime started =
            DateTime.Now;

        CharacterVerificationReport report =
            new CharacterVerificationReport
            {
                SessionId =
                    started.ToString(
                        "yyyyMMdd_HHmmss_fff"),
                ProfileId =
                    profile?.ProfileId ??
                    "NULL_PROFILE",
                ProfileName =
                    profile?.name ??
                    "NULL_PROFILE",
                BundleName =
                    profile?.Bundle?.name ??
                    "NULL_BUNDLE",
                CharacterName =
                    profile?.Bundle?.DisplayName ??
                    profile?.Bundle?.CharacterData?.CharacterName ??
                    profile?.Bundle?.CharacterPrefab?.name ??
                    "Unknown Character",
                StartedAt =
                    started.ToString("O")
            };

        if (profile == null)
        {
            report.Results.Add(
                new CharacterVerificationCaseResult
                {
                    CaseId = "framework.profile.null",
                    DisplayName = "Verification Profile",
                    Status =
                        CharacterVerificationStatus.Error,
                    Expected =
                        "CharacterVerificationProfile 존재",
                    Actual = "NULL",
                    Details =
                        "검증할 Profile이 없습니다."
                });

            CompleteReport(
                report,
                started,
                writeReport);

            return report;
        }

        IReadOnlyList<CharacterVerificationCaseDefinition>
            definitions =
                profile.Cases;

        if (definitions == null ||
            definitions.Count == 0)
        {
            report.Results.Add(
                new CharacterVerificationCaseResult
                {
                    CaseId = "framework.profile.empty",
                    DisplayName = "Verification Cases",
                    Status =
                        CharacterVerificationStatus.Fail,
                    Expected =
                        "검증 Case 1개 이상",
                    Actual = "0",
                    Details =
                        "Create/Refresh Default Profiles를 실행하세요."
                });

            CompleteReport(
                report,
                started,
                writeReport);

            return report;
        }

        for (int i = 0;
             i < definitions.Count;
             i++)
        {
            CharacterVerificationCaseDefinition definition =
                definitions[i];

            if (definition == null ||
                !definition.Enabled)
            {
                continue;
            }

            CharacterVerificationCaseResult result =
                ExecuteCase(
                    profile,
                    definition,
                    includeRuntime,
                    includeLiveScene);

            report.Results.Add(
                result);
        }

        CompleteReport(
            report,
            started,
            writeReport);

        return report;
    }

    public static List<CharacterVerificationReport> RunAll(
        IEnumerable<CharacterVerificationProfile> profiles,
        bool includeRuntime,
        bool includeLiveScene,
        bool writeReports = true)
    {
        List<CharacterVerificationReport> reports =
            new List<CharacterVerificationReport>();

        if (profiles == null)
            return reports;

        foreach (CharacterVerificationProfile profile
                 in profiles)
        {
            if (profile == null)
                continue;

            reports.Add(
                Run(
                    profile,
                    includeRuntime,
                    includeLiveScene,
                    writeReports));
        }

        return reports;
    }

    private static CharacterVerificationCaseResult
        ExecuteCase(
            CharacterVerificationProfile profile,
            CharacterVerificationCaseDefinition definition,
            bool includeRuntime,
            bool includeLiveScene)
    {
        Stopwatch stopwatch =
            Stopwatch.StartNew();

        CharacterVerificationCaseResult result;
        CharacterVerificationFixture fixture = null;
        Character liveCharacter = null;
        BattleContext battleContext = null;
        bool liveScene = false;

        try
        {
            switch (definition.ExecutionMode)
            {
                case CharacterVerificationExecutionMode.DataOnly:
                    break;

                case CharacterVerificationExecutionMode.IsolatedRuntime:
                    if (!includeRuntime)
                    {
                        result =
                            CreateSkip(
                                definition,
                                "런타임 검증이 비활성화되어 있습니다.");

                        result.ElapsedMilliseconds =
                            stopwatch.ElapsedMilliseconds;

                        return result;
                    }

                    if (!Application.isPlaying)
                    {
                        result =
                            CreateSkip(
                                definition,
                                "Isolated Runtime 검증은 Play Mode에서 실행합니다.");

                        result.ElapsedMilliseconds =
                            stopwatch.ElapsedMilliseconds;

                        return result;
                    }

                    if (profile.Bundle == null ||
                        profile.Bundle.CharacterPrefab == null)
                    {
                        result =
                            CreateSkip(
                                definition,
                                "선행 조건 실패: Character Prefab이 없습니다. " +
                                "Repair All Verification Dependencies를 먼저 실행하세요.");

                        result.ElapsedMilliseconds =
                            stopwatch.ElapsedMilliseconds;

                        return result;
                    }

                    fixture =
                        CharacterVerificationFixture.Create(
                            profile);

                    liveCharacter =
                        fixture.Character;

                    battleContext =
                        fixture.BattleContext;
                    break;

                case CharacterVerificationExecutionMode.LiveScene:
                    if (!includeLiveScene)
                    {
                        result =
                            CreateSkip(
                                definition,
                                "Live Scene 검증이 비활성화되어 있습니다.");

                        result.ElapsedMilliseconds =
                            stopwatch.ElapsedMilliseconds;

                        return result;
                    }

                    if (!Application.isPlaying)
                    {
                        result =
                            CreateSkip(
                                definition,
                                "Live Scene 검증은 Play Mode에서 실행합니다.");

                        result.ElapsedMilliseconds =
                            stopwatch.ElapsedMilliseconds;

                        return result;
                    }

                    BattleManager manager =
                        UnityEngine.Object
                            .FindFirstObjectByType<
                                BattleManager>();

                    liveCharacter =
                        manager?.BattleContext?.Player;

                    battleContext =
                        manager?.BattleContext;

                    liveScene = true;
                    break;
            }

            CharacterVerificationContext context =
                new CharacterVerificationContext(
                    profile,
                    definition,
                    liveCharacter,
                    battleContext,
                    liveScene);

            string initial =
                CharacterVerificationContext
                    .DescribeSnapshot(
                        liveCharacter);

            if (!CharacterVerificationCaseProviderRegistry
                    .TryExecute(
                        context,
                        out result))
            {
                result =
                    context.Fail(
                        "Case Executor 존재",
                        "미등록 CaseId",
                        definition.CaseId);
            }

            string final =
                CharacterVerificationContext
                    .DescribeSnapshot(
                        liveCharacter);

            result.InitialSnapshot =
                initial;

            result.FinalSnapshot =
                final;
        }
        catch (Exception exception)
        {
            CharacterVerificationContext context =
                new CharacterVerificationContext(
                    profile,
                    definition,
                    liveCharacter,
                    battleContext,
                    liveScene);

            result =
                context.Error(
                    exception);
        }
        finally
        {
            fixture?.Dispose();
        }

        stopwatch.Stop();

        result ??=
            new CharacterVerificationCaseResult
            {
                CaseId =
                    definition.CaseId,
                DisplayName =
                    definition.DisplayName,
                Category =
                    definition.Category,
                ExecutionMode =
                    definition.ExecutionMode,
                Status =
                    CharacterVerificationStatus.Error,
                Expected =
                    "검증 결과",
                Actual = "NULL",
                Details =
                    "Case Provider가 null 결과를 반환했습니다."
            };

        result.ElapsedMilliseconds =
            stopwatch.ElapsedMilliseconds;

        return result;
    }

    private static CharacterVerificationCaseResult
        CreateSkip(
            CharacterVerificationCaseDefinition definition,
            string reason)
    {
        return new CharacterVerificationCaseResult
        {
            CaseId =
                definition?.CaseId ??
                "UNKNOWN",
            DisplayName =
                definition?.DisplayName ??
                "Unknown",
            Category =
                definition?.Category ??
                CharacterVerificationCategory.Data,
            ExecutionMode =
                definition?.ExecutionMode ??
                CharacterVerificationExecutionMode.DataOnly,
            Status =
                CharacterVerificationStatus.Skip,
            Expected =
                "검증 실행",
            Actual =
                "건너뜀",
            Details =
                reason ?? string.Empty
        };
    }

    private static void CompleteReport(
        CharacterVerificationReport report,
        DateTime started,
        bool writeReport)
    {
        report.FinishedAt =
            DateTime.Now.ToString("O");

        report.RecalculateCounts();

        if (writeReport)
        {
            report.OutputDirectory =
                CharacterVerificationReportWriter.Write(
                    report);
        }

        UnityEngine.Debug.Log(
            "[CharacterVerification] " +
            $"{report.CharacterName} / " +
            $"PASS={report.PassCount}, " +
            $"FAIL={report.FailCount}, " +
            $"SKIP={report.SkipCount}, " +
            $"ERROR={report.ErrorCount}" +
            (string.IsNullOrWhiteSpace(
                 report.OutputDirectory)
                ? string.Empty
                : $" / {report.OutputDirectory}"));
    }
}
