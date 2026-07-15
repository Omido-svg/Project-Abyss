using System.Collections.Generic;

/// <summary>
/// COMBAT 페이즈의 한 실행 단위.
/// Second가 있으면 합, 없으면 일방 공격이다.
/// </summary>
public sealed class ClashPair
{
    public ActionSlot First;
    public ActionSlot Second;

    public bool IsClash => Second != null;

    public ClashPair(
        ActionSlot first,
        ActionSlot second = null)
    {
        First = first;
        Second = second;
    }
}

/// <summary>
/// ClashMatchPolicy가 만든 합/일방 공격 계획.
/// UI 미리보기에서는 슬롯을 변경하지 않고 Pairs만 사용한다.
/// 실제 실행 큐를 만들 때만 ApplyTargetLinks를 호출한다.
/// </summary>
public sealed class ActionPairingResult
{
    private readonly List<ClashPair> pairs = new();

    public IReadOnlyList<ClashPair> Pairs => pairs;

    public int Count => pairs.Count;

    public void AddClash(
        ActionSlot first,
        ActionSlot second)
    {
        if (first == null || second == null)
            return;

        pairs.Add(
            new ClashPair(
                first,
                second));
    }

    public void AddOneSide(
        ActionSlot slot)
    {
        if (slot == null)
            return;

        pairs.Add(
            new ClashPair(slot));
    }

    public Queue<ClashPair> ToQueue()
    {
        return new Queue<ClashPair>(pairs);
    }

    /// <summary>
    /// 실제 전투 실행용 TargetSlot 링크를 한 번에 갱신한다.
    /// UI 미리보기에서는 호출하지 않는다.
    /// </summary>
    public void ApplyTargetLinks(
        IReadOnlyList<ActionSlot> allSlots)
    {
        ClearTargetLinks(allSlots);

        foreach (ClashPair pair in pairs)
        {
            if (pair == null ||
                !pair.IsClash ||
                pair.First == null ||
                pair.Second == null)
            {
                continue;
            }

            pair.First.TargetSlot =
                pair.Second;

            pair.Second.TargetSlot =
                pair.First;
        }
    }

    public static void ClearTargetLinks(
        IReadOnlyList<ActionSlot> slots)
    {
        if (slots == null)
            return;

        foreach (ActionSlot slot in slots)
        {
            if (slot != null)
                slot.TargetSlot = null;
        }
    }
}
