using UnityEngine;

/// <summary>
/// 0922 다음 턴 예약 상태.
/// Numeric 예약은 Entry identity를 유지하고, Presence 예약만 max-duration refresh한다.
/// 실제 상태 materialization은 이 예약을 소유한 CharacterStatusController를 통해 수행한다.
/// </summary>
public sealed class DeferredStatusEffect : StatusEffect
{
    private readonly StatusEffectId statusEffectId;
    private int pendingDuration;
    private int regenerationHealAmount;
    private RegenerationRecoveryChannel regenerationChannel;
    private CharacterStatusController materializationController;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnStart;

    public bool IsNumericReservation =>
        IsNumericStatusId(statusEffectId);

    public bool IsPresenceReservation =>
        IsPresenceStatusId(statusEffectId);

    public override StatusEffectStackPolicy StackPolicy =>
        IsNumericReservation
            ? StatusEffectStackPolicy.Ignore
            : IsPresenceReservation
                ? StatusEffectStackPolicy.RefreshDuration
                : StatusEffectStackPolicy.AddStacksAndRefreshDuration;

    /// <summary>
    /// Planning undo 및 진단이 예약 종류를 식별하기 위한 읽기 전용 ID.
    /// </summary>
    public StatusEffectId DeferredStatusId =>
        statusEffectId;

    /// <summary>
    /// 실제 상태로 전환될 때 사용할 예약 Duration.
    /// -1은 ∞를 의미한다.
    /// </summary>
    public int PendingDuration =>
        pendingDuration;

    internal void BindMaterializationController(
        CharacterStatusController controller)
    {
        materializationController =
            controller;
    }

    /// <summary>
    /// Planning 취소 시 merge된 Presence/Bespoke 예약을 정확한 이전 상태로 되돌린다.
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
            duration < 0
                ? StatusEffect.InfiniteDuration
                : Mathf.Max(
                    0,
                    duration);

        Duration = 1;
    }

    internal override void RestoreStateForPlanning(
        int stack,
        int duration)
    {
        RestorePendingStateForPlanning(
            stack,
            duration);
    }

    public DeferredStatusEffect(
        StatusEffectId statusEffectId,
        int stack,
        int duration,
        int regenerationHealAmount = 0,
        RegenerationRecoveryChannel regenerationChannel =
            RegenerationRecoveryChannel.HitPoints)
    {
        this.statusEffectId =
            statusEffectId;

        Stack =
            Mathf.Max(
                1,
                stack);

        pendingDuration =
            duration < 0
                ? StatusEffect.InfiniteDuration
                : Mathf.Max(
                    1,
                    duration);

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

        // 0922 Numeric 예약은 독립 Entry이므로 merge 대상이 아니다.
        if (IsNumericReservation)
            return;

        if (IsPresenceReservation)
        {
            Stack = 1;

            if (pendingDuration < 0 ||
                deferred.pendingDuration < 0)
            {
                pendingDuration =
                    StatusEffect.InfiniteDuration;
            }
            else
            {
                pendingDuration =
                    Mathf.Max(
                        pendingDuration,
                        deferred.pendingDuration);
            }

            Duration = 1;
            return;
        }

        // Bespoke/legacy 예약은 기존 누적 의미를 유지한다.
        Stack +=
            Mathf.Max(
                1,
                deferred.Stack);

        if (pendingDuration < 0 ||
            deferred.pendingDuration < 0)
        {
            pendingDuration =
                StatusEffect.InfiniteDuration;
        }
        else
        {
            pendingDuration =
                Mathf.Max(
                    pendingDuration,
                    deferred.pendingDuration);
        }

        regenerationHealAmount =
            Mathf.Max(
                regenerationHealAmount,
                deferred.regenerationHealAmount);

        regenerationChannel =
            deferred.regenerationChannel;

        Duration = 1;
    }

    public override bool CanMergeWith(
        StatusEffect other)
    {
        if (other is not DeferredStatusEffect deferred ||
            deferred.statusEffectId != statusEffectId)
        {
            return false;
        }

        if (IsNumericReservation)
            return false;

        if (IsPresenceReservation)
            return true;

        return statusEffectId != StatusEffectId.Regeneration ||
               deferred.regenerationChannel == regenerationChannel;
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

        // CharacterStatusController가 예약을 소유하는 경우 반드시 같은 저장소에 materialize한다.
        // isolated verification controller와 실제 runtime controller 모두 동일한 계약을 사용한다.
        if (materializationController != null)
        {
            if (ownerPart != null)
            {
                materializationController.AddPartStatus(
                    ownerPart,
                    actual,
                    source);
            }
            else
            {
                materializationController.AddStatus(
                    actual,
                    source,
                    sourcePart);
            }

            return;
        }

        // 구 직접 호출 경로 호환 fallback.
        if (ownerPart != null)
        {
            owner.AddPartStatus(
                ownerPart,
                actual,
                source);
        }
        else
        {
            owner.AddStatus(
                actual,
                source,
                sourcePart);
        }
    }

    private static bool IsNumericStatusId(
        StatusEffectId id)
    {
        return id == StatusEffectId.Strength ||
               id == StatusEffectId.Weakness ||
               id == StatusEffectId.Sturdy ||
               id == StatusEffectId.Disarm ||
               id == StatusEffectId.Fracture ||
               id == StatusEffectId.Protection ||
               id == StatusEffectId.Rupture ||
               id == StatusEffectId.Heat ||
               id == StatusEffectId.Regeneration ||
               id == StatusEffectId.Swift;
    }

    private static bool IsPresenceStatusId(
        StatusEffectId id)
    {
        return id == StatusEffectId.Pain;
    }
}
