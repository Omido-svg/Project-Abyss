using System.Collections.Generic;

/// <summary>
/// ActionSlot을 페이즈별로 분리하고,
/// 같은 페이즈 안에서는 속도 -> ActionIndex -> ActionId 순으로 정렬한다.
/// </summary>
public sealed class ActionPhaseSorter
{
    public List<ActionSlot> GetPhaseSlots(
        IReadOnlyList<ActionSlot> slots,
        ActionPhase phase)
    {
        List<ActionSlot> result = new();

        if (slots == null)
            return result;

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.Phase != phase)
                continue;

            result.Add(slot);
        }

        result.Sort(CompareForExecution);
        return result;
    }

    public int CompareForExecution(
        ActionSlot a,
        ActionSlot b)
    {
        if (ReferenceEquals(a, b))
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        // 높은 속도가 먼저 실행된다.
        int speedCompare =
            b.Speed.CompareTo(a.Speed);

        if (speedCompare != 0)
            return speedCompare;

        // 같은 부위의 추가 행동은 낮은 ActionIndex가 먼저다.
        int actionIndexCompare =
            a.ActionIndex.CompareTo(
                b.ActionIndex);

        if (actionIndexCompare != 0)
            return actionIndexCompare;

        // 완전 동률에서는 ActionId로 순서를 고정한다.
        return a.ActionId.CompareTo(
            b.ActionId);
    }
}
