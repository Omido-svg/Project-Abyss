using System;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class BattleTraceFileWriter : IDisposable
{
    private readonly StreamWriter writer;

    public string SessionDirectory { get; }
    public string EventsPath { get; }
    public string SummaryPath { get; }

    public BattleTraceFileWriter(
        string rootDirectory,
        string sessionId)
    {
        string safeSession = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString("N")
            : sessionId;

        SessionDirectory = Path.Combine(
            rootDirectory,
            safeSession);

        Directory.CreateDirectory(SessionDirectory);

        File.WriteAllText(
            Path.Combine(rootDirectory, "latest_session.txt"),
            SessionDirectory,
            new UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(SessionDirectory, "output_location.txt"),
            SessionDirectory,
            new UTF8Encoding(false));

        EventsPath = Path.Combine(
            SessionDirectory,
            "events.jsonl");

        SummaryPath = Path.Combine(
            SessionDirectory,
            "summary.json");

        writer = new StreamWriter(
            new FileStream(
                EventsPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read),
            new UTF8Encoding(false));

        writer.AutoFlush = true;
    }

    public void Write(BattleTraceRecord record)
    {
        if (record == null || writer == null)
            return;

        writer.WriteLine(
            JsonUtility.ToJson(record));
    }

    public void WriteSummary(
        BattleTraceSessionSummary summary)
    {
        if (summary == null)
            return;

        File.WriteAllText(
            SummaryPath,
            JsonUtility.ToJson(summary, true),
            new UTF8Encoding(false));

        string root = Directory.GetParent(
            SessionDirectory)?.FullName;

        if (!string.IsNullOrWhiteSpace(root))
        {
            File.WriteAllText(
                Path.Combine(root, "latest_session.txt"),
                SessionDirectory,
                new UTF8Encoding(false));
        }
    }

    public void Dispose()
    {
        writer?.Flush();
        writer?.Dispose();
    }
}
