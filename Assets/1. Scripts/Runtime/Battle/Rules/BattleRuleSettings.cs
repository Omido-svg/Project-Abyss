using System;
using UnityEngine;

[Serializable]
public sealed class BattleRuleSettings
{
    public MomentumRuleSettings Momentum = new();
    public ClashRuleSettings Clash = new();
    public PrestigeRuleSettings Prestige = new();

    [Header("Legacy Compatibility")]
    [Tooltip(
        "공격력/방어력/피해 배율 기반의 기존 수치 파이프라인을 유지할 때만 켭니다. " +
        "새 기세 바 설계에서는 꺼진 상태가 표준입니다.")]
    public bool UseLegacyAttackDefenseStats;

    public void Normalize()
    {
        Momentum ??= new MomentumRuleSettings();
        Clash ??= new ClashRuleSettings();
        Prestige ??= new PrestigeRuleSettings();

        Momentum.Normalize();
        Clash.Normalize();
        Prestige.Normalize();
    }
}

[Serializable]
public sealed class MomentumRuleSettings
{
    [Header("Range")]
    public int Minimum = -100;
    public int Maximum = 100;

    [Header("Perspective Thresholds")]
    public int LastStandThreshold = -70;
    public int DisadvantageThreshold = -30;
    public int AdvantageThreshold = 30;
    public int OverwhelmThreshold = 70;

    [Header("Damage Multiplier")]
    [Min(0f)] public float LastStandMultiplier = 0.4f;
    [Min(0f)] public float DisadvantageMultiplier = 0.4f;
    [Min(0f)] public float BalanceMultiplier = 0.4f;
    [Min(0f)] public float AdvantageMultiplier = 1.0f;
    [Min(0f)] public float OverwhelmMultiplier = 2.0f;
    [Min(0f)] public float MaximumOverwhelmMultiplier = 2.5f;

    [Header("Shift")]
    [Min(0)] public int HitShift = 5;
    [Min(0)] public int DuelVictoryShift = 20;
    [Min(1)] public int LastStandHitShiftMultiplier = 2;

    public void Normalize()
    {
        if (Maximum <= Minimum)
            Maximum = Minimum + 1;

        LastStandThreshold = Mathf.Clamp(
            LastStandThreshold,
            Minimum,
            Maximum);

        DisadvantageThreshold = Mathf.Clamp(
            DisadvantageThreshold,
            LastStandThreshold,
            Maximum);

        AdvantageThreshold = Mathf.Clamp(
            AdvantageThreshold,
            DisadvantageThreshold,
            Maximum);

        OverwhelmThreshold = Mathf.Clamp(
            OverwhelmThreshold,
            AdvantageThreshold,
            Maximum);

        LastStandMultiplier = Mathf.Max(0f, LastStandMultiplier);
        DisadvantageMultiplier = Mathf.Max(0f, DisadvantageMultiplier);
        BalanceMultiplier = Mathf.Max(0f, BalanceMultiplier);
        AdvantageMultiplier = Mathf.Max(0f, AdvantageMultiplier);
        OverwhelmMultiplier = Mathf.Max(0f, OverwhelmMultiplier);
        MaximumOverwhelmMultiplier = Mathf.Max(
            OverwhelmMultiplier,
            MaximumOverwhelmMultiplier);

        HitShift = Mathf.Max(0, HitShift);
        DuelVictoryShift = Mathf.Max(0, DuelVictoryShift);
        LastStandHitShiftMultiplier = Mathf.Max(
            1,
            LastStandHitShiftMultiplier);
    }
}

[Serializable]
public sealed class ClashRuleSettings
{
    [Min(1)]
    public int DefaultExchangeRollCount = 3;

    [Min(0)]
    public int SpeedWeight = 1;

    [Tooltip(
        "true면 COMBAT 행동은 승패와 관계없이 행동 시작 시 자원을 소비합니다.")]
    public bool ConsumeResourceOnActionStart = true;

    public void Normalize()
    {
        DefaultExchangeRollCount = Mathf.Max(
            1,
            DefaultExchangeRollCount);

        SpeedWeight = Mathf.Max(0, SpeedWeight);
    }
}

[Serializable]
public sealed class PrestigeRuleSettings
{
    [Header("Standard Charge")]
    [Min(0)] public int ClashStartCharge = 1;
    [Min(0)] public int HitDealtCharge = 1;
    [Min(0)] public int HitTakenCharge = 1;
    [Min(0)] public int ClashVictoryCharge = 1;

    [Tooltip(
        "도사림은 합을 만들지 않으므로 표준 위세 충전에서 제외합니다.")]
    public bool PreparationDoesNotCharge = true;

    public void Normalize()
    {
        ClashStartCharge = Mathf.Max(0, ClashStartCharge);
        HitDealtCharge = Mathf.Max(0, HitDealtCharge);
        HitTakenCharge = Mathf.Max(0, HitTakenCharge);
        ClashVictoryCharge = Mathf.Max(0, ClashVictoryCharge);
    }
}
