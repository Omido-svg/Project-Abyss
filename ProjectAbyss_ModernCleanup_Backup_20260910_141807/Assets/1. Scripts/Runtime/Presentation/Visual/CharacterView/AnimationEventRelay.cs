using System;
using UnityEngine;

/// <summary>
/// Timeline-only 전환 전 Prefab/AnimationClip 직렬화 호환을 위해 남겨 둔 폐기 예정 Relay입니다.
/// 신규 전투 스킬은 이 Component와 Animation Event를 사용하지 않습니다.
/// 기존 Prefab에 남은 컴포넌트는 Prefab 점검 과정에서 제거합니다.
/// </summary>
[Obsolete("Combat skill timing is authored only by Timeline Event Clips.")]
[AddComponentMenu("")]
public sealed class AnimationEventRelay : MonoBehaviour
{
    [Obsolete("Use Timeline Hit Event Clip.")]
    public void AnimationEvent_HitFrame() { }

    [Obsolete("Use Timeline Vfx Event Clip.")]
    public void AnimationEvent_EffectFrame() { }

    [Obsolete("Timeline duration controls completion.")]
    public void AnimationEvent_End() { }
}