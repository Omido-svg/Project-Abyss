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
    // 0915 C-27: 힘은 RED/BLUE 여부와 무관하게 모든 전투 굴림에 적용한다.
    public int GetRollShift(BattleAction action) =>
        action != null ? Stack : 0;
}

public sealed class WeaknessStatus : OneTurnCommonStatus, ICommonRollShiftStatus
{
    public WeaknessStatus(int stack = 1) : base("쇠약", stack, 4) { }
    // 0915 C-27: 쇠약은 RED/BLUE 여부와 무관하게 모든 전투 굴림에 적용한다.
    public int GetRollShift(BattleAction action) =>
        action != null ? -Stack : 0;
}

// 0915 C-27: 견고/무장해제의 실제 효과는 (미정). 타입은 구 에셋 호환용으로만 남기며
// 신규 Factory authoring에서는 생성하지 않는다.
public sealed class SturdyStatus : OneTurnCommonStatus, ICommonRollShiftStatus
{
    public SturdyStatus(int stack = 1) : base("견고", stack, 3) { }
    public int GetRollShift(BattleAction action) => 0;
}

public sealed class DisarmStatus : OneTurnCommonStatus, ICommonRollShiftStatus
{
    public DisarmStatus(int stack = 1) : base("무장해제", stack, 4) { }
    public int GetRollShift(BattleAction action) => 0;
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
/// 0915 C-48: 재생은 지속형이며 Stack/Duration은 남은 턴 수를 나타낸다.
/// 턴당 회복량과 회복 채널은 별도 데이터다.
/// </summary>
public enum RegenerationRecoveryChannel
{
    HitPoints = 0,
    Stagger = 1
}

/// <summary>
/// 0915 C-48: 지속시간, 턴당 회복량, 회복 채널을 서로 독립적으로 보관한다.
/// </summary>
public sealed class RegenerationStatus : StatusEffect
{
    public int HealAmount { get; private set; }
    public RegenerationRecoveryChannel Channel { get; private set; }

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnEnd;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.AddStacksAndRefreshDuration;

    public RegenerationStatus(
        int turns = 1,
        int healAmount = 1,
        RegenerationRecoveryChannel channel = RegenerationRecoveryChannel.HitPoints)
    {
        Name = "재생";
        Duration = Mathf.Max(1, turns);
        Stack = Duration;
        HealAmount = Mathf.Max(0, healAmount);
        Channel = channel;
    }

    public override bool CanMergeWith(StatusEffect other) =>
        other is RegenerationStatus regeneration &&
        regeneration.Channel == Channel;

    public override void Merge(StatusEffect other)
    {
        if (other is not RegenerationStatus regeneration ||
            regeneration.Channel != Channel)
            return;

        // 기존 지속형 재생의 "재부여 시 남은 턴 추가" 동작은 보존하되,
        // 회복량과 채널은 턴 수와 독립된 축으로 유지한다.
        Duration = Mathf.Clamp(
            Duration + Mathf.Max(1, regeneration.Duration),
            1,
            99);
        HealAmount = Mathf.Max(HealAmount, regeneration.HealAmount);
        Stack = Duration;
    }

    public override void OnTurnEnd(StatusEffectTickContext context)
    {
        if (Owner == null || HealAmount <= 0)
            return;

        if (Channel == RegenerationRecoveryChannel.Stagger)
            Owner.GetMechanic<StaggerGaugeMechanic>()?.Recover(HealAmount);
        else
            Owner.RestoreCurrentHP(HealAmount);

        // ProcessTurnEnd가 이 호출 뒤 Duration을 1 감소시킨다.
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