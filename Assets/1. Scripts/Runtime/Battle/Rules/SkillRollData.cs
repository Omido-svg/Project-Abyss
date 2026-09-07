using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillRollData
{
    [Min(0)] public int Index;
    public CombatRollType Type = CombatRollType.Attack;

    [Header("Physical type — per roll")]
    [Tooltip("켜면 이 굴림만의 절단/관통/타격 타입을 사용합니다. 끄면 SkillDefinition의 Legacy 타입을 사용합니다. 유진은 무기 타입이 항상 최우선입니다.")]
    public bool OverridePhysicalType;
    public PhysicalDamageType PhysicalType = PhysicalDamageType.Cut;

    [Header("Dice power")]
    [Tooltip("AbsoluteRange: Min~Max가 최종 위력입니다. BasePlusRoll: 스킬 BasePower + Min~Max입니다.")]
    public DicePowerMode DiceMode = DicePowerMode.AbsoluteRange;
    [Min(0)] public int MinPower = 1;
    [Min(0)] public int MaxPower = 1;

    public RollRngSource RngSource = RollRngSource.CharacterDefault;

    [Header("Coin — one discrete result, not destructible coins")]
    [Range(0f, 1f)] public float CoinFrontChance = 0.5f;
    [Min(0)] public int CoinBackPower = 4;
    [Min(0)] public int CoinFrontPower = 12;
    public bool CoinFrontIsCritical;

    [Header("Slot — two reels multiplied")]
    [Range(1, 9)] public int SlotMinimum = 1;
    [Range(1, 9)] public int SlotMaximum = 9;

    [Header("Chinchiro — exact power by result")]
    public int ChinchiroHifumiPower = -4;
    public int ChinchiroBlankPower = 3;
    public int ChinchiroMokuPower = 7;
    public int ChinchiroShigoroPower = 20;
    public int ChinchiroArashiPower = 24;

    [Header("Judgment only — never added to damage")]
    public int JudgmentModifier;

    [Tooltip("같은 굴림 인덱스의 값을 행동 전체에서 재사용합니다.")]
    public bool ReuseValueAcrossAction;

    [Header("Per-roll effects — reusable template + parameters")]
    public List<SkillEffectEntry> OnWinEffectEntries = new();
    public List<SkillEffectEntry> OnLoseEffectEntries = new();

    [HideInInspector] public List<SkillEffectDefinition> OnWinEffects = new();
    [HideInInspector] public List<SkillEffectDefinition> OnLoseEffects = new();

    public int SafeMinPower => Mathf.Max(0, Mathf.Min(MinPower, MaxPower));
    public int SafeMaxPower => Mathf.Max(SafeMinPower, Mathf.Max(MinPower, MaxPower));

    public int ResolveDiceBasePower(int skillBasePower) =>
        DiceMode == DicePowerMode.BasePlusRoll
            ? skillBasePower
            : 0;

    public int GetDiceFinalMinPower(int skillBasePower) =>
        ResolveDiceBasePower(skillBasePower) + SafeMinPower;

    public int GetDiceFinalMaxPower(int skillBasePower) =>
        ResolveDiceBasePower(skillBasePower) + SafeMaxPower;

    public void Sanitize(int index)
    {
        Index = Mathf.Max(0, index);
        int min = SafeMinPower;
        int max = SafeMaxPower;
        MinPower = min;
        MaxPower = max;
        CoinFrontChance = Mathf.Clamp01(CoinFrontChance);
        CoinBackPower = Mathf.Max(0, CoinBackPower);
        CoinFrontPower = Mathf.Max(0, CoinFrontPower);
        SlotMinimum = Mathf.Clamp(SlotMinimum, 1, 9);
        SlotMaximum = Mathf.Clamp(SlotMaximum, SlotMinimum, 9);
        OnWinEffectEntries ??= new List<SkillEffectEntry>();
        OnLoseEffectEntries ??= new List<SkillEffectEntry>();
        OnWinEffects ??= new List<SkillEffectDefinition>();
        OnLoseEffects ??= new List<SkillEffectDefinition>();
    }
}