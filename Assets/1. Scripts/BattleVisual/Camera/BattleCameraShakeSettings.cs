using System;
using UnityEngine;

[Serializable]
public class BattleCameraShakeSettings
{
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
}