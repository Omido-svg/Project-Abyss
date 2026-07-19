using System;

[Serializable]
public struct ClashRollVisualStep
{
    public int RoundIndex;

    // 합 판정에 사용된 최종 수치.
    public int AttackerValue;
    public int TargetValue;

    public RollResult AttackerRollResult;
    public RollResult TargetRollResult;

    public int AttackerSpeedModifier;
    public int TargetSpeedModifier;

    public int AttackerMomentumModifier;
    public int TargetMomentumModifier;

    public bool AttackerCritical;
    public bool TargetCritical;

    // 굴림 소모전 메타데이터.
    public bool IsOneSided;
    public bool WasCancelled;
    public int AttackerDamage;
    public int TargetDamage;

    public int AttackerFinalPower =>
        AttackerRollResult?.FinalPower ??
        AttackerValue -
        AttackerSpeedModifier -
        AttackerMomentumModifier;

    public int TargetFinalPower =>
        TargetRollResult?.FinalPower ??
        TargetValue -
        TargetSpeedModifier -
        TargetMomentumModifier;

    public int AttackerClashPower => AttackerValue;
    public int TargetClashPower => TargetValue;

    public bool IsTie => AttackerValue == TargetValue;
    public bool AttackerWon => AttackerValue > TargetValue;
    public bool TargetWon => TargetValue > AttackerValue;

    public ClashRollVisualStep(
        int attackerValue,
        int targetValue)
        : this(
            0,
            attackerValue,
            targetValue,
            null,
            null,
            0,
            0,
            0,
            0,
            false,
            false)
    {
    }

    // 기존 호출부 호환.
    public ClashRollVisualStep(
        int attackerValue,
        int targetValue,
        RollResult attackerRollResult,
        RollResult targetRollResult,
        int attackerSpeedModifier,
        int targetSpeedModifier)
        : this(
            0,
            attackerValue,
            targetValue,
            attackerRollResult,
            targetRollResult,
            attackerSpeedModifier,
            targetSpeedModifier,
            attackerRollResult?.MomentumModifier ?? 0,
            targetRollResult?.MomentumModifier ?? 0,
            attackerRollResult?.IsCritical ?? false,
            targetRollResult?.IsCritical ?? false)
    {
    }

    public ClashRollVisualStep(
        int roundIndex,
        int attackerValue,
        int targetValue,
        RollResult attackerRollResult,
        RollResult targetRollResult,
        int attackerSpeedModifier,
        int targetSpeedModifier,
        int attackerMomentumModifier,
        int targetMomentumModifier,
        bool attackerCritical,
        bool targetCritical)
    {
        RoundIndex = roundIndex;
        AttackerValue = attackerValue;
        TargetValue = targetValue;
        AttackerRollResult = attackerRollResult;
        TargetRollResult = targetRollResult;
        AttackerSpeedModifier = attackerSpeedModifier;
        TargetSpeedModifier = targetSpeedModifier;
        AttackerMomentumModifier = attackerMomentumModifier;
        TargetMomentumModifier = targetMomentumModifier;
        AttackerCritical = attackerCritical;
        TargetCritical = targetCritical;
        IsOneSided = false;
        WasCancelled = false;
        AttackerDamage = 0;
        TargetDamage = 0;
    }

    public ClashRollVisualStep Swapped()
    {
        return new ClashRollVisualStep(
            RoundIndex,
            TargetValue,
            AttackerValue,
            TargetRollResult,
            AttackerRollResult,
            TargetSpeedModifier,
            AttackerSpeedModifier,
            TargetMomentumModifier,
            AttackerMomentumModifier,
            TargetCritical,
            AttackerCritical)
        {
            IsOneSided = IsOneSided,
            WasCancelled = WasCancelled,
            AttackerDamage = TargetDamage,
            TargetDamage = AttackerDamage
        };
    }
}