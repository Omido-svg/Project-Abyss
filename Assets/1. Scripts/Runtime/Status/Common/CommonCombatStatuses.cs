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

public abstract class OneTurnCommonStatus : StatusEffect
{
    private readonly int maxStack;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnEnd;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.AddStacksAndRefreshDuration;

    protected OneTurnCommonStatus(string name, int stack, int maxStack)
    {
        Name = name;
        this.maxStack = Mathf.Max(1, maxStack);
        Stack = Mathf.Clamp(stack, 0, this.maxStack);
        Duration = 1;
    }

    public override void Merge(StatusEffect other)
    {
        if (other == null)
            return;

        Stack = Mathf.Clamp(Stack + Mathf.Max(0, other.Stack), 0, maxStack);
        Duration = 1;
    }
}

public sealed class StrengthStatus : OneTurnCommonStatus, ICommonRollShiftStatus
{
    public StrengthStatus(int stack = 1) : base("힘", stack, 4) { }
    public int GetRollShift(BattleAction action) =>
        action?.CurrentRollType == CombatRollType.Attack ? Stack : 0;
}

public sealed class WeaknessStatus : OneTurnCommonStatus, ICommonRollShiftStatus
{
    public WeaknessStatus(int stack = 1) : base("쇠약", stack, 4) { }
    public int GetRollShift(BattleAction action) =>
        action?.CurrentRollType == CombatRollType.Attack ? -Stack : 0;
}

public sealed class SturdyStatus : OneTurnCommonStatus, ICommonRollShiftStatus
{
    public SturdyStatus(int stack = 1) : base("견고", stack, 3) { }
    public int GetRollShift(BattleAction action) =>
        action?.CurrentRollType == CombatRollType.Stagger ? Stack : 0;
}

public sealed class DisarmStatus : OneTurnCommonStatus, ICommonRollShiftStatus
{
    public DisarmStatus(int stack = 1) : base("무장해제", stack, 4) { }
    public int GetRollShift(BattleAction action) =>
        action?.CurrentRollType == CombatRollType.Stagger ? -Stack : 0;
}

public sealed class FractureStatus : OneTurnCommonStatus, ICommonRollMaxReductionStatus
{
    public FractureStatus(int stack = 1) : base("골절", stack, 4) { }
    public int GetMaxReduction(BattleAction action) => Stack;
}

public sealed class ProtectionStatus : OneTurnCommonStatus
{
    public ProtectionStatus(int stack = 1) : base("보호", stack, 4) { }
    public override float ModifyDamageTaken(BattleAction action, float damage) =>
        Mathf.Max(0f, damage - Stack);
}

public sealed class RuptureStatus : OneTurnCommonStatus
{
    public RuptureStatus(int stack = 1) : base("균열", stack, 4) { }
    public override float ModifyDamageTaken(BattleAction action, float damage) =>
        Mathf.Max(0f, damage + Stack);
}

/// <summary>
/// P0 D-08 확정 규칙: 열기는 1턴 크기형이며 적용되는 순간 위세 +N을 1회 지급한다.
/// 다음 턴 예약은 DeferredStatusEffect가 TurnStart에 실제 HeatStatus를 생성하여 지급한다.
/// </summary>
public sealed class HeatStatus : OneTurnCommonStatus
{
    public HeatStatus(int stack = 1) : base("열기", stack, 99) { }

    public override void OnApply()
    {
        if (Owner != null && Stack > 0)
            Owner.AddPrestige(Stack);
    }

    public override void Merge(StatusEffect other)
    {
        int before = Stack;
        base.Merge(other);
        int gained = Mathf.Max(0, Stack - before);
        if (Owner != null && gained > 0)
            Owner.AddPrestige(gained);
    }
}

/// <summary>
/// P0 D-08 확정 규칙: 신속은 다음 턴 전용 크기형 상태이며 속도 굴림의 최댓값 +N.
/// 카드에서 즉시 부여하지 말고 DeferredStatusEffect(StatusEffectId.Swift, ...)로 예약한다.
/// </summary>
public sealed class SwiftStatus : OneTurnCommonStatus, ICommonSpeedMaximumStatus
{
    public SwiftStatus(int stack = 1) : base("신속", stack, 99) { }
    public int GetSpeedMaximumIncrease(BodyPart part) => Stack;
}

/// <summary>
/// P0 D-08: 재생은 지속형이다. Stack과 Duration은 남은 턴 수를 같은 값으로 유지한다.
/// 정본이 공용 재생의 기본 회복량을 별도로 확정하지 않았기 때문에, 기존 런타임 호환을 위해
/// 최초 부여 N을 턴당 회복량으로도 보존한다. 추후 데이터에 회복량 필드가 생기면 분리 가능하다.
/// </summary>
public sealed class RegenerationStatus : StatusEffect
{
    private readonly int healPerTurn;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnEnd;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.AddStacksAndRefreshDuration;

    public RegenerationStatus(int turns = 1)
    {
        Name = "재생";
        int safeTurns = Mathf.Max(1, turns);
        Stack = safeTurns;
        Duration = safeTurns;
        healPerTurn = safeTurns;
    }

    public override void Merge(StatusEffect other)
    {
        if (other is not RegenerationStatus regeneration)
            return;

        int addedTurns = Mathf.Max(1, regeneration.Stack);
        Duration = Mathf.Clamp(Duration + addedTurns, 1, 99);
        Stack = Duration;
    }

    public override void OnTurnEnd(StatusEffectTickContext context)
    {
        Owner?.RestoreCurrentHP(healPerTurn);

        // ProcessTurnEnd가 이 호출 뒤 Duration을 1 감소시키므로 미리 같은 값으로 맞춘다.
        Stack = Mathf.Max(0, Duration - 1);
    }
}

/// <summary>
/// P0 D-08: 고통은 지속형이며 비누적이다. 재부여하면 더하지 않고 더 긴 지속시간으로 갱신한다.
/// 활성 중 모든 양수 회복량은 절반(버림), 최소 1을 보장한다.
/// </summary>
public sealed class PainStatus : StatusEffect
{
    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnEnd;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.RefreshDuration;

    public PainStatus(int turns = 1)
    {
        Name = "고통";
        int safeTurns = Mathf.Max(1, turns);
        Stack = safeTurns;
        Duration = safeTurns;
    }

    public override void Merge(StatusEffect other)
    {
        if (other is not PainStatus pain)
            return;

        Duration = Mathf.Max(Duration, Mathf.Max(1, pain.Duration));
        Stack = Duration;
    }

    public override void OnTurnEnd(StatusEffectTickContext context)
    {
        Stack = Mathf.Max(0, Duration - 1);
    }

    public override int ModifyHealing(int amount)
    {
        if (amount <= 0)
            return 0;

        return Mathf.Max(1, amount / 2);
    }
}
