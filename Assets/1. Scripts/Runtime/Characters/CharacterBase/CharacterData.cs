using System.Collections.Generic;
using UnityEngine;

public enum TurnStartEnergyPolicy
{
    GlobalGain = 0,
    GainFlat = 1,
    RefillToMaximum = 2,
    None = 3
}

[CreateAssetMenu(menuName = "Character/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character")]
    public string CharacterName;
    public CombatantTier CombatantTier = CombatantTier.Player;

    [Header("Battle UI Presentation")]
    public Sprite Portrait;
    public Sprite DetailArtwork;
    public string RoleName;
    [TextArea(2, 6)] public string UiSummary;

    [Header("Target Model")]
    public CharacterTargetMode TargetMode = CharacterTargetMode.Auto;
    [Min(1)] public int SingleHpMax = 1;

    [Header("Prestige")]
    [Min(0)] public int maxPrestige = 100;

    [Header("Energy / Light")]
    [Min(1)] public int maxEnergy = 3;
    public TurnStartEnergyPolicy TurnStartEnergyPolicy = TurnStartEnergyPolicy.GlobalGain;
    [Min(0)] public int TurnStartEnergyAmount = 1;

    [Header("Character-specific slots")]
    public List<CharacterSlotConfig> ActionSlots = new();
    [Header("Replaceable skill loadout")]
    public CharacterCombatLoadout CombatLoadout;
    [Header("Boss phases")]
    public List<BossPhaseData> BossPhases = new();

    [Header("HP Resistance — 절단 / 타격 / 관통")]
    public PhysicalResistanceProfile PhysicalResistances = new PhysicalResistanceProfile();

    [Header("Stagger Resistance — HP 내성과 완전 별개")]
    public PhysicalResistanceProfile StaggerResistances = new PhysicalResistanceProfile();

    [Header("Stagger Gauge")]
    public bool EnableStaggerGauge = true;
    [Tooltip("끄면 CombatantTier의 확정 기본값(Player 350 / Normal·Elite 100 / Boss 400)을 사용합니다.")]
    public bool OverrideMaxStaggerGauge;
    [Min(1)] public int MaxStaggerGauge = 100;
    [HideInInspector, Min(0f)] public float StaggerDamageRatio = 1f;

    [Header("Speed")]
    public int minSpeed = 3;
    public int maxSpeed = 8;

    [Header("Initial Custom Resources")]
    public List<CombatResourceDefinition> InitialResources = new();

    public int GetEffectiveMaxStaggerGauge(BattleRuleSettings rules = null)
    {
        if (OverrideMaxStaggerGauge)
            return Mathf.Max(1, MaxStaggerGauge);

        StaggerRuleSettings stagger = rules?.Stagger ?? new StaggerRuleSettings();
        stagger.Normalize();
        return stagger.GetTierMaximum(CombatantTier);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SingleHpMax = Mathf.Max(1, SingleHpMax);
        maxPrestige = Mathf.Max(0, maxPrestige);
        maxEnergy = Mathf.Max(1, maxEnergy);
        TurnStartEnergyAmount = Mathf.Max(0, TurnStartEnergyAmount);
        if (maxSpeed < minSpeed) maxSpeed = minSpeed;
        PhysicalResistances ??= new PhysicalResistanceProfile();
        StaggerResistances ??= new PhysicalResistanceProfile();
        PhysicalResistances.Sanitize();
        StaggerResistances.Sanitize();
        MaxStaggerGauge = Mathf.Max(1, MaxStaggerGauge);
        InitialResources ??= new List<CombatResourceDefinition>();
        ActionSlots ??= new List<CharacterSlotConfig>();
        BossPhases ??= new List<BossPhaseData>();
        for (int i = 0; i < ActionSlots.Count; i++) ActionSlots[i]?.Sanitize(i);
    }
#endif
}
