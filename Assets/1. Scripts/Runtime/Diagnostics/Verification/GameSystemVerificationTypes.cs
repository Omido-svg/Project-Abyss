using System;
using System.Collections.Generic;

public enum GameSystemVerificationCategory
{
    Data,
    Contract,
    Planning,
    Speed,
    Targeting,
    Pairing,
    Resource,
    Damage,
    Momentum,
    Fervor,
    Prestige,
    Status,
    Phase,
    Lifecycle,
    Coverage,
    LiveState
}

public enum GameSystemVerificationExecutionMode
{
    StaticContract,
    IsolatedRuntime,
    LiveScene
}

public enum GameSystemVerificationStatus
{
    Pass,
    Fail,
    Skip,
    Pending,
    Error
}

[Serializable]
public sealed class GameSystemVerificationCaseResult
{
    public string CaseId;
    public string RequirementId;
    public string ModuleId;
    public string DisplayName;
    public GameSystemVerificationCategory Category;
    public GameSystemVerificationExecutionMode ExecutionMode;
    public GameSystemVerificationStatus Status;
    public bool Required = true;
    public string Expected;
    public string Actual;
    public string Details;
    public long ElapsedMilliseconds;
}

[Serializable]
public sealed class GameSystemVerificationReport
{
    public string SessionId;
    public string SpecId;
    public string StartedAt;
    public string FinishedAt;
    public string SceneName;
    public string PlayerName;

    public int PassCount;
    public int FailCount;
    public int SkipCount;
    public int PendingCount;
    public int ErrorCount;

    public List<GameSystemVerificationCaseResult> Results = new();

    [NonSerialized]
    public string OutputDirectory;

    public bool Succeeded
    {
        get
        {
            if (Results == null)
                return true;

            foreach (GameSystemVerificationCaseResult result in Results)
            {
                if (result == null || !result.Required)
                    continue;

                if (result.Status != GameSystemVerificationStatus.Pass)
                    return false;
            }

            return true;
        }
    }

    public void RecalculateCounts()
    {
        PassCount = 0;
        FailCount = 0;
        SkipCount = 0;
        PendingCount = 0;
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
                case GameSystemVerificationStatus.Pending:
                    PendingCount++;
                    break;
                case GameSystemVerificationStatus.Error:
                    ErrorCount++;
                    break;
            }
        }
    }
}
