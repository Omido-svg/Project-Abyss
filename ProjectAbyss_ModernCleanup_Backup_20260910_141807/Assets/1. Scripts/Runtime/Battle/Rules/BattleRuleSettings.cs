using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class BattleRuleSettings
{
    public MomentumRuleSettings Momentum = new();
    public FervorRuleSettings Fervor = new();
    public StaggerRuleSettings Stagger = new();
    public ClashRuleSettings Clash = new();
    public EnergyRuleSettings Energy = new();
    public PrestigeRuleSettings Prestige = new();
    public ProgressionRuleSettings Progression = new();

    public void Normalize()
    {
        Momentum ??= new MomentumRuleSettings();
        Fervor ??= new FervorRuleSettings();
        Stagger ??= new StaggerRuleSettings();
        Clash ??= new ClashRuleSettings();
        Energy ??= new EnergyRuleSettings();
        Prestige ??= new PrestigeRuleSettings();
        Progression ??= new ProgressionRuleSettings();

        Momentum.Normalize();
        Fervor.Normalize();
        Stagger.Normalize();
        Clash.Normalize();
        Energy.Normalize();
        Prestige.Normalize();
        Progression.Normalize();
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

    [Header("Gameplay v5 — exchange shifts")]
    [Tooltip("일반 성공 교환 한 번의 총 기세 이동량입니다.")]
    [Min(0)] public int HitShift = 20;

    [Tooltip("Duel vs Duel의 개별 교환 승리 한 번이 만드는 총 기세 이동량입니다. HitShift에 더하는 값이 아닙니다.")]
    [Min(0)] public int DuelExchangeTotalShift = 40;

    [Tooltip("발악 중 일반 적중 이동 배수. 발악 판정은 직전 턴 종료 상태를 이번 턴 전체에 고정합니다.")]
    [Min(1)] public int LastStandHitShiftMultiplier = 2;

    // 이전 프로젝트 데이터/Diagnostics 역직렬화 호환. Gameplay v5 피해에는 사용하지 않는다.
    [HideInInspector] public float LastStandMultiplier = 1f;
    [HideInInspector] public float DisadvantageMultiplier = 1f;
    [HideInInspector] public float BalanceMultiplier = 1f;
    [HideInInspector] public float AdvantageMultiplier = 1f;
    [HideInInspector] public float OverwhelmMultiplier = 1f;
    [HideInInspector] public float MaximumOverwhelmMultiplier = 1f;
    [HideInInspector, FormerlySerializedAs("DuelExchangeShift")]
    public int LegacyDuelExchangeShift = 0;

    /// <summary>
    /// Legacy API compatibility: 기존 코드는 DuelExchangeShift를 "Hit에 더하는 추가량"으로 읽었다.
    /// Gameplay v5 내부 진실은 DuelExchangeTotalShift이며 이 값은 총량-일반 Hit량을 반환한다.
    /// </summary>
    public int DuelExchangeShift =>
        Mathf.Max(0, DuelExchangeTotalShift - HitShift);

    public void Normalize()
    {
        Minimum = -100;
        Maximum = 100;
        LastStandThreshold = -70;
        DisadvantageThreshold = -30;
        AdvantageThreshold = 30;
        OverwhelmThreshold = 70;
        HitShift = Mathf.Max(0, HitShift <= 5 ? 20 : HitShift);
        DuelExchangeTotalShift = Mathf.Max(HitShift, DuelExchangeTotalShift <= 20 ? 40 : DuelExchangeTotalShift);
        LastStandHitShiftMultiplier = Mathf.Max(1, LastStandHitShiftMultiplier);
        LastStandMultiplier = DisadvantageMultiplier = BalanceMultiplier =
            AdvantageMultiplier = OverwhelmMultiplier = MaximumOverwhelmMultiplier = 1f;
    }
}

[Serializable]
public sealed class FervorRuleSettings
{
    [Tooltip("짓누름/우세/중립/열세·짓눌림 턴 종료 시 얻는 고조 칸.")]
    [Min(0)] public int OverwhelmGain = 10;
    [Min(0)] public int AdvantageGain = 5;
    [Min(0)] public int BalanceGain = 2;
    [Min(0)] public int DisadvantageGain = 0;
    [Min(0)] public int LastStandGain = 0;

    [Tooltip("열광 0→1 / 1→2 / 2→3에 필요한 추가 고조 칸.")]
    [Min(1)] public int Level1Cost = 4;
    [Min(1)] public int Level2Cost = 8;
    [Min(1)] public int Level3Cost = 10;
    [Range(0, 3)] public int MaximumLevel = 3;

    public int GetCostForNextLevel(int currentLevel) => currentLevel switch
    {
        0 => Level1Cost,
        1 => Level2Cost,
        2 => Level3Cost,
        _ => int.MaxValue
    };

    public int GetGain(MomentumState state) => state switch
    {
        MomentumState.Overwhelm => OverwhelmGain,
        MomentumState.Advantage => AdvantageGain,
        MomentumState.Balance => BalanceGain,
        MomentumState.Disadvantage => DisadvantageGain,
        MomentumState.LastStand => LastStandGain,
        _ => 0
    };

    public void Normalize()
    {
        OverwhelmGain = Mathf.Max(0, OverwhelmGain);
        AdvantageGain = Mathf.Max(0, AdvantageGain);
        BalanceGain = Mathf.Max(0, BalanceGain);
        DisadvantageGain = Mathf.Max(0, DisadvantageGain);
        LastStandGain = Mathf.Max(0, LastStandGain);
        Level1Cost = Mathf.Max(1, Level1Cost);
        Level2Cost = Mathf.Max(1, Level2Cost);
        Level3Cost = Mathf.Max(1, Level3Cost);
        MaximumLevel = Mathf.Clamp(MaximumLevel, 0, 3);
    }
}

[Serializable]
public sealed class StaggerRuleSettings
{
    [Min(1)] public int PlayerMaximum = 350;
    [Min(1)] public int NormalEnemyMaximum = 100;
    [Min(1)] public int EliteEnemyMaximum = 100;
    [Min(1)] public int BossMaximum = 400;

    [Min(0f)] public float VulnerabilityHpResistanceOverride = 2f;
    [Min(0f)] public float StaggerRollSelfRecoveryRatio = 1f;

    public int GetTierMaximum(CombatantTier tier) => tier switch
    {
        CombatantTier.Player => PlayerMaximum,
        CombatantTier.Boss => BossMaximum,
        CombatantTier.EliteEnemy => EliteEnemyMaximum,
        _ => NormalEnemyMaximum
    };

    public void Normalize()
    {
        PlayerMaximum = Mathf.Max(1, PlayerMaximum);
        NormalEnemyMaximum = Mathf.Max(1, NormalEnemyMaximum);
        EliteEnemyMaximum = Mathf.Max(1, EliteEnemyMaximum);
        BossMaximum = Mathf.Max(1, BossMaximum);
        VulnerabilityHpResistanceOverride = Mathf.Max(0f, VulnerabilityHpResistanceOverride);
        StaggerRollSelfRecoveryRatio = Mathf.Max(0f, StaggerRollSelfRecoveryRatio);
    }
}

[Serializable]
public sealed class ProgressionRuleSettings
{
    [Range(1, 5)] public int MaximumSkillUpgradeLevel = 2;
    [Min(0f)] public float SecondUpgradeCostMultiplier = 1.5f;
    public int NormalAttackLoadoutLimit = 3;
    public int DuelLoadoutLimit = 2;
    public int PreparationLoadoutLimit = 3;
    public int PrestigeLoadoutLimit = 1;

    public void Normalize()
    {
        MaximumSkillUpgradeLevel = Mathf.Clamp(MaximumSkillUpgradeLevel, 1, 5);
        SecondUpgradeCostMultiplier = Mathf.Max(0f, SecondUpgradeCostMultiplier);
        NormalAttackLoadoutLimit = Mathf.Max(0, NormalAttackLoadoutLimit);
        DuelLoadoutLimit = Mathf.Max(0, DuelLoadoutLimit);
        PreparationLoadoutLimit = Mathf.Max(0, PreparationLoadoutLimit);
        PrestigeLoadoutLimit = Mathf.Max(0, PrestigeLoadoutLimit);
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
        DefaultExchangeRollCount = Mathf.Clamp(DefaultExchangeRollCount, 1, 8);
        SpeedWeight = Mathf.Max(0, SpeedWeight);
        MaxTieRerolls = Mathf.Max(1, MaxTieRerolls);
        MaxCharacterRerollsPerExchange = Mathf.Max(1, MaxCharacterRerollsPerExchange);
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
    [Header("Exchange-based charge")]
    [Min(0)] public int ExchangeParticipantCharge = 5;
    [Min(0)] public int OneSidedParticipantCharge = 5;

    [Tooltip("준비 행동은 교환 위세 충전 대상에서 제외합니다.")]
    [FormerlySerializedAs("PreparationDoesNotCharge")]
    public bool ExcludePreparationActions = true;

    public void Normalize()
    {
        ExchangeParticipantCharge = 5;
        OneSidedParticipantCharge = 5;
    }
}
