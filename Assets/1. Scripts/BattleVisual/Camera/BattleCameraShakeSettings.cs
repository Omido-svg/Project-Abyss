using System;
using UnityEngine;

[Serializable]
public class BattleCameraShakeSettings
{
    [Header("Cinemachine Impulse")]
    public bool UseImpulse = true;

    [Min(0f)]
    public float ImpulseForce = 1f;

    [Header("Legacy Serialized Values - Cinemachine 3 경로에서는 사용하지 않음")]
    [Min(0f)] public float Duration = 0.15f;
    [Min(0f)] public float Frequency = 25f;
    [Min(0f)] public float PositionStrength = 0.1f;
    [Min(0f)] public float RotationStrength = 2f;

    public AnimationCurve StrengthCurve =
        AnimationCurve.EaseInOut(
            0f,
            1f,
            1f,
            0f);

    public bool CanPlay => UseImpulse && ImpulseForce > 0f;

    public float GetSafeImpulseForce()
    {
        return Mathf.Max(0f, ImpulseForce);
    }

    public void Sanitize()
    {
        ImpulseForce = Mathf.Max(0f, ImpulseForce);
        Duration = Mathf.Max(0f, Duration);
        Frequency = Mathf.Max(0f, Frequency);
        PositionStrength = Mathf.Max(0f, PositionStrength);
        RotationStrength = Mathf.Max(0f, RotationStrength);
    }
}
