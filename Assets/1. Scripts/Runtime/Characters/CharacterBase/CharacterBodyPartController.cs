using System.Collections.Generic;
using UnityEngine;

public class CharacterBodyPartController
{
    private readonly Character owner;
    private const bool VerboseLog = false;

    public CharacterBodyPartController(Character owner)
    {
        this.owner = owner;
    }

    public void WeakenPart(BodyPart part)
    {
        WeakenPart(
            part,
            owner?.ActiveDamageContext?.Attacker,
            owner?.ActiveDamageContext?.Action);
    }

    public void WeakenPart(
        BodyPart part,
        Character source,
        BattleAction sourceAction = null)
    {
        if (owner == null ||
            part == null ||
            (part.Owner != null && part.Owner != owner) ||
            part.IsBroken ||
            part.IsWeakened)
        {
            return;
        }

        BodyPartState stateBefore =
            part.State;

        part.Weaken();

        // 이벤트 구독자가 약화 디버프까지 적용된 완성 상태를 보게 한다.
        owner.ApplyDisabledStatusForPart(part);

        // DamageManager 처리 중이면 최종 스냅샷으로 한 번만 발행한다.
        if (!owner.IsDamageResolutionInProgress)
        {
            owner.BattleEvent?.RaiseBodyPartWeakened(
                BodyPartWeakenEventContext.External(
                    source,
                    owner,
                    part,
                    sourceAction,
                    stateBefore));
        }
    }

    public bool TryBreakWeakenedPart(BodyPart part)
    {
        return TryBreakWeakenedPart(
            part,
            owner?.ActiveDamageContext?.Attacker,
            owner?.ActiveDamageContext?.Action);
    }

    public bool TryBreakWeakenedPart(
        BodyPart part,
        Character source,
        BattleAction sourceAction = null)
    {
        if (owner == null ||
            part == null ||
            (part.Owner != null && part.Owner != owner) ||
            part.IsBroken)
        {
            return false;
        }

        if (!part.IsWeakened)
        {
            Debug.Log(
                $"{owner.Data.CharacterName} {part.Type} 부위는 " +
                "약화 상태가 아니므로 파괴할 수 없습니다.");

            return false;
        }

        BreakPartInternal(
            part,
            source,
            sourceAction);

        return true;
    }

    public void ForceBreakPart(BodyPart part)
    {
        ForceBreakPart(
            part,
            owner,
            null);
    }

    public void ForceBreakPart(
        BodyPart part,
        Character source,
        BattleAction sourceAction = null)
    {
        if (owner == null ||
            part == null ||
            (part.Owner != null && part.Owner != owner) ||
            part.IsBroken)
        {
            return;
        }

        BreakPartInternal(
            part,
            source,
            sourceAction);
    }

    private void BreakPartInternal(
        BodyPart part,
        Character source,
        BattleAction sourceAction)
    {
        BodyPartState stateBefore =
            part.State;

        LogVerboseWarning(
            $"[BREAK BEFORE] {owner.Data.CharacterName} {part.Type} " +
            $"State={part.State}, HP={part.PartHP}/{part.MaxPartHP}");

        int removedSlotCount =
            RemoveActionSlotsOfPart(part);

        int remainingPartHP =
            Mathf.Max(
                0,
                Mathf.RoundToInt(part.PartHP));

        if (remainingPartHP > 0)
            owner.ReduceCurrentHP(remainingPartHP);

        part.Break();

        owner.TransferPartStatusesToCharacter(part);
        owner.OnBodyPartBroken(part, null);

        DamageContext activeDamage =
            owner.ActiveDamageContext;

        BodyPartBreakEventContext breakContext =
            activeDamage != null
                ? new BodyPartBreakEventContext(
                    activeDamage.Attacker,
                    owner,
                    part,
                    activeDamage.Action,
                    activeDamage,
                    activeDamage.Result,
                    stateBefore,
                    part.State,
                    true)
                : BodyPartBreakEventContext.External(
                    source,
                    owner,
                    part,
                    sourceAction,
                    stateBefore);

        // 불사의 분노처럼 사망 판정에 영향을 주는 메커닉은
        // 공개 이벤트보다 먼저 내부 훅으로 처리한다.
        owner.NotifyBodyPartBreakBeforeDeath(
            breakContext);

        // 표준 피해 중에는 DamageEventDispatcher가 발행한다.
        // 강제 파괴처럼 DamageContext가 없는 경로만 여기서 즉시 발행한다.
        if (!owner.IsDamageResolutionInProgress)
        {
            owner.BattleEvent?.RaiseBodyPartDestroyed(
                breakContext);
        }

        LogVerboseWarning(
            $"[BREAK AFTER] {owner.Data.CharacterName} {part.Type} " +
            $"State={part.State}, HP={part.PartHP}/{part.MaxPartHP}, " +
            $"RemovedSlots={removedSlotCount}");

        owner.CheckDead(
            source,
            sourceAction);
    }

    public void RecoverPart(BodyPart part)
    {
        if (owner == null ||
            part == null ||
            (part.Owner != null && part.Owner != owner))
        {
            return;
        }

        if (!part.IsBroken && !part.IsWeakened)
            return;

        int hpBeforeRecovery =
            Mathf.Max(
                0,
                Mathf.RoundToInt(part.PartHP));

        int recoverAmount =
            Mathf.Max(
                0,
                Mathf.RoundToInt(part.MaxPartHP) -
                hpBeforeRecovery);

        owner.RemoveAllPartStatuses(
            part,
            StatusEffectRemoveReason.PartRecovered);

        part.Recover();

        owner.RemoveBrokenStatusForPart(part);

        // 기존 직접 피해를 보존하고 회복된 부위량만 더한다.
        owner.RestoreCurrentHP(recoverAmount);

        owner.BattleEvent?.RaiseBodyPartRecovered(
            owner,
            part);

        Debug.Log(
            $"{owner.Data.CharacterName} {part.Type} 부위 회복 / " +
            $"HP +{recoverAmount} ({owner.CurrentHP}/{owner.MaxCombatHP})");
    }

    private ActionManager GetActionManager()
    {
        return owner?.BattleContext?
            .battleManager?.ActionManager;
    }

    private int RemoveActionSlotsOfPart(BodyPart part)
    {
        if (owner == null || part == null)
            return 0;

        ActionManager actionManager =
            GetActionManager();

        if (actionManager == null)
        {
            LogVerboseWarning(
                "[REMOVE SLOT] ActionManager NULL");
            return 0;
        }

        int removeCount = 0;
        HashSet<long> visitedActionIds = new();

        while (true)
        {
            ActionSlot slot =
                actionManager.FindSlot(
                    owner,
                    part);

            if (slot == null)
                break;

            // RemoveSlot 구현 이상으로 같은 슬롯이 반복 반환될 경우
            // 전투 해석을 멈추는 무한 루프를 방지한다.
            if (!visitedActionIds.Add(slot.ActionId))
            {
                Debug.LogWarning(
                    $"[REMOVE SLOT ABORT] " +
                    $"{owner.Data?.CharacterName} / {part.Type} / " +
                    $"ActionId={slot.ActionId}");
                break;
            }

            actionManager.RemoveSlot(
                owner,
                part,
                slot.ActionIndex);

            removeCount++;
        }

        return removeCount;
    }

    private void LogVerbose(string message)
    {
        if (VerboseLog)
            Debug.Log(message);
    }

    private void LogVerboseWarning(string message)
    {
        if (VerboseLog)
            Debug.LogWarning(message);
    }
}

