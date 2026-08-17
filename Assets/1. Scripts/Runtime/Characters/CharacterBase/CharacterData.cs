using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Character/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character")]
    public string CharacterName;
    public CombatantTier CombatantTier = CombatantTier.Player;

    [Header("Battle UI Presentation")]
    [Tooltip("캐릭터 상세 화면과 월드 HUD 보조 표시에서 사용하는 초상화입니다.")]
    public Sprite Portrait;

    [Tooltip("상세 화면의 큰 일러스트입니다. 비어 있으면 Portrait를 확대해 사용합니다.")]
    public Sprite DetailArtwork;

    [Tooltip("전투 상세 화면에 표시할 역할명입니다.")]
    public string RoleName;

    [TextArea(2, 6)]
    [Tooltip("전투 상세 화면의 요약 설명입니다.")]
    public string UiSummary;

    [Header("Target Model")]
    public CharacterTargetMode TargetMode = CharacterTargetMode.Auto;
    [Min(1)] public int SingleHpMax = 1;

    [Header("Prestige")]
    [Min(0)] public int maxPrestige = 100;

    [Header("Energy / Light")]
    [Tooltip("기본 상한은 3. 고티어 EnergyCapacityAugment가 이 값을 증가시킬 수 있습니다.")]
    [Min(1)] public int maxEnergy = 3;

    [Header("Character-specific slots")]
    public List<CharacterSlotConfig> ActionSlots = new();

    [Header("Replaceable skill loadout")]
    public CharacterCombatLoadout CombatLoadout;

    [Header("Boss phases")]
    public List<BossPhaseData> BossPhases = new();

    [Header("Physical Resistance — 절단 / 둔격 / 관통")]
    public PhysicalResistanceProfile PhysicalResistances =
        new PhysicalResistanceProfile();

    [Header("Stagger Gauge")]
    public bool EnableStaggerGauge = true;
    [Min(1)] public int MaxStaggerGauge = 100;
    [Min(0f)] public float StaggerDamageRatio = 1f;

    [Header("Speed")]
    public int minSpeed = 3;
    public int maxSpeed = 8;

    [Header("Initial Custom Resources")]
    public List<CombatResourceDefinition> InitialResources = new();

#if UNITY_EDITOR
    private void OnValidate()
    {
        SingleHpMax = Mathf.Max(1, SingleHpMax);
        maxPrestige = Mathf.Max(0, maxPrestige);
        maxEnergy = Mathf.Max(3, maxEnergy);
        if (maxSpeed < minSpeed) maxSpeed = minSpeed;
        PhysicalResistances ??= new PhysicalResistanceProfile();
        PhysicalResistances.Sanitize();
        MaxStaggerGauge = Mathf.Max(1, MaxStaggerGauge);
        StaggerDamageRatio = Mathf.Max(0f, StaggerDamageRatio);
        InitialResources ??= new List<CombatResourceDefinition>();
        ActionSlots ??= new List<CharacterSlotConfig>();
        BossPhases ??= new List<BossPhaseData>();
        for (int i = 0; i < ActionSlots.Count; i++) ActionSlots[i]?.Sanitize(i);
    }
#endif
}
