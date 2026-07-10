using System;

[Serializable]
public struct ClashRollVisualStep
{
    public int AttackerValue;
    public int TargetValue;

    public RollResult AttackerRollResult;
    public RollResult TargetRollResult;

    public int AttackerSpeedModifier;
    public int TargetSpeedModifier;

    public bool IsTie =>
        AttackerValue == TargetValue;

    public ClashRollVisualStep(
        int attackerValue,
        int targetValue)
    {
        AttackerValue = attackerValue;
        TargetValue = targetValue;

        AttackerRollResult = null;
        TargetRollResult = null;

        AttackerSpeedModifier = 0;
        TargetSpeedModifier = 0;
    }

    public ClashRollVisualStep(
        int attackerValue,
        int targetValue,
        RollResult attackerRollResult,
        RollResult targetRollResult,
        int attackerSpeedModifier,
        int targetSpeedModifier)
    {
        AttackerValue = attackerValue;
        TargetValue = targetValue;

        AttackerRollResult = attackerRollResult;
        TargetRollResult = targetRollResult;

        AttackerSpeedModifier = attackerSpeedModifier;
        TargetSpeedModifier = targetSpeedModifier;
    }
}