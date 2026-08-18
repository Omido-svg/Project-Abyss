using System.Collections.Generic;

/// <summary>
/// 해결 단계 슬롯 정렬기. PRETURN(위세) -> FORESIGHT(도사림) -> COMBAT 순서를 만든다.
/// FORESIGHT는 START 전까지 ActionManager에 계획으로 유지되고 해결 단계에서 COMBAT보다 먼저 실행된다.
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
            if (slot == null || slot.Phase != phase)
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

        int phaseCompare =
            a.Phase.CompareTo(b.Phase);

        if (phaseCompare != 0)
            return phaseCompare;

        // 위세와 도사림은 속도 바깥에서 처리한다. COMBAT만 속도로 정렬한다.
        if (a.Phase == ActionPhase.COMBAT)
        {
            int speedCompare =
                b.Speed.CompareTo(a.Speed);

            if (speedCompare != 0)
                return speedCompare;
        }

        int actionIndexCompare =
            a.ActionIndex.CompareTo(
                b.ActionIndex);

        if (actionIndexCompare != 0)
            return actionIndexCompare;

        return a.ActionId.CompareTo(b.ActionId);
    }
}