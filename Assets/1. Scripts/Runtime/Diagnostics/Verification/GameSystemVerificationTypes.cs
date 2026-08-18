using System;
using System.Collections.Generic;

public enum GameSystemVerificationCategory
{
    Data,
    Planning,
    Speed,
    Targeting,
    Pairing,
    Resource,
    Phase,
    Lifecycle,
    LiveState
}

public enum GameSystemVerificationStatus
{
    Pass,
    Fail,
    Skip,
    Error
}

[Serializable]
public sealed class GameSystemVerificationCaseResult
{
    public string CaseId;
    public string DisplayName;
    public GameSystemVerificationCategory Category;
    public GameSystemVerificationStatus Status;
    public string Expected;
    public string Actual;
    public string Details;
    public long ElapsedMilliseconds;
}

[Serializable]
public sealed class GameSystemVerificationReport
{
    public string SessionId;
    public string StartedAt;
    public string FinishedAt;
    public string SceneName;
    public string PlayerName;

    public int PassCount;
    public int FailCount;
    public int SkipCount;
    public int ErrorCount;

    public List<GameSystemVerificationCaseResult> Results = new();

    [NonSerialized]
    public string OutputDirectory;

    public bool Succeeded =>
        FailCount == 0 &&
        ErrorCount == 0;

    public void RecalculateCounts()
    {
        PassCount = 0;
        FailCount = 0;
        SkipCount = 0;
        ErrorCount = 0;

        if (Results == null)
            return;

        foreach (GameSystemVerificationCaseResult result in Results)
        {
            if (result == null)
                continue;

            switch (result.Status)
            {
                case GameSystemVerificationStatus.Pass:
                    PassCount++;
                    break;

                case GameSystemVerificationStatus.Fail:
                    FailCount++;
                    break;

                case GameSystemVerificationStatus.Skip:
                    SkipCount++;
                    break;

                case GameSystemVerificationStatus.Error:
                    ErrorCount++;
                    break;
            }
        }
    }
}
