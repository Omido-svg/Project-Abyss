using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class CharacterVerificationReportWriter
{
    public const string RootFolderName =
        "CharacterVerification";

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
        CharacterVerificationReport report)
    {
        if (report == null)
            return string.Empty;

        Directory.CreateDirectory(
            RootDirectory);

        string safeName =
            Sanitize(
                report.CharacterName);

        string sessionName =
            $"{report.SessionId}_{safeName}";

        string directory =
            Path.Combine(
                RootDirectory,
                sessionName);

        Directory.CreateDirectory(
            directory);

        string jsonPath =
            Path.Combine(
                directory,
                "report.json");

        string textPath =
            Path.Combine(
                directory,
                "summary.txt");

        File.WriteAllText(
            jsonPath,
            JsonUtility.ToJson(
                report,
                true),
            new UTF8Encoding(false));

        File.WriteAllText(
            textPath,
            BuildSummary(report),
            new UTF8Encoding(false));

        File.WriteAllText(
            LatestReportPointer,
            directory,
            new UTF8Encoding(false));

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

    private static string BuildSummary(
        CharacterVerificationReport report)
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "PROJECT ABYSS CHARACTER VERIFICATION");

        builder.AppendLine(
            $"Character: {report.CharacterName}");

        builder.AppendLine(
            $"Profile: {report.ProfileName} ({report.ProfileId})");

        builder.AppendLine(
            $"Bundle: {report.BundleName}");

        builder.AppendLine(
            $"Started: {report.StartedAt}");

        builder.AppendLine(
            $"Finished: {report.FinishedAt}");

        builder.AppendLine(
            $"PASS {report.PassCount} / " +
            $"FAIL {report.FailCount} / " +
            $"SKIP {report.SkipCount} / " +
            $"ERROR {report.ErrorCount}");

        builder.AppendLine(
            new string('=', 72));

        if (report.Results == null)
            return builder.ToString();

        for (int i = 0;
             i < report.Results.Count;
             i++)
        {
            CharacterVerificationCaseResult result =
                report.Results[i];

            if (result == null)
                continue;

            builder.AppendLine(
                $"[{result.Status}] " +
                $"{result.CaseId} / " +
                $"{result.DisplayName}");

            builder.AppendLine(
                $"Category: {result.Category} / " +
                $"Mode: {result.ExecutionMode} / " +
                $"Time: {result.ElapsedMilliseconds}ms");

            builder.AppendLine(
                $"Expected: {result.Expected}");

            builder.AppendLine(
                $"Actual: {result.Actual}");

            if (!string.IsNullOrWhiteSpace(
                    result.InitialSnapshot))
            {
                builder.AppendLine(
                    $"Initial: {result.InitialSnapshot}");
            }

            if (!string.IsNullOrWhiteSpace(
                    result.FinalSnapshot))
            {
                builder.AppendLine(
                    $"Final: {result.FinalSnapshot}");
            }

            if (!string.IsNullOrWhiteSpace(
                    result.Details))
            {
                builder.AppendLine(
                    "Details:");

                builder.AppendLine(
                    result.Details);
            }

            builder.AppendLine(
                new string('-', 72));
        }

        return builder.ToString();
    }

    private static string Sanitize(
        string value)
    {
        string result =
            string.IsNullOrWhiteSpace(value)
                ? "Character"
                : value.Trim();

        char[] invalid =
            Path.GetInvalidFileNameChars();

        for (int i = 0;
             i < invalid.Length;
             i++)
        {
            result =
                result.Replace(
                    invalid[i],
                    '_');
        }

        return result.Replace(
            ' ',
            '_');
    }
}
