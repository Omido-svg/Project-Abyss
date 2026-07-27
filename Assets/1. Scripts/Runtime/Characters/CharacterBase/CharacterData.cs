using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Character/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character")]
    public string CharacterName;
    public CombatantTier CombatantTier = CombatantTier.Player;

    [Header("Battle UI Presentation")]
    [Tooltip("우측 상단 결투 현황과 상세 화면에서 사용하는 초상화입니다.")]
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

    [Header("Damage")]
    [Min(0f)] public float damageMultiplier = 1f;
    [Range(0f, 1f)] public float defensePenetration = 0f;

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
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        defensePenetration = Mathf.Clamp01(defensePenetration);
        if (maxSpeed < minSpeed) maxSpeed = minSpeed;
        InitialResources ??= new List<CombatResourceDefinition>();
        ActionSlots ??= new List<CharacterSlotConfig>();
        BossPhases ??= new List<BossPhaseData>();
        for (int i = 0; i < ActionSlots.Count; i++) ActionSlots[i]?.Sanitize(i);
    }
#endif
}