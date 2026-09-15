using UnityEngine;

/// <summary>
/// 계획/해결 중 예약된 상태이상을 다음 TurnStart에 실제 상태로 전환한다.
/// 새로 적용된 실제 1턴 상태는 해당 턴 TurnEnd까지 유지된다.
/// </summary>
public sealed class DeferredStatusEffect : StatusEffect
{
    private readonly StatusEffectId statusEffectId;
    private int pendingDuration;
    private int regenerationHealAmount;
    private RegenerationRecoveryChannel regenerationChannel;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnStart;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.AddStacksAndRefreshDuration;

    /// <summary>
    /// Planning undo가 같은 예약 상태를 정확히 찾기 위한 읽기 전용 식별자.
    /// </summary>
    public StatusEffectId DeferredStatusId =>
        statusEffectId;

    /// <summary>
    /// 실제 상태로 전환될 때 사용할 예약 Duration.
    /// </summary>
    public int PendingDuration =>
        pendingDuration;

    /// <summary>
    /// Planning immediate effect 취소 전용 복원 API.
    /// 이미 존재하던 DeferredStatusEffect에 새 stack이 merge된 경우
    /// 취소 직전의 stack/duration으로 되돌린다.
    ///
    /// regenerationHealAmount / channel은 같은 instance의 기존 값이므로
    /// 이 API에서는 건드리지 않는다.
    /// </summary>
    public void RestorePendingStateForPlanning(
        int stack,
        int duration)
    {
        Stack =
            Mathf.Max(
                0,
                stack);

        pendingDuration =
            Mathf.Max(
                0,
                duration);

        Duration = 1;
    }

    /// <summary>
    /// Phase B C-48 계약:
    /// Regeneration은 turns / healAmount / recovery channel을 별도로 보존한다.
    /// AddBodyPartStatusEffect의 NextTurn 경로가 이 5인자 생성자를 사용한다.
    /// </summary>
    public DeferredStatusEffect(
        StatusEffectId statusEffectId,
        int stack,
        int duration,
        int regenerationHealAmount = 0,
        RegenerationRecoveryChannel regenerationChannel =
            RegenerationRecoveryChannel.HitPoints)
    {
        this.statusEffectId = statusEffectId;
        Stack = Mathf.Max(1, stack);
        pendingDuration = duration;
        this.regenerationHealAmount =
            Mathf.Max(
                0,
                regenerationHealAmount);
        this.regenerationChannel =
            regenerationChannel;
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

        Stack +=
            Mathf.Max(
                1,
                deferred.Stack);

        pendingDuration =
            Mathf.Max(
                pendingDuration,
                deferred.pendingDuration);

        regenerationHealAmount =
            Mathf.Max(
                regenerationHealAmount,
                deferred.regenerationHealAmount);

        regenerationChannel =
            deferred.regenerationChannel;

        Duration = 1;
    }

    public override bool CanMergeWith(StatusEffect other)
    {
        return other is DeferredStatusEffect deferred &&
               deferred.statusEffectId == statusEffectId &&
               (statusEffectId != StatusEffectId.Regeneration ||
                deferred.regenerationChannel == regenerationChannel);
    }

    public override void OnTurnStart(
        StatusEffectTickContext context)
    {
        if (owner == null || owner.IsDead)
            return;

        StatusEffect actual =
            StatusEffectFactory.Create(
                statusEffectId,
                Stack,
                pendingDuration,
                regenerationHealAmount,
                regenerationChannel);

        if (actual == null)
            return;

        if (ownerPart != null)
            owner.AddPartStatus(
                ownerPart,
                actual,
                source);
        else
            owner.AddStatus(
                actual,
                source,
                sourcePart);
    }
}
