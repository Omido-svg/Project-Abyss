using System;
using UnityEngine;

/// <summary>
/// 스킬이 Effect Template을 재사용하면서 스킬별 수치만 덮어쓰기 위한 값 모음입니다.
/// Template에 없는 의미를 억지로 해석하지 않고, 각 Effect가 자신에게 필요한 값만 읽습니다.
/// </summary>
[Serializable]
public sealed class SkillEffectOverrides
{
    [Header("Status")]
    public bool OverrideStack;
    [Min(1)] public int Stack = 1;
    public bool OverrideDuration;
    [Min(1)] public int Duration = 3;

    [Header("Numeric")]
    public bool OverrideAmount;
    public int Amount = 1;
    public bool OverrideMinimum;
    public int Minimum;
    public bool OverrideMaximum;
    public int Maximum = 999;
    public bool OverrideFlatValue;
    public int FlatValue;
    public bool OverrideMultiplier;
    public float Multiplier = 1f;

    [Header("Optional Contract")]
    public bool OverrideResourceKey;
    public string ResourceKey = string.Empty;
    public bool OverrideForceCharacterStatus;
    public bool ForceCharacterStatus;
    public bool OverrideGiveToSelectedTarget;
    public bool GiveToSelectedTarget;

    public bool HasAnyOverride =>
        OverrideStack || OverrideDuration || OverrideAmount ||
        OverrideMinimum || OverrideMaximum || OverrideFlatValue ||
        OverrideMultiplier || OverrideResourceKey ||
        OverrideForceCharacterStatus || OverrideGiveToSelectedTarget;

    public int ResolveStack(int fallback) =>
        OverrideStack ? Mathf.Max(1, Stack) : Mathf.Max(1, fallback);

    public int ResolveDuration(int fallback) =>
        OverrideDuration ? Mathf.Max(1, Duration) : Mathf.Max(1, fallback);

    public int ResolveAmount(int fallback) =>
        OverrideAmount ? Amount : fallback;

    public int ResolveMinimum(int fallback) =>
        OverrideMinimum ? Minimum : fallback;

    public int ResolveMaximum(int fallback) =>
        OverrideMaximum ? Maximum : fallback;

    public int ResolveFlatValue(int fallback) =>
        OverrideFlatValue ? FlatValue : fallback;

    public float ResolveMultiplier(float fallback) =>
        OverrideMultiplier ? Multiplier : fallback;

    public string ResolveResourceKey(string fallback) =>
        OverrideResourceKey ? ResourceKey : fallback;

    public bool ResolveForceCharacterStatus(bool fallback) =>
        OverrideForceCharacterStatus ? ForceCharacterStatus : fallback;

    public bool ResolveGiveToSelectedTarget(bool fallback) =>
        OverrideGiveToSelectedTarget ? GiveToSelectedTarget : fallback;
}
