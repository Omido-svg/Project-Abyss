using System;
using UnityEngine;

public enum AttackWeightSecondaryPartMode
{
    RandomValidTargetPoint = 0,
    MatchPrimaryPartType = 1,
    CharacterLevelDirect = 2,

    /// <summary>
    /// 같은 캐릭터의 다른 부위를 추가 타깃으로 사용합니다.
    /// 유진 적설처럼 한 적의 두 부위를 동시에 공격할 때 사용합니다.
    /// </summary>
    AnotherPartOnPrimaryCharacter = 3
}

[Serializable]
public sealed class AttackWeightSettings
{
    public const int MaximumSupportedWeight = 32;

    [Tooltip(
        "메인 타깃을 포함해 이 스킬이 한 번의 공격 굴림으로 " +
        "동시에 피해를 줄 수 있는 서로 다른 캐릭터 수입니다.")]
    [Min(1)]
    public int Weight = 1;

    [Tooltip(
        "추가 타깃의 부위를 고르는 방식입니다. " +
        "캐릭터 자체가 추가 타깃으로 뽑힌 뒤 이 규칙으로 부위를 정합니다.")]
    public AttackWeightSecondaryPartMode SecondaryPartMode =
        AttackWeightSecondaryPartMode.RandomValidTargetPoint;

    [Tooltip(
        "추가 타깃에 적용되는 피해 배율입니다. " +
        "1이면 메인 타깃과 같은 순수 위력을 사용합니다.")]
    [Min(0f)]
    public float SecondaryDamageMultiplier = 1f;

    [Tooltip(
        "추가 타깃의 랜덤 부위 후보에 이미 파괴된 부위를 포함합니다. " +
        "파괴 부위가 선택되면 기존 DamageManager 규칙에 따라 직접 HP 피해로 전환될 수 있습니다.")]
    public bool AllowBrokenSecondaryParts = true;

    public int EffectiveWeight =>
        Mathf.Clamp(
            Weight,
            1,
            MaximumSupportedWeight);

    public bool IsMultiTarget =>
        EffectiveWeight > 1;

    public void Sanitize()
    {
        Weight =
            Mathf.Clamp(
                Weight,
                1,
                MaximumSupportedWeight);

        SecondaryDamageMultiplier =
            Mathf.Max(
                0f,
                SecondaryDamageMultiplier);
    }
}