using System.Collections.Generic;

/// <summary>
/// COMBAT 행동끼리 합이 가능한지와,
/// 특정 공격이 어느 상대 행동과 합할지를 결정한다.
///
/// 최신 규칙:
/// - 상호 조준 여부를 요구하지 않는다.
/// - 공격자의 속도가 더 빨라야 한다는 조건도 요구하지 않는다.
/// - 공격한 상대 부위가 보유한 미사용 COMBAT 행동이 있고,
///   양쪽 스킬이 CanClash이면 합을 만든다.
/// - 속도는 합 생성 조건이 아니라 합 수치 보정에만 사용한다.
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

        if (slot.Phase !=
            ActionPhase.COMBAT)
        {
            return false;
        }

        if (!slot.Skill.CanClash)
            return false;

        if (slot.Owner.IsDead ||
            slot.TargetCharacter.IsDead)
        {
            return false;
        }

        // 독립 행동/일반몹 행동을 위해 Part == null 자체는 허용한다.
        if (slot.Part != null &&
            slot.Part.IsBroken)
        {
            return false;
        }

        // TargetPart가 Broken이어도 공격 및 합 후보가 될 수 있다.
        return true;
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

        ActionSlot best = null;

        foreach (ActionSlot candidate in combatSlots)
        {
            if (candidate == null ||
                candidate == source)
            {
                continue;
            }

            if (usedSlots != null &&
                usedSlots.Contains(candidate))
            {
                continue;
            }

            // 공격 대상 캐릭터가 가진 행동만 후보가 된다.
            if (candidate.Owner !=
                source.TargetCharacter)
            {
                continue;
            }

            // 공격한 부위의 행동 슬롯과 연결한다.
            if (!MatchesTargetSource(
                    source,
                    candidate))
            {
                continue;
            }

            if (!CanEnterClash(candidate))
                continue;

            if (best == null ||
                phaseSorter.CompareForExecution(
                    candidate,
                    best) < 0)
            {
                best = candidate;
            }
        }

        return best;
    }

    private bool MatchesTargetSource(
        ActionSlot attacker,
        ActionSlot targetAction)
    {
        if (attacker == null ||
            targetAction == null)
        {
            return false;
        }

        // 부위형 대상:
        // 공격한 TargetPart를 행동 원천으로 사용하는 슬롯과 합한다.
        if (attacker.TargetPart != null)
        {
            return IsSamePart(
                targetAction.Part,
                attacker.TargetPart);
        }

        // 단일 HP 대상:
        // 부위 없는 행동 원천끼리 매칭한다.
        return targetAction.Part == null;
    }

    private bool IsSamePart(
        BodyPart a,
        BodyPart b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        return a.Type == b.Type;
    }
}
