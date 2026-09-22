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

/// <summary>
/// 구 0915~0917 소스/마이그레이션 문자열 호환용 alias.
/// 신규 공용 상태는 NumericTimedStatus를 직접 사용한다.
/// </summary>
public abstract class OneTurnCommonStatus : NumericTimedStatus
{
    protected OneTurnCommonStatus(
        string name,
        int stack,
        int legacyMaxStack)
        : base(name, stack, 1)
    {
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
/// 0922 저장 모델에서는 Heat도 일반 NumericTimed N/T Entry다.
/// 위세 적용 시점(OnApply -> TurnEnd) 변경은 Phase 3에서 처리한다.
/// </summary>
public sealed class HeatStatus : NumericTimedStatus
{
    public HeatStatus(int stack = 1, int duration = 1)
        : base("열기", stack, duration) { }

    public override void OnApply()
    {
        // Phase 2는 저장 모델만 변경한다. 기존 gameplay timing은 Phase 3까지 보존한다.
        if (Owner != null && Stack > 0)
            Owner.AddPrestige(Stack);
    }
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
/// 0922 저장 모델: 재생의 N은 Stack, T는 Duration으로 독립 보관한다.
/// HP+Stagger 동시 aggregate 효과는 Phase 3에서 정본화한다.
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

    public override void OnTurnEnd(StatusEffectTickContext context)
    {
        if (Owner == null || HealAmount <= 0)
            return;

        // Phase 2는 N/T 저장만 정본화한다.
        // 두 회복 채널을 하나의 aggregate N으로 처리하는 것은 Phase 3 소유다.
        if (Channel == RegenerationRecoveryChannel.Stagger)
            Owner.GetMechanic<StaggerGaugeMechanic>()?.Recover(HealAmount);
        else
            Owner.RestoreCurrentHP(HealAmount);
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
