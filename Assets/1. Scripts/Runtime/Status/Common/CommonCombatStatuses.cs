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

/// <summary>최신 문서 우선 규칙에 따라 재생도 1턴형으로 변경한다.</summary>
public sealed class RegenerationStatus : OneTurnCommonStatus
{
    public RegenerationStatus(int stack = 1) : base("재생", stack, 99) { }
    public override void OnTurnEnd(StatusEffectTickContext context)
    {
        Owner?.RestoreCurrentHP(Stack);
    }
}

/// <summary>최신 문서 우선 규칙에 따라 고통도 1턴형. 활성 중 회복량을 절반으로 만든다.</summary>
public sealed class PainStatus : OneTurnCommonStatus
{
    public PainStatus(int stack = 1) : base("고통", Mathf.Max(1, stack), 1) { }
    public override int ModifyHealing(int amount) => Mathf.Max(0, amount / 2);
}