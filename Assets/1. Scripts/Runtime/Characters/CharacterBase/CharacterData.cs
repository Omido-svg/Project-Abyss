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
    [Tooltip("1029 DMG-06: 정본의 명시적 예외로만 사용합니다. 켜면 MaxStaggerGauge 값을 그대로 사용합니다. 끄면 부위형은 '부위 1개의 최대 HP', 단일 HP형은 100으로 런타임에 계산합니다.")]
    public bool OverrideMaxStaggerGauge;
    [Min(1)] public int MaxStaggerGauge = 100;
    [HideInInspector, Min(0f)] public float StaggerDamageRatio = 1f;

    [Header("Speed")]
    public int minSpeed = 3;
    public int maxSpeed = 8;

    [Header("Initial Custom Resources")]
    public List<CombatResourceDefinition> InitialResources = new();

    // 1029 DMG-06: 등급별 기본 최대치(GetEffectiveMaxStaggerGauge)는 삭제했다.
    // 런타임 정본은 Character.ResolveMaxStaggerGauge → StaggerGaugeMaximumResolver 하나뿐이다.
    // StaggerRuleSettings의 등급별 필드는 데이터/검증용으로만 남고 게이지 생성에 쓰이지 않는다.

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