using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class GameSystemVerificationReportWriter
{
    public const string RootFolderName = "GameSystemVerification";

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
        Directory.CreateDirectory(RootDirectory);

        string directory = Path.Combine(
            RootDirectory,
            $"{report.SessionId}_{Sanitize(report.SceneName)}");

        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "report.json"),
            JsonUtility.ToJson(report, true),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(directory, "summary.txt"),
            BuildSummary(report),
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(directory, "case_matrix.csv"),
            BuildCsv(report),
            new UTF8Encoding(false));

        File.WriteAllText(
            LatestReportPointer,
            directory,
            new UTF8Encoding(false));

        report.OutputDirectory = directory;
        return directory;
    }

    public static string ReadLatestDirectory()
    {
        if (!File.Exists(LatestReportPointer))
            return string.Empty;

        try
        {
            return File.ReadAllText(LatestReportPointer).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string BuildSummary(
        GameSystemVerificationReport report)
    {
        StringBuilder builder = new();

        if (report == null)
        {
            builder.AppendLine(
                "PROJECT ABYSS GAME SYSTEM VERIFICATION / NULL REPORT");
            return builder.ToString();
        }

        int total = report.Results?.Count ?? 0;
        int evaluated =
            report.PassCount +
            report.FailCount +
            report.SkipCount +
            report.PendingCount +
            report.ErrorCount;

        builder.AppendLine("PROJECT ABYSS GAME SYSTEM VERIFICATION");
        builder.AppendLine($"Spec: {report.SpecId}");
        builder.AppendLine($"Scene: {report.SceneName}");
        builder.AppendLine($"Player: {report.PlayerName}");
        builder.AppendLine($"Started: {report.StartedAt}");
        builder.AppendLine($"Finished: {report.FinishedAt}");
        builder.AppendLine(
            $"Result: {(report.Succeeded ? "PASSED" : "NOT CLOSED")}");
        builder.AppendLine(
            $"TOTAL {total} / EXECUTED {evaluated} / " +
            $"PASS {report.PassCount} / FAIL {report.FailCount} / " +
            $"SKIP {report.SkipCount} / PENDING {report.PendingCount} / " +
            $"ERROR {report.ErrorCount}");
        builder.AppendLine(new string('=', 84));

        if (report.Results == null)
            return builder.ToString();

        foreach (GameSystemVerificationCaseResult result in report.Results)
        {
            if (result == null)
                continue;

            builder.AppendLine(
                $"[{result.Status}] {result.CaseId} / {result.DisplayName}");
            builder.AppendLine(
                $"Module: {result.ModuleId} / Requirement: {result.RequirementId} / " +
                $"Mode: {result.ExecutionMode} / Category: {result.Category} / " +
                $"Required: {result.Required} / Time: {result.ElapsedMilliseconds}ms");
            builder.AppendLine($"Expected: {result.Expected}");
            builder.AppendLine($"Actual: {result.Actual}");

            if (!string.IsNullOrWhiteSpace(result.Details))
            {
                builder.AppendLine("Details:");
                builder.AppendLine(result.Details);
            }

            builder.AppendLine(new string('-', 84));
        }

        return builder.ToString();
    }

    private static string BuildCsv(
        GameSystemVerificationReport report)
    {
        StringBuilder builder = new();

        builder.AppendLine(
            "CaseId,RequirementId,ModuleId,DisplayName,Category,ExecutionMode,Status,Required,ElapsedMilliseconds,Expected,Actual,Details");

        if (report?.Results == null)
            return builder.ToString();

        foreach (GameSystemVerificationCaseResult result in report.Results)
        {
            if (result == null)
                continue;

            builder
                .Append(Csv(result.CaseId)).Append(',')
                .Append(Csv(result.RequirementId)).Append(',')
                .Append(Csv(result.ModuleId)).Append(',')
                .Append(Csv(result.DisplayName)).Append(',')
                .Append(Csv(result.Category.ToString())).Append(',')
                .Append(Csv(result.ExecutionMode.ToString())).Append(',')
                .Append(Csv(result.Status.ToString())).Append(',')
                .Append(result.Required).Append(',')
                .Append(result.ElapsedMilliseconds).Append(',')
                .Append(Csv(result.Expected)).Append(',')
                .Append(Csv(result.Actual)).Append(',')
                .Append(Csv(result.Details))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string Csv(string value)
    {
        string safe = (value ?? string.Empty)
            .Replace("\"", "\"\"");
        return "\"" + safe + "\"";
    }

    private static string Sanitize(string value)
    {
        string result = string.IsNullOrWhiteSpace(value)
            ? "System"
            : value.Trim();

        foreach (char invalid in Path.GetInvalidFileNameChars())
            result = result.Replace(invalid, '_');

        return result.Replace(' ', '_');
    }
}
