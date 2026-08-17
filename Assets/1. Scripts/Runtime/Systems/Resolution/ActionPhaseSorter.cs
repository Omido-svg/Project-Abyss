using System.Collections.Generic;

/// <summary>
/// 해결 단계 슬롯 정렬기. PRETURN(위세) -> COMBAT 순서를 만든다.
/// FORESIGHT(도사림/환형)는 구형 데이터 분류값으로만 남고, 실제 실행은 계획 단계 즉시 처리한다.
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

        // 위세는 속도 바깥에서 처리한다. FORESIGHT가 들어오더라도 속도 정렬은 하지 않는다.
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
