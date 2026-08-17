using UnityEngine;

/// <summary>
/// 계획/해결 중 예약된 상태이상을 다음 TurnStart에 실제 상태로 전환한다.
/// 새로 적용된 실제 1턴 상태는 해당 턴 TurnEnd까지 유지된다.
/// </summary>
public sealed class DeferredStatusEffect : StatusEffect
{
    private readonly StatusEffectId statusEffectId;
    private int pendingDuration;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnStart;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.AddStacksAndRefreshDuration;

    public DeferredStatusEffect(
        StatusEffectId statusEffectId,
        int stack,
        int duration)
    {
        this.statusEffectId = statusEffectId;
        Stack = Mathf.Max(1, stack);
        pendingDuration = duration;
        Duration = 1;
        Name = $"예약:{statusEffectId}";
    }

    public override void Merge(StatusEffect other)
    {
        if (other is not DeferredStatusEffect deferred ||
            deferred.statusEffectId != statusEffectId)
        {
            return;
        }

        Stack += Mathf.Max(1, deferred.Stack);
        pendingDuration = Mathf.Max(pendingDuration, deferred.pendingDuration);
        Duration = 1;
    }

    public override bool CanMergeWith(StatusEffect other)
    {
        return other is DeferredStatusEffect deferred &&
               deferred.statusEffectId == statusEffectId;
    }

    public override void OnTurnStart(StatusEffectTickContext context)
    {
        if (owner == null || owner.IsDead)
            return;

        StatusEffect actual = StatusEffectFactory.Create(
            statusEffectId,
            Stack,
            pendingDuration);

        if (actual == null)
            return;

        if (ownerPart != null)
            owner.AddPartStatus(ownerPart, actual, source);
        else
            owner.AddStatus(actual, source, sourcePart);
    }
}
