using System;
using UnityEngine;

[Serializable]
public class BattleCameraShakeSettings
{
    [Header("Legacy")]
    public float Duration = 0.15f;
    public float Frequency = 25f;
    public float PositionStrength = 0.1f;
    public float RotationStrength = 2f;

    public AnimationCurve StrengthCurve =
        AnimationCurve.EaseInOut(
            0f,
            1f,
            1f,
            0f);

    [Header("Cinemachine Impulse")]
    public bool UseImpulse = true;
    public float ImpulseForce = 1f;
}