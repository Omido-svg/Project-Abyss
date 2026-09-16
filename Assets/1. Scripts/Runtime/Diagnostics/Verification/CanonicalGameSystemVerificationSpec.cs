using System.Collections.Generic;

/// <summary>
/// 0915 Source-of-Truth의 검증 Oracle.
/// 런타임 BattleRuleSettings를 읽어서 기대값을 만들지 않는다.
/// 기획이 바뀌면 Runtime과 이 Spec을 각각 의도적으로 수정해야 한다.
/// </summary>
public static class CanonicalGameSystemVerificationSpec
{
    public const string SpecId = "0915-AB__0916-CD-Olaf-Yujin";
    public const string PhaseCTargetId = "0916";

    public const int NormalAttackLoadout = 3;
    public const int DuelLoadout = 3;
    public const int PreparationLoadout = 3;
    public const int PrestigeLoadout = 1;

    public const int EnergyStartMaximum = 3;
    public const int EnergyTurnStartGain = 1;

    // PowerFormulaService.EnergyPowerPerPoint 역시 const이므로
    // 이 값까지 const로 두면 C# 컴파일러가 Verification의 비교식을
    // 컴파일 타임에 항상 false/true로 접어 CS0162를 발생시킨다.
    // 독립 Test Oracle의 값은 유지하되 runtime-readonly로 둔다.
    public static readonly int EnergyPowerPerPoint = 2;

    public const int MomentumMinimum = -100;
    public const int MomentumMaximum = 100;
    public const int MomentumLastStand = -70;
    public const int MomentumDisadvantage = -30;
    public const int MomentumAdvantage = 30;
    public const int MomentumOverwhelm = 70;
    public const int MomentumHitShift = 20;
    public const int MomentumDuelShift = 40;

    public const int FervorOverwhelmGain = 10;
    public const int FervorAdvantageGain = 5;
    public const int FervorBalanceGain = 2;
    public const int FervorLevel1Cost = 4;
    public const int FervorLevel2Cost = 8;
    public const int FervorLevel3Cost = 10;
    public const int FervorMaximumLevel = 3;

    public static readonly IReadOnlyDictionary<int, int> PowerCurve =
        new Dictionary<int, int>
        {
            { 1, 20 },
            { 2, 12 },
            { 3, 11 },
            { 4, 9 },
            { 5, 8 }
        };

    public static readonly IReadOnlyList<string> PhaseARequirements =
        new[]
        {
            "C-42", "C-46", "C-47", "C-02", "C-03",
            "C-04", "C-45", "C-05", "C-06", "C-10"
        };

    public static readonly IReadOnlyList<string> PhaseBRequirements =
        new[]
        {
            "C-09", "C-12", "C-13", "C-15", "C-17", "C-18",
            "C-19", "C-20", "C-21", "C-22", "C-23", "C-24",
            "C-25", "C-27", "C-48", "C-49", "C-41", "C-43"
        };

    public static readonly IReadOnlyList<string> PhaseCRequirements =
        new[]
        {
            "C-34", "C-35", "C-36", "C-37", "C-39"
        };

    public static readonly IReadOnlyList<string> PhaseDOlafRequirements =
        new[]
        {
            "O-01", "O-03", "O-04", "O-05"
        };

    public static readonly IReadOnlyList<string> PhaseDYujinRequirements =
        new[]
        {
            "Y-01", "Y-02", "Y-03", "Y-05", "Y-07"
        };

    public static readonly IReadOnlyList<string> PhaseDHifumiRequirements =
        new[]
        {
            "H-01", "H-02", "H-03", "H-05",
            "H-06", "H-07", "H-09", "H-10"
        };

    public static readonly IReadOnlyList<string> PhaseERequirements =
        new[]
        {
            "C-31", "C-32", "C-33", "C-38", "C-40", "C-44",
            "O-02", "Y-06", "H-08"
        };
}
