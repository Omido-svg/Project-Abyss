using System.Collections.Generic;

/// <summary>
/// 자동계획 계산과 실제 ActionManager mutation을 분리한다.
/// 적용 실패 시 기존 플레이어 계획을 복구하는 기존 동작을 보존한다.
/// </summary>
public sealed class PlayerAutoPlanApplicationService
{
    public int Apply(
        ActionManager actionManager,
        Character owner,
        IReadOnlyList<ActionSlot> plannedSlots)
    {
        if (actionManager == null ||
            owner == null ||
            plannedSlots == null)
        {
            return 0;
        }

        List<ActionSlot> oldSlots =
            SnapshotOwnerSlots(
                actionManager,
                owner);

        actionManager.RemoveSlotsByOwner(owner);

        int applied = 0;

        foreach (ActionSlot slot in plannedSlots)
        {
            if (slot == null)
                continue;

            if (actionManager.TryAddOrReplaceSlot(slot))
                applied++;
        }

        if (applied > 0)
            return applied;

        RestoreSlots(
            actionManager,
            oldSlots);

        return 0;
    }

    private static List<ActionSlot> SnapshotOwnerSlots(
        ActionManager actionManager,
        Character owner)
    {
        List<ActionSlot> result = new();

        if (actionManager?.Slots == null ||
            owner == null)
        {
            return result;
        }

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot?.Owner == owner)
                result.Add(slot);
        }

        return result;
    }

    private static void RestoreSlots(
        ActionManager actionManager,
        IReadOnlyList<ActionSlot> slots)
    {
        if (actionManager == null ||
            slots == null)
        {
            return;
        }

        foreach (ActionSlot slot in slots)
        {
            if (slot != null)
                actionManager.TryAddOrReplaceSlot(slot);
        }
    }
}
