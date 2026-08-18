using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Game System Verification 결과를 사람이 읽는 summary, JSON, CSV로 함께 저장한다.
/// CharacterVerification과 분리된 루트 폴더를 사용해 캐릭터 구현 결함과 시스템 룰 결함을 구분한다.
/// </summary>
public static class GameSystemVerificationReportWriter
{
    public const string RootFolderName =
        "GameSystemVerification";

    public static string RootDirectory =>
        Path.GetFullPath(
            Path.Combine(
                Application.dataPath,
                "..",
                RootFolderName));

    public static string LatestReportPointer =>
        Path.Combine(
            RootDirectory,
            "latest_report.txt");

    public static string Write(
        GameSystemVerificationReport report)
    {
        if (report == null)
            return string.Empty;

        report.RecalculateCounts();

        Directory.CreateDirectory(
            RootDirectory);

        string scene =
            Sanitize(
                report.SceneName);

        string directory =
            Path.Combine(
                RootDirectory,
                $"{report.SessionId}_{scene}");

        Directory.CreateDirectory(
            directory);

        File.WriteAllText(
            Path.Combine(
                directory,
                "report.json"),
            JsonUtility.ToJson(
                report,
                true),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(
                directory,
                "summary.txt"),
            BuildSummary(report),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(
                directory,
                "case_matrix.csv"),
            BuildCsv(report),
            new UTF8Encoding(false));

        File.WriteAllText(
            LatestReportPointer,
            directory,
            new UTF8Encoding(false));

        report.OutputDirectory =
            directory;

        return directory;
    }

    public static string ReadLatestDirectory()
    {
        if (!File.Exists(
                LatestReportPointer))
        {
            return string.Empty;
        }

        try
        {
            return File
                .ReadAllText(
                    LatestReportPointer)
                .Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string BuildSummary(
        GameSystemVerificationReport report)
    {
        StringBuilder builder =
            new StringBuilder();

        if (report == null)
        {
            builder.AppendLine(
                "PROJECT ABYSS GAME SYSTEM VERIFICATION / NULL REPORT");

            return builder.ToString();
        }

        int total =
            report.Results?.Count ?? 0;

        int evaluated =
            report.PassCount +
            report.FailCount +
            report.ErrorCount;

        builder.AppendLine(
            "PROJECT ABYSS GAME SYSTEM VERIFICATION");

        builder.AppendLine(
            $"Scene: {report.SceneName}");

        builder.AppendLine(
            $"Player: {report.PlayerName}");

        builder.AppendLine(
            $"Started: {report.StartedAt}");

        builder.AppendLine(
            $"Finished: {report.FinishedAt}");

        builder.AppendLine(
            $"Result: {(report.Succeeded ? "PASSED" : "FAILED")}");

        builder.AppendLine(
            $"TOTAL {total} / EXECUTED {evaluated} / " +
            $"PASS {report.PassCount} / FAIL {report.FailCount} / " +
            $"SKIP {report.SkipCount} / ERROR {report.ErrorCount}");

        builder.AppendLine(
            new string('=', 84));

        if (report.Results == null)
            return builder.ToString();

        for (int i = 0;
             i < report.Results.Count;
             i++)
        {
            GameSystemVerificationCaseResult result =
                report.Results[i];

            if (result == null)
                continue;

            builder.AppendLine(
                $"[{result.Status}] {result.CaseId} / {result.DisplayName}");

            builder.AppendLine(
                $"Category: {result.Category} / Time: {result.ElapsedMilliseconds}ms");

            builder.AppendLine(
                $"Expected: {result.Expected}");

            builder.AppendLine(
                $"Actual: {result.Actual}");

            if (!string.IsNullOrWhiteSpace(
                    result.Details))
            {
                builder.AppendLine("Details:");
                builder.AppendLine(result.Details);
            }

            builder.AppendLine(
                new string('-', 84));
        }

        return builder.ToString();
    }

    private static string BuildCsv(
        GameSystemVerificationReport report)
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "CaseId,DisplayName,Category,Status,ElapsedMilliseconds,Expected,Actual,Details");

        if (report?.Results == null)
            return builder.ToString();

        foreach (GameSystemVerificationCaseResult result
                 in report.Results)
        {
            if (result == null)
                continue;

            builder
                .Append(Csv(result.CaseId)).Append(',')
                .Append(Csv(result.DisplayName)).Append(',')
                .Append(Csv(result.Category.ToString())).Append(',')
                .Append(Csv(result.Status.ToString())).Append(',')
                .Append(result.ElapsedMilliseconds).Append(',')
                .Append(Csv(result.Expected)).Append(',')
                .Append(Csv(result.Actual)).Append(',')
                .Append(Csv(result.Details))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string Csv(
        string value)
    {
        string safe =
            value ??
            string.Empty;

        safe =
            safe.Replace(
                "\"",
                "\"\"");

        return
            "\"" + safe + "\"";
    }

    private static string Sanitize(
        string value)
    {
        string result =
            string.IsNullOrWhiteSpace(value)
                ? "System"
                : value.Trim();

        foreach (char invalid
                 in Path.GetInvalidFileNameChars())
        {
            result =
                result.Replace(
                    invalid,
                    '_');
        }

        return result.Replace(
            ' ',
            '_');
    }
}
