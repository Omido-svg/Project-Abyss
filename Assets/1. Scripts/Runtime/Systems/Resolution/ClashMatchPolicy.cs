using System.Collections.Generic;

/// <summary>
/// COMBAT 행동의 합 성립 규칙.
///
/// P0 D-05 확정 규칙:
/// - 내 공격 슬롯이 상대의 살아 있는 공격 슬롯(TargetSlot)을 조준하면 합이 성립한다.
/// - 상대 슬롯이 누구를 조준했는지, 두 슬롯의 속도가 얼마인지는 합 성립 여부에 관여하지 않는다.
/// - 속도는 실행 순서와 합 판정 보정에만 사용한다.
/// - 공격하지 않는 슬롯/파괴된 슬롯/정확한 TargetSlot이 없는 공격은 합이 아니라 일방타격이다.
/// - 한 상대 슬롯을 여러 슬롯이 조준하면 가장 나중에 계획된(ActionId가 큰) 유효 슬롯이 합을 가져간다.
///
/// 기존 Focused Encounter의 속도 가로채기 API는 호출부 호환을 위해 남기지만,
/// 새 규칙에서는 합 성립 조건으로 속도를 사용하지 않는다.
/// </summary>
public sealed class ClashMatchPolicy
{
    private readonly ActionPhaseSorter phaseSorter;

    public ClashMatchPolicy(
        ActionPhaseSorter phaseSorter)
    {
        this.phaseSorter =
            phaseSorter ??
            new ActionPhaseSorter();
    }

    public bool CanEnterClash(
        ActionSlot slot)
    {
        if (slot == null ||
            slot.Owner == null ||
            slot.TargetCharacter == null ||
            slot.Skill == null)
        {
            return false;
        }

        if (slot.Phase != ActionPhase.COMBAT)
            return false;

        // 새 정본에서 "공격 슬롯"은 합에 들어갈 수 있는 COMBAT 스킬이다.
        if (!slot.Skill.CanClash)
            return false;

        if (slot.Owner.IsDead ||
            slot.TargetCharacter.IsDead)
        {
            return false;
        }

        if (slot.Part != null &&
            slot.Part.IsBroken)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 레거시 호출부 호환 API.
    /// 새 정본에서는 속도 가로채기가 합 성립 조건이 아니므로,
    /// incoming이 challenger 쪽을 공격하고 있는지만 확인한다.
    /// </summary>
    public static bool CanRedirectOrOppose(
        Character challengerOwner,
        BodyPart challengerPart,
        int challengerSpeed,
        ActionSlot incoming,
        out bool isRedirect)
    {
        isRedirect = false;

        if (challengerOwner == null ||
            incoming == null ||
            incoming.Owner == null ||
            incoming.TargetCharacter == null ||
            incoming.Skill == null ||
            incoming.Phase != ActionPhase.COMBAT ||
            !incoming.Skill.CanClash ||
            incoming.Owner.IsDead ||
            incoming.TargetCharacter.IsDead)
        {
            return false;
        }

        if (incoming.Part != null &&
            incoming.Part.IsBroken)
        {
            return false;
        }

        return incoming.TargetCharacter == challengerOwner;
    }

    /// <summary>
    /// challenger가 incoming을 정확한 TargetSlot으로 조준했는지만 본다.
    /// 상대의 조준 방향과 속도는 합 성립과 무관하다.
    /// </summary>
    public bool CanChallenge(
        ActionSlot challenger,
        ActionSlot incoming)
    {
        if (!CanEnterClash(challenger) ||
            !CanEnterClash(incoming) ||
            challenger == incoming)
        {
            return false;
        }

        if (challenger.TargetCharacter != incoming.Owner)
            return false;

        return IsSameSlot(
            challenger.TargetSlot,
            incoming);
    }

    public ActionSlot FindBestMatch(
        ActionSlot source,
        IReadOnlyList<ActionSlot> combatSlots,
        HashSet<ActionSlot> usedSlots)
    {
        if (!CanEnterClash(source) ||
            combatSlots == null)
        {
            return null;
        }

        // source가 직접 상대 공격 슬롯을 조준했다면 그 의도를 우선한다.
        // 다만 그 상대 슬롯을 더 나중에 계획된 다른 공격이 조준했다면
        // 정본의 "마지막으로 조준한 슬롯이 합을 가져간다" 규칙에 따라 양보한다.
        if (source.TargetSlot != null)
        {
            ActionSlot target = source.TargetSlot;

            if (target == source ||
                (usedSlots != null && usedSlots.Contains(target)) ||
                !CanChallenge(source, target) ||
                !IsLatestChallenger(source, target, combatSlots, usedSlots))
            {
                return null;
            }

            return target;
        }

        // source 자체를 조준한 슬롯들 가운데 가장 마지막 유효 조준만 합을 가져간다.
        ActionSlot latest = null;
        foreach (ActionSlot candidate in combatSlots)
        {
            if (candidate == null ||
                candidate == source ||
                (usedSlots != null && usedSlots.Contains(candidate)) ||
                !CanChallenge(candidate, source))
            {
                continue;
            }

            if (latest == null ||
                ComparePlanningOrder(candidate, latest) > 0)
            {
                latest = candidate;
            }
        }

        return latest;
    }

    private bool IsLatestChallenger(
        ActionSlot challenger,
        ActionSlot target,
        IReadOnlyList<ActionSlot> combatSlots,
        HashSet<ActionSlot> usedSlots)
    {
        if (challenger == null ||
            target == null ||
            combatSlots == null)
        {
            return false;
        }

        foreach (ActionSlot candidate in combatSlots)
        {
            if (candidate == null ||
                candidate == challenger ||
                candidate == target ||
                (usedSlots != null && usedSlots.Contains(candidate)) ||
                !CanChallenge(candidate, target))
            {
                continue;
            }

            if (ComparePlanningOrder(candidate, challenger) > 0)
                return false;
        }

        return true;
    }

    private static int ComparePlanningOrder(
        ActionSlot a,
        ActionSlot b)
    {
        if (a == null && b == null)
            return 0;
        if (a == null)
            return -1;
        if (b == null)
            return 1;

        // ActionId는 ActionManager가 예약 순서대로 증가시키므로
        // "마지막으로 조준한 슬롯"의 1차 기준으로 사용한다.
        int actionIdCompare =
            a.ActionId.CompareTo(b.ActionId);
        if (actionIdCompare != 0)
            return actionIdCompare;

        // 검증/프리뷰처럼 ActionId가 없는 경우의 결정론적 fallback.
        int indexCompare =
            a.ActionIndex.CompareTo(b.ActionIndex);
        if (indexCompare != 0)
            return indexCompare;

        // 마지막 fallback은 실행 정렬 결과가 안정적으로 유지되도록 속도를 쓴다.
        // 이것은 "합 성립 조건"이 아니라 동일 식별자 충돌의 결정론적 tie-breaker다.
        return a.Speed.CompareTo(b.Speed);
    }

    private static bool IsSameSlot(
        ActionSlot a,
        ActionSlot b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        return a.ActionId > 0 &&
               b.ActionId > 0 &&
               a.ActionId == b.ActionId &&
               a.Owner == b.Owner;
    }
}
