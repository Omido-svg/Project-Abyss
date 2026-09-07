using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class BattleRuleSettings
{
    public MomentumRuleSettings Momentum = new();
    public ClashRuleSettings Clash = new();
    public EnergyRuleSettings Energy = new();
    public PrestigeRuleSettings Prestige = new();

    public void Normalize()
    {
        Momentum ??= new MomentumRuleSettings();
        Clash ??= new ClashRuleSettings();
        Energy ??= new EnergyRuleSettings();
        Prestige ??= new PrestigeRuleSettings();

        Momentum.Normalize();
        Clash.Normalize();
        Energy.Normalize();
        Prestige.Normalize();
    }
}

[Serializable]
public sealed class MomentumRuleSettings
{
    public int Minimum = -100;
    public int Maximum = 100;
    public int LastStandThreshold = -70;
    public int DisadvantageThreshold = -30;
    public int AdvantageThreshold = 30;
    public int OverwhelmThreshold = 70;

    [Header("Fixed interval multipliers")]
    [Min(0f)] public float LastStandMultiplier = 0.5f;
    [Min(0f)] public float DisadvantageMultiplier = 0.5f;
    [Min(0f)] public float BalanceMultiplier = 1f;
    [Min(0f)] public float AdvantageMultiplier = 1f;
    [Min(0f)] public float OverwhelmMultiplier = 2f;
    [HideInInspector] public float MaximumOverwhelmMultiplier = 2f;

    [Header("Exchange shifts")]
    [Min(0)] public int HitShift = 5;

    [Tooltip("결투 대 결투에서 개별 교환 승자가 받는 추가 기세 이동량입니다.")]
    [Min(0)] public int DuelExchangeShift = 15;

    [Min(1)] public int LastStandHitShiftMultiplier = 2;

    public void Normalize()
    {
        Minimum = -100;
        Maximum = 100;
        LastStandThreshold = -70;
        DisadvantageThreshold = -30;
        AdvantageThreshold = 30;
        OverwhelmThreshold = 70;

        LastStandMultiplier = 0.5f;
        DisadvantageMultiplier = 0.5f;
        BalanceMultiplier = 1f;
        AdvantageMultiplier = 1f;
        OverwhelmMultiplier = 2f;
        MaximumOverwhelmMultiplier = 2f;

        HitShift = 5;
        DuelExchangeShift = 15;
        LastStandHitShiftMultiplier = 2;
    }
}

[Serializable]
public sealed class ClashRuleSettings
{
    [Range(1, 8)] public int DefaultExchangeRollCount = 3;
    [Min(0)] public int SpeedWeight = 1;
    [Min(1)] public int MaxTieRerolls = 64;
    [Min(1)] public int MaxCharacterRerollsPerExchange = 64;
    public bool ConsumeResourceOnActionStart = true;

    public void Normalize()
    {
        DefaultExchangeRollCount =
            Mathf.Clamp(
                DefaultExchangeRollCount,
                1,
                8);

        SpeedWeight = Mathf.Max(
            0,
            SpeedWeight);

        MaxTieRerolls = Mathf.Max(
            1,
            MaxTieRerolls);

        MaxCharacterRerollsPerExchange =
            Mathf.Max(
                1,
                MaxCharacterRerollsPerExchange);
    }
}

[Serializable]
public sealed class EnergyRuleSettings
{
    [Min(1)] public int DefaultMaximum = 3;
    [Min(0)] public int TurnStartGain = 1;
    public bool StartFull = true;

    public void Normalize()
    {
        DefaultMaximum = Mathf.Max(
            1,
            DefaultMaximum);

        TurnStartGain = Mathf.Max(
            0,
            TurnStartGain);
    }
}

[Serializable]
public sealed class PrestigeRuleSettings
{
    [Header("Exchange-based charge")]
    [Min(0)] public int ExchangeParticipantCharge = 5;
    [Min(0)] public int OneSidedParticipantCharge = 5;

    [Tooltip("준비 행동은 교환 위세 충전 대상에서 제외합니다.")]
    [FormerlySerializedAs("PreparationDoesNotCharge")]
    public bool ExcludePreparationActions = true;

    public void Normalize()
    {
        // 현재 전투 밸런스의 확정값을 유지합니다.
        ExchangeParticipantCharge = 5;
        OneSidedParticipantCharge = 5;
    }
}
