using System;
using System.Collections.Generic;

public enum CharacterVerificationCategory
{
    Data,
    Passive,
    NormalAttack,
    Duel,
    Preparation,
    Prestige,
    Status,
    UI,
    Presentation,
    Boundary
}

public enum CharacterVerificationExecutionMode
{
    DataOnly,
    IsolatedRuntime,
    LiveScene
}

public enum CharacterVerificationStatus
{
    Pass,
    Fail,
    Skip,
    Error
}

[Serializable]
public sealed class CharacterVerificationCaseDefinition
{
    public string CaseId;
    public string DisplayName;
    public CharacterVerificationCategory Category;
    public CharacterVerificationExecutionMode ExecutionMode;
    public bool Enabled = true;
    public int Seed = 1207;

    [UnityEngine.TextArea(2, 5)]
    public string Description;

    public static CharacterVerificationCaseDefinition Create(
        string caseId,
        string displayName,
        CharacterVerificationCategory category,
        CharacterVerificationExecutionMode executionMode,
        string description,
        int seed = 1207)
    {
        return new CharacterVerificationCaseDefinition
        {
            CaseId = caseId,
            DisplayName = displayName,
            Category = category,
            ExecutionMode = executionMode,
            Description = description,
            Seed = seed,
            Enabled = true
        };
    }
}

[Serializable]
public sealed class CharacterVerificationCaseResult
{
    public string CaseId;
    public string DisplayName;
    public CharacterVerificationCategory Category;
    public CharacterVerificationExecutionMode ExecutionMode;
    public CharacterVerificationStatus Status;

    public string Expected;
    public string Actual;
    public string Details;
    public string InitialSnapshot;
    public string FinalSnapshot;

    public long ElapsedMilliseconds;
}

[Serializable]
public sealed class CharacterVerificationReport
{
    public string SessionId;
    public string ProfileId;
    public string ProfileName;
    public string BundleName;
    public string CharacterName;
    public string StartedAt;
    public string FinishedAt;

    public int PassCount;
    public int FailCount;
    public int SkipCount;
    public int ErrorCount;

    public List<CharacterVerificationCaseResult> Results = new();

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

        for (int i = 0; i < Results.Count; i++)
        {
            CharacterVerificationCaseResult result = Results[i];

            if (result == null)
                continue;

            switch (result.Status)
            {
                case CharacterVerificationStatus.Pass:
                    PassCount++;
                    break;

                case CharacterVerificationStatus.Fail:
                    FailCount++;
                    break;

                case CharacterVerificationStatus.Skip:
                    SkipCount++;
                    break;

                case CharacterVerificationStatus.Error:
                    ErrorCount++;
                    break;
            }
        }
    }
}
