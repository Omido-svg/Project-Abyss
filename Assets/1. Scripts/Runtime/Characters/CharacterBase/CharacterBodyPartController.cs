using System.Collections.Generic;
using UnityEngine;

public class CharacterBodyPartController
{
    private readonly Character owner;
    // const false를 사용하면 아래 로그 분기가 컴파일 타임에 도달 불가 코드가 된다.
    // readonly로 두어 기본 OFF 상태를 유지하면서 CS0162를 방지한다.
    private static readonly bool VerboseLog = false;

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

        if (!owner.CanBreakPart(
                part,
                sourceAction))
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

    /// <summary>
    /// O-05 카드 고유 예외. 파괴 권한 자체는 owner.CanBreakPart를 통과해야 하지만
    /// "이미 약화" 선행 조건만 면제한다.
    /// </summary>
    public bool TryBreakPartIgnoringWeakenPrerequisite(
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

        if (!owner.CanBreakPart(part, sourceAction))
            return false;

        BreakPartInternal(part, source, sourceAction);
        return true;
    }

    /// <summary>
    /// C-35 왕귀(짓누름) 전용. 죽음의 저항 해제 상태에서 0에 도달한 부위를
    /// Skill.CanBreakPart / 선행 약화 게이트 없이 파괴한다.
    /// </summary>
    public bool BreakPartIgnoringDeathResistance(
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

        BreakPartInternal(part, source, sourceAction);
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

        if (!owner.CanBreakPart(
                part,
                sourceAction))
        {
            WeakenPart(
                part,
                source,
                sourceAction);
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

        bool preserveSlots =
            owner.BattleContext?.Services?.EmotionRulebreakerService?
                .TryPreserveSlotsOnBreak(owner, part) == true;

        int removedSlotCount =
            preserveSlots
                ? 0
                : RemoveActionSlotsOfPart(part);

        // 최신 규칙: 파괴는 해당 부위에 연결된 행동 슬롯만 상실시킨다.
        // 파괴 순간 남은 부위 HP를 전신 HP에 다시 차감하지 않는다.
        part.Break();

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

    public void RestoreTemporaryWeakenedPart(
        BodyPart part,
        float hpBeforeTemporaryWeaken)
    {
        if (owner == null ||
            part == null ||
            (part.Owner != null && part.Owner != owner) ||
            !part.IsWeakened)
        {
            return;
        }

        // 임시 약화는 일반 부위 회복과 다르다. 부위에 걸린 다른 상태이상은 유지하고
        // 약화 때문에 생성된 PartDisabledStatus만 제거한다.
        part.RestoreTemporaryWeaken(hpBeforeTemporaryWeaken);
        owner.RemoveDisabledStatusForPart(part);

        owner.BattleEvent?.RaiseBodyPartRecovered(
            owner,
            part);
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

    /// <summary>
    /// C-35 부위 재생. 파괴 상태만 약화로 되돌리며 Whole HP를 회복하지 않는다.
    /// </summary>
    public bool RegenerateBrokenPartAsWeakened(BodyPart part)
    {
        if (owner == null ||
            part == null ||
            (part.Owner != null && part.Owner != owner) ||
            !part.IsBroken)
        {
            return false;
        }

        owner.RemoveAllPartStatuses(
            part,
            StatusEffectRemoveReason.PartRecovered);

        owner.RemoveBrokenStatusForPart(part);
        part.RegenerateAsWeakened();
        owner.ApplyDisabledStatusForPart(part);

        owner.BattleEvent?.RaiseBodyPartRecovered(
            owner,
            part);

        return true;
    }

    /// <summary>
    /// C-39 정비 노드: 부위 상태만 정상으로 복구한다.
    /// 일반 RecoverPart의 dual-heal은 수행하지 않는다.
    /// </summary>
    public bool RecoverPartAtMaintenance(BodyPart part)
    {
        if (owner == null ||
            part == null ||
            (part.Owner != null && part.Owner != owner) ||
            (!part.IsBroken && !part.IsWeakened))
        {
            return false;
        }

        owner.RemoveAllPartStatuses(
            part,
            StatusEffectRemoveReason.PartRecovered);

        part.Recover();
        owner.RemoveBrokenStatusForPart(part);

        owner.BattleEvent?.RaiseBodyPartRecovered(
            owner,
            part);

        return true;
    }

    private ActionManager GetActionManager()
    {
        return owner?.BattleContext?
            .Services?.ActionManager;
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
