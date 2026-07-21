using System;
using UnityEngine;

public enum SkillCameraImpactTiming
{
    OnActionStart,
    OnClashRoll,
    OnHitFrame,
    OnClashFinalResult,
    AfterAction
}

public enum SkillCameraImpactShakeTiming
{
    OnPulseStart,
    OnZoomPeak
}

/// <summary>
/// 스킬 카메라 위에 겹쳐 재생되는 짧은 FOV 줌 펄스.
/// CameraPoint, Group Shot, Pose Shot의 위치/회전을 바꾸지 않고
/// 현재 활성 CinemachineCamera의 Lens.FieldOfView만 잠시 변경한 뒤 복구한다.
/// </summary>
[Serializable]
public sealed class SkillCameraImpactPulse
{
    [Header("Identity")]
    public string DisplayName = "Impact Zoom";

    public bool Enabled = true;

    public SkillCameraImpactTiming Timing =
        SkillCameraImpactTiming.OnHitFrame;

    [Header("Context Filters")]
    [Tooltip("합 연출 중에만 재생합니다. 일반 단방향 공격에서는 재생하지 않습니다.")]
    public bool OnlyDuringClash;

    [Tooltip("일방 공격 교환에서는 재생하지 않습니다.")]
    public bool ExcludeOneSided;

    [Tooltip("실제 표시 피해가 1 이상일 때만 재생합니다.")]
    public bool RequirePositiveDamage = true;

    [Tooltip("치명타일 때만 재생합니다.")]
    public bool RequireCritical;

    [Tooltip("이 타격으로 부위가 파괴되었을 때만 재생합니다.")]
    public bool RequirePartBreak;

    [Tooltip("이 타격으로 대상이 사망했을 때만 재생합니다.")]
    public bool RequireKill;

    [Tooltip("특정 HitFrame 번호에만 적용합니다. 0부터 시작합니다.")]
    public bool UseHitIndexFilter;

    [Min(0)]
    public int HitIndex;

    [Tooltip("특정 합 교환 번호에만 적용합니다. 0부터 시작합니다.")]
    public bool UseExchangeIndexFilter;

    [Min(0)]
    public int ExchangeIndex;

    [Header("FOV Zoom Pulse")]
    [Tooltip(
        "현재 FOV에 더할 값입니다. " +
        "음수는 빠른 줌인, 양수는 빠른 줌아웃입니다.")]
    [Range(-120f, 120f)]
    public float FieldOfViewDelta = -7f;

    [Min(0f)]
    public float StartDelay;

    [Tooltip("현재 FOV에서 목표 FOV까지 이동하는 시간")]
    [Min(0f)]
    public float ZoomInDuration = 0.035f;

    [Tooltip("목표 FOV를 유지하는 시간")]
    [Min(0f)]
    public float HoldDuration = 0.015f;

    [Tooltip("원래 FOV로 복귀하는 시간")]
    [Min(0f)]
    public float ZoomOutDuration = 0.09f;

    public AnimationCurve ZoomInCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f);

    public AnimationCurve ZoomOutCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f);

    [Tooltip(
        "Hit Stop 또는 Time.timeScale 변화 중에도 " +
        "카메라 펄스를 계속 재생합니다.")]
    public bool UseUnscaledTime = true;

    [Header("Optional Impulse")]
    public bool UseShake;

    public SkillCameraImpactShakeTiming ShakeTiming =
        SkillCameraImpactShakeTiming.OnZoomPeak;

    public BattleCameraShakeSettings Shake =
        new BattleCameraShakeSettings();

    public bool CanPlay
    {
        get
        {
            bool hasZoom =
                Mathf.Abs(FieldOfViewDelta) > 0.001f;

            bool hasShake =
                UseShake &&
                Shake != null &&
                Shake.CanPlay;

            return Enabled &&
                   (hasZoom || hasShake);
        }
    }

    public void Sanitize()
    {
        HitIndex = Mathf.Max(0, HitIndex);
        ExchangeIndex = Mathf.Max(0, ExchangeIndex);

        FieldOfViewDelta =
            Mathf.Clamp(
                FieldOfViewDelta,
                -120f,
                120f);

        StartDelay = Mathf.Max(0f, StartDelay);
        ZoomInDuration = Mathf.Max(0f, ZoomInDuration);
        HoldDuration = Mathf.Max(0f, HoldDuration);
        ZoomOutDuration = Mathf.Max(0f, ZoomOutDuration);

        ZoomInCurve ??=
            AnimationCurve.EaseInOut(
                0f,
                0f,
                1f,
                1f);

        ZoomOutCurve ??=
            AnimationCurve.EaseInOut(
                0f,
                0f,
                1f,
                1f);

        Shake ??=
            new BattleCameraShakeSettings();

        Shake.Sanitize();
    }

    public bool Matches(
        SkillCameraImpactTiming timing,
        int hitIndex,
        int exchangeIndex,
        int damage,
        bool isCritical,
        bool brokePart,
        bool wasKilled,
        bool isClash,
        bool isOneSided)
    {
        if (!Enabled ||
            Timing != timing)
        {
            return false;
        }

        if (OnlyDuringClash &&
            !isClash)
        {
            return false;
        }

        if (ExcludeOneSided &&
            isOneSided)
        {
            return false;
        }

        if (RequirePositiveDamage &&
            damage <= 0)
        {
            return false;
        }

        if (RequireCritical &&
            !isCritical)
        {
            return false;
        }

        if (RequirePartBreak &&
            !brokePart)
        {
            return false;
        }

        if (RequireKill &&
            !wasKilled)
        {
            return false;
        }

        if (UseHitIndexFilter &&
            hitIndex != HitIndex)
        {
            return false;
        }

        if (UseExchangeIndexFilter &&
            exchangeIndex != ExchangeIndex)
        {
            return false;
        }

        return CanPlay;
    }
}
