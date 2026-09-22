using UnityEngine;

/// <summary>
/// 0922 PresenceTimed 저장 모델의 공통 기반.
/// Gameplay N은 없고, 동일 상태 재부여는 효과를 중첩하지 않은 채 Duration=max(current,new)만 적용한다.
/// Stack=1은 기존 condition/UI 경로를 깨지 않기 위한 legacy presence marker다.
/// </summary>
public abstract class PresenceTimedStatus : StatusEffect
{
    public override StatusEffectStorageKind StorageKind =>
        StatusEffectStorageKind.PresenceTimed;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnEnd;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.RefreshDuration;

    protected PresenceTimedStatus(
        string name,
        int duration)
    {
        Name = name;
        Stack = 1;
        Duration = Mathf.Max(1, duration);
    }

    public override void Merge(StatusEffect other)
    {
        if (other == null ||
            other.GetType() != GetType())
        {
            return;
        }

        Duration = Mathf.Max(
            Duration,
            Mathf.Max(1, other.Duration));

        Stack = 1;
    }
}
