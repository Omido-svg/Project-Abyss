using System;
using UnityEngine;

[Serializable]
public sealed class BattleRuleSettings
{
    public MomentumRuleSettings Momentum = new();
    public ClashRuleSettings Clash = new();
    public EnergyRuleSettings Energy = new();
    public PrestigeRuleSettings Prestige = new();

    [Header("Legacy Compatibility")]
    public bool UseLegacyAttackDefenseStats;

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

    [Header("Shift")]
    [Min(0)] public int HitShift = 5;
    [Min(0)] public int DuelVictoryShift = 25;
    [Min(1)] public int LastStandHitShiftMultiplier = 2;

    public void Normalize()
    {
        // 2026-07-26 확정 규칙. 기존 Scene/Prefab에 직렬화된 구 수치도
        // 런타임에서는 이 고정값으로 정규화한다.
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
        DuelVictoryShift = 25;
        LastStandHitShiftMultiplier = 2;
    }
}

[Serializable]
public sealed class ClashRuleSettings
{
    [Range(2, 8)] public int DefaultExchangeRollCount = 3;
    [Min(0)] public int SpeedWeight = 1;
    [Min(1)] public int MaxTieRerolls = 64;
    public bool ConsumeResourceOnActionStart = true;

    public void Normalize()
    {
        DefaultExchangeRollCount = Mathf.Clamp(DefaultExchangeRollCount, 2, 8);
        SpeedWeight = Mathf.Max(0, SpeedWeight);
        MaxTieRerolls = Mathf.Max(1, MaxTieRerolls);
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
        DefaultMaximum = Mathf.Max(1, DefaultMaximum);
        TurnStartGain = Mathf.Max(0, TurnStartGain);
    }
}

[Serializable]
public sealed class PrestigeRuleSettings
{
    [Min(0)] public int ClashStartCharge = 1;
    [Min(0)] public int HitDealtCharge = 1;
    [Min(0)] public int HitTakenCharge = 1;
    [Min(0)] public int ClashVictoryCharge = 1;
    public bool PreparationDoesNotCharge = true;

    public void Normalize()
    {
        ClashStartCharge = Mathf.Max(0, ClashStartCharge);
        HitDealtCharge = Mathf.Max(0, HitDealtCharge);
        HitTakenCharge = Mathf.Max(0, HitTakenCharge);
        ClashVictoryCharge = Mathf.Max(0, ClashVictoryCharge);
    }
}