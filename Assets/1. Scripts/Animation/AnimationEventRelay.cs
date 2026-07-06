using System;
using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    public event Action HitFrame;
    public event Action EffectFrame;
    public event Action AnimationEnd;

    public void AnimationEvent_HitFrame()
    {
        HitFrame?.Invoke();
    }

    public void AnimationEvent_EffectFrame()
    {
        EffectFrame?.Invoke();
    }

    public void AnimationEvent_End()
    {
        AnimationEnd?.Invoke();
    }
}