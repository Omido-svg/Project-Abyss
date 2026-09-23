using UnityEngine;

public interface ICommonRollShiftStatus
{
    int GetRollShift(BattleAction action);
}

public interface ICommonRollMaxReductionStatus
{
    int GetMaxReduction(BattleAction action);
}

public interface ICommonSpeedMaximumStatus
{
    int GetSpeedMaximumIncrease(BodyPart part);
}

public static class CommonStatusSpeedRules
{
    public static int GetSpeedMaximumIncrease(Character character, BodyPart part)
    {
        if (character == null)
            return 0;

        int total = 0;
        foreach (StatusEffect effect in character.StatusEffects)
        {
            if (effect is ICommonSpeedMaximumStatus speed)
                total += speed.GetSpeedMaximumIncrease(part);
        }

        if (part?.StatusEffects != null)
        {
            foreach (StatusEffect effect in part.StatusEffects)
            {
                if (effect is ICommonSpeedMaximumStatus speed)
                    total += speed.GetSpeedMaximumIncrease(part);
            }
        }

        return Mathf.Max(0, total);
    }
}


public sealed class StrengthStatus : NumericTimedStatus, ICommonRollShiftStatus
{
    public StrengthStatus(int stack = 1, int duration = 1)
        : base("힘", stack, duration) { }

    public int GetRollShift(BattleAction action) =>
        action != null ? Stack : 0;
}

public sealed class WeaknessStatus : NumericTimedStatus, ICommonRollShiftStatus
{
    public WeaknessStatus(int stack = 1, int duration = 1)
        : base("쇠약", stack, duration) { }

    public int GetRollShift(BattleAction action) =>
        action != null ? -Stack : 0;
}

public sealed class SturdyStatus : NumericTimedStatus
{
    public SturdyStatus(int stack = 1, int duration = 1)
        : base("견고", stack, duration) { }

    public override int GetStaggerDamageTakenFlatModifier(BattleAction action) =>
        -Stack;
}

public sealed class DisarmStatus : NumericTimedStatus
{
    public DisarmStatus(int stack = 1, int duration = 1)
        : base("무장해제", stack, duration) { }

    public override int GetStaggerDamageTakenFlatModifier(BattleAction action) =>
        Stack;
}

public sealed class FractureStatus : NumericTimedStatus, ICommonRollMaxReductionStatus
{
    public FractureStatus(int stack = 1, int duration = 1)
        : base("골절", stack, duration) { }

    public int GetMaxReduction(BattleAction action) => Stack;
}

public sealed class ProtectionStatus : NumericTimedStatus
{
    public ProtectionStatus(int stack = 1, int duration = 1)
        : base("보호", stack, duration) { }

    public override float ModifyDamageTaken(BattleAction action, float damage) =>
        Mathf.Max(0f, damage - Stack);
}

public sealed class RuptureStatus : NumericTimedStatus
{
    public RuptureStatus(int stack = 1, int duration = 1)
        : base("균열", stack, duration) { }

    public override float ModifyDamageTaken(BattleAction action, float damage) =>
        Mathf.Max(0f, damage + Stack);
}

/// <summary>
/// 0922 열기 N·T.
/// 개별 Entry는 gameplay 효과를 직접 실행하지 않는다.
/// TurnEnd에서 살아 있는 Heat/Stagnation N을 합산해 한 번 적용한다.
/// </summary>
public sealed class HeatStatus : NumericTimedStatus
{
    public HeatStatus(int stack = 1, int duration = 1)
        : base("열기", stack, duration) { }
}

/// <summary>
/// 0922 저장 모델에서는 Swift도 일반 NumericTimed N/T Entry다.
/// 즉시/예약 timing의 완전한 정리는 Phase 4에서 처리한다.
/// </summary>
public sealed class SwiftStatus : NumericTimedStatus, ICommonSpeedMaximumStatus
{
    public SwiftStatus(int stack = 1, int duration = 1)
        : base("신속", stack, duration) { }

    public int GetSpeedMaximumIncrease(BodyPart part) => Stack;
}

public enum RegenerationRecoveryChannel
{
    HitPoints = 0,
    Stagger = 1
}

/// <summary>
/// 0922 재생 N·T.
/// Channel은 구 asset/생성자 호환용 metadata로만 보존한다.
/// 실제 gameplay는 TurnEnd에서 모든 살아 있는 Regeneration N을 합산한 뒤
/// HP와 Stagger를 각각 한 번 회복한다.
/// </summary>
public sealed class RegenerationStatus : NumericTimedStatus
{
    public int HealAmount => Stack;
    public RegenerationRecoveryChannel Channel { get; private set; }

    public RegenerationStatus(
        int turns = 1,
        int healAmount = 1,
        RegenerationRecoveryChannel channel = RegenerationRecoveryChannel.HitPoints)
        : base("재생", healAmount, turns)
    {
        Channel = channel;
    }
}

public sealed class PainStatus : PresenceTimedStatus
{
    public PainStatus(int turns = 1)
        : base("고통", turns)
    {
    }

    public override int ModifyHealing(int amount)
    {
        if (amount <= 0)
            return 0;

        return Mathf.Max(1, amount / 2);
    }
}