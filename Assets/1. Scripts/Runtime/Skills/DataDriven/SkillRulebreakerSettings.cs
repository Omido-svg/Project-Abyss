using System;
using UnityEngine;

/// <summary>
/// 명시적인 부위 파괴 권한. None은 권한 없음, WeakenedOnly는 정본 기본 게이트,
/// IgnoreWeakenedPrerequisite는 카드가 명시한 예외에만 사용한다.
/// </summary>
public enum PartBreakMode
{
    None = 0,
    WeakenedOnly = 1,
    IgnoreWeakenedPrerequisite = 2
}

/// <summary>
/// 특수 카드가 공통 전투 파이프라인을 우회하지 않고 요구사항을 표현하기 위한 데이터 훅.
/// 수치가 미정인 항목은 SO에서 0/비활성 상태로 남길 수 있도록 기본값을 주지 않는다.
/// </summary>
[Serializable]
public sealed class SkillRulebreakerSettings
{
    [Tooltip("활성화된 룰브레이커 authoring만 런타임에서 해석합니다.")]
    public bool Enabled;

    [Header("Dynamic Cost")]
    [Min(0)] public int EnergyCostReductionPerCommittedUse;
    [Min(0)] public int MinimumEnergyCost;

    [Header("Duel Match")]
    public bool MirrorThisSkillToOpponentOnDuelMatch;
    public bool IgnoreOwnerRollModifiers;

    [Header("Bleeding Explosion")]
    public bool ExplodeBleedingOncePerAction;
    [Min(0)] public int BleedingExplosionMultiplier;

    [Header("Target / One-sided")]
    public bool SuppressFriendlyOneSidedHitsOnSameTargetSlot;

    [Header("Bleeding -> Break Authority")]
    public bool ConsumeAllOwnerBleedingOnExecute;
    [Min(0)] public int BreakAuthorityBleedingThreshold;
    [Min(0)] public int IgnoreWeakenPrerequisiteBleedingThreshold;

    [Header("Momentum Conditions — legacy 0916")]
    public bool RequirePreviousTurnLastStand;
    public bool ApplyCurrentLastStandPowerBonus;
    public int CurrentLastStandPowerBonus;

    [Header("Momentum Conditions — 0917")]
    [Tooltip("현재 기세가 열세(B<=-30) 또는 짓눌림(B<=-70)이면 보너스를 적용합니다.")]
    public bool ApplyCurrentDisadvantageOrWorsePowerBonus;
    public int CurrentDisadvantageOrWorsePowerBonus;

    [Header("Speed")]
    public bool GrantInfiniteAttackSlotSpeedThisTurn;

    public bool HasDynamicCost =>
        Enabled && EnergyCostReductionPerCommittedUse > 0;

    public int ResolveMinimumEnergyCost()
    {
        // 최소값 0은 데이터 Unset을 뜻한다. 동적 비용이 실제 활성일 때만 정본 기본 최소 1을 사용한다.
        return HasDynamicCost
            ? Mathf.Max(1, MinimumEnergyCost)
            : Mathf.Max(0, MinimumEnergyCost);
    }

#if UNITY_EDITOR
    public void Sanitize()
    {
        EnergyCostReductionPerCommittedUse = Mathf.Max(0, EnergyCostReductionPerCommittedUse);
        MinimumEnergyCost = Mathf.Max(0, MinimumEnergyCost);
        BleedingExplosionMultiplier = Mathf.Max(0, BleedingExplosionMultiplier);
        BreakAuthorityBleedingThreshold = Mathf.Max(0, BreakAuthorityBleedingThreshold);
        IgnoreWeakenPrerequisiteBleedingThreshold = Mathf.Max(0, IgnoreWeakenPrerequisiteBleedingThreshold);
    }
#endif
}
