#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// 플레이어블/적을 구분하지 않고 모든 CharacterVerificationReport를 합산한다.
/// 기존 클래스 이름은 호출 호환을 위해 유지한다.
/// </summary>
public static class CoreCharacterVerificationAggregateWriter
{
    private sealed class Entry
    {
        public CharacterVerificationProfile Profile;
        public CharacterVerificationReport Report;
    }

    public static string TryWrite(
        IReadOnlyList<CharacterVerificationProfile> profiles,
        IReadOnlyList<CharacterVerificationReport> reports,
        string runLabel)
    {
        if (profiles == null ||
            reports == null ||
            profiles.Count == 0 ||
            reports.Count == 0)
        {
            return string.Empty;
        }

        int count =
            Math.Min(
                profiles.Count,
                reports.Count);

        List<Entry> entries =
            new List<Entry>();

        for (int i = 0;
             i < count;
             i++)
        {
            CharacterVerificationProfile profile =
                profiles[i];

            CharacterVerificationReport report =
                reports[i];

            if (profile?.Bundle == null ||
                report == null)
            {
                continue;
            }

            entries.Add(
                new Entry
                {
                    Profile = profile,
                    Report = report
                });
        }

        if (entries.Count == 0)
            return string.Empty;

        DateTime now =
            DateTime.Now;

        string root =
            Path.Combine(
                CharacterVerificationReportWriter.RootDirectory,
                "AllCharacters");

        Directory.CreateDirectory(
            root);

        string directory =
            Path.Combine(
                root,
                now.ToString(
                    "yyyyMMdd_HHmmss_fff"));

        Directory.CreateDirectory(
            directory);

        File.WriteAllText(
            Path.Combine(
                directory,
                "all_characters_summary.txt"),
            BuildSummary(
                entries,
                runLabel,
                now),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(
                directory,
                "all_characters_case_matrix.csv"),
            BuildCsv(entries),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(
                root,
                "latest_all_characters_report.txt"),
            directory,
            new UTF8Encoding(false));

        return directory;
    }

    private static string BuildSummary(
        IReadOnlyList<Entry> entries,
        string runLabel,
        DateTime generatedAt)
    {
        StringBuilder builder =
            new StringBuilder();

        int totalPass = 0;
        int totalFail = 0;
        int totalSkip = 0;
        int totalError = 0;
        int totalCases = 0;

        builder.AppendLine(
            "PROJECT ABYSS ALL CHARACTER COVERAGE");

        builder.AppendLine(
            "Scope=PLAYABLE + ENEMY + FUTURE CONCRETE CHARACTER BUNDLES");

        builder.AppendLine(
            $"Run={runLabel ?? "Coverage"}");

        builder.AppendLine(
            $"Generated={generatedAt:O}");

        builder.AppendLine(
            $"Profiles={entries.Count}");

        builder.AppendLine(
            new string(
                '=',
                96));

        foreach (Entry entry in entries)
        {
            CharacterAuthoringBundle bundle =
                entry.Profile.Bundle;

            CharacterVerificationReport report =
                entry.Report;

            report.RecalculateCounts();

            int cases =
                report.Results?.Count ?? 0;

            totalPass += report.PassCount;
            totalFail += report.FailCount;
            totalSkip += report.SkipCount;
            totalError += report.ErrorCount;
            totalCases += cases;

            builder.AppendLine(
                $"[{GetRole(bundle.Kind)}][{bundle.Kind}] {report.CharacterName}");

            builder.AppendLine(
                $"Result={(report.Succeeded ? "PASSED" : "FAILED")} / " +
                $"TOTAL={cases}, PASS={report.PassCount}, FAIL={report.FailCount}, " +
                $"SKIP={report.SkipCount}, ERROR={report.ErrorCount}");

            if (!string.IsNullOrWhiteSpace(
                    report.OutputDirectory))
            {
                builder.AppendLine(
                    $"Report={report.OutputDirectory}");
            }

            List<CharacterVerificationCaseResult> problems =
                report.Results?
                    .Where(
                        result =>
                            result != null &&
                            (result.Status == CharacterVerificationStatus.Fail ||
                             result.Status == CharacterVerificationStatus.Error))
                    .ToList() ??
                new List<CharacterVerificationCaseResult>();

            if (problems.Count > 0)
            {
                builder.AppendLine(
                    "Problems:");

                foreach (CharacterVerificationCaseResult problem
                         in problems)
                {
                    builder.AppendLine(
                        $"  - [{problem.Status}] {problem.CaseId} / " +
                        $"{problem.DisplayName} / {problem.Actual}");
                }
            }

            builder.AppendLine(
                new string(
                    '-',
                    96));
        }

        builder.AppendLine(
            $"OVERALL={(totalFail == 0 && totalError == 0 ? "PASSED" : "FAILED")}");

        builder.AppendLine(
            $"TOTAL={totalCases}, PASS={totalPass}, FAIL={totalFail}, " +
            $"SKIP={totalSkip}, ERROR={totalError}");

        return builder.ToString();
    }

    private static string BuildCsv(
        IReadOnlyList<Entry> entries)
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "Role,Kind,Character,Profile,CaseId,DisplayName,Category,ExecutionMode,Status,Expected,Actual,Details");

        foreach (Entry entry in entries)
        {
            CharacterAuthoringBundle bundle =
                entry.Profile.Bundle;

            CharacterVerificationReport report =
                entry.Report;

            if (report?.Results == null)
                continue;

            foreach (CharacterVerificationCaseResult result
                     in report.Results)
            {
                if (result == null)
                    continue;

                builder
                    .Append(Csv(GetRole(bundle.Kind))).Append(',')
                    .Append(Csv(bundle.Kind.ToString())).Append(',')
                    .Append(Csv(report.CharacterName)).Append(',')
                    .Append(Csv(report.ProfileName)).Append(',')
                    .Append(Csv(result.CaseId)).Append(',')
                    .Append(Csv(result.DisplayName)).Append(',')
                    .Append(Csv(result.Category.ToString())).Append(',')
                    .Append(Csv(result.ExecutionMode.ToString())).Append(',')
                    .Append(Csv(result.Status.ToString())).Append(',')
                    .Append(Csv(result.Expected)).Append(',')
                    .Append(Csv(result.Actual)).Append(',')
                    .Append(Csv(result.Details))
                    .AppendLine();
            }
        }

        return builder.ToString();
    }

    private static string GetRole(
        CharacterAuthoringKind kind)
    {
        return kind == CharacterAuthoringKind.NormalEnemy ||
               kind == CharacterAuthoringKind.EliteEnemy
            ? "ENEMY"
            : "PLAYABLE/CHARACTER";
    }

    private static string Csv(
        string value)
    {
        string safe =
            (value ?? string.Empty)
                .Replace(
                    "\"",
                    "\"\"");

        return
            "\"" + safe + "\"";
    }
}
#endif
