using System;

[Serializable]
public struct ClashRollVisualStep
{
    public int AttackerValue;
    public int TargetValue;

    public bool IsTie =>
        AttackerValue == TargetValue;

    public ClashRollVisualStep(
        int attackerValue,
        int targetValue)
    {
        AttackerValue = attackerValue;
        TargetValue = targetValue;
    }
}