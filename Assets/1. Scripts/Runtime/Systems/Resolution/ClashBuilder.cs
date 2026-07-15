using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 페이즈 분리와 합 매칭 결과를 조립하는 진입점.
/// 실제 규칙은 ActionPhaseSorter와 ClashMatchPolicy에 위임한다.
/// </summary>
public sealed class ClashBuilder
{
    private readonly ActionPhaseSorter phaseSorter;
    private readonly ClashMatchPolicy matchPolicy;

    public ClashBuilder()
        : this(
            new ActionPhaseSorter(),
            null)
    {
    }

    public ClashBuilder(
        ActionPhaseSorter phaseSorter,
        ClashMatchPolicy matchPolicy)
    {
        this.phaseSorter =
            phaseSorter ??
            new ActionPhaseSorter();

        this.matchPolicy =
            matchPolicy ??
            new ClashMatchPolicy(
                this.phaseSorter);
    }

    public ActionExecutionQueue BuildQueue(
        IReadOnlyList<ActionSlot> slots)
    {
        ActionExecutionQueue queue =
            new();

        List<ActionSlot> prestigeSlots =
            phaseSorter.GetPhaseSlots(
                slots,
                ActionPhase.PRETURN);

        List<ActionSlot> preparationSlots =
            phaseSorter.GetPhaseSlots(
                slots,
                ActionPhase.FORESIGHT);

        List<ActionSlot> combatSlots =
            phaseSorter.GetPhaseSlots(
                slots,
                ActionPhase.COMBAT);

        EnqueueSlots(
            queue.PrestigeQueue,
            prestigeSlots);

        EnqueueSlots(
            queue.PreparationQueue,
            preparationSlots);

        ActionPairingResult pairingResult =
            BuildPairingResult(
                combatSlots);

        // 실제 실행 계획을 만들 때만 슬롯의 TargetSlot 링크를 갱신한다.
        pairingResult.ApplyTargetLinks(slots);

        queue.ClashQueue =
            pairingResult.ToQueue();

        PrintSlots(slots);

        return queue;
    }

    /// <summary>
    /// UI도 실제 전투와 동일한 합 규칙을 사용한다.
    /// 이 메서드는 ActionSlot.TargetSlot을 변경하지 않는다.
    /// 따라서 LateUpdate에서 반복 호출해도 실제 실행 계획을 오염시키지 않는다.
    /// </summary>
    public IReadOnlyList<ClashPair> BuildClashPreview(
        IReadOnlyList<ActionSlot> slots)
    {
        List<ActionSlot> combatSlots =
            phaseSorter.GetPhaseSlots(
                slots,
                ActionPhase.COMBAT);

        ActionPairingResult pairingResult =
            BuildPairingResult(
                combatSlots);

        return new List<ClashPair>(
            pairingResult.Pairs);
    }

    public ActionPairingResult BuildPairingResult(
        IReadOnlyList<ActionSlot> combatSlots)
    {
        ActionPairingResult result =
            new();

        if (combatSlots == null)
            return result;

        // 전달받은 목록이 이미 정렬되어 있어도,
        // 외부 호출을 위해 복사 후 실행 순서를 다시 고정한다.
        List<ActionSlot> orderedSlots =
            new();

        foreach (ActionSlot slot in combatSlots)
        {
            if (slot != null)
                orderedSlots.Add(slot);
        }

        orderedSlots.Sort(
            phaseSorter.CompareForExecution);

        HashSet<ActionSlot> usedSlots =
            new();

        foreach (ActionSlot slot in orderedSlots)
        {
            if (slot == null ||
                usedSlots.Contains(slot))
            {
                continue;
            }

            if (!matchPolicy.CanEnterClash(slot))
            {
                result.AddOneSide(slot);
                usedSlots.Add(slot);
                continue;
            }

            ActionSlot targetSlot =
                matchPolicy.FindBestMatch(
                    slot,
                    orderedSlots,
                    usedSlots);

            if (targetSlot != null)
            {
                result.AddClash(
                    slot,
                    targetSlot);

                usedSlots.Add(slot);
                usedSlots.Add(targetSlot);
                continue;
            }

            // 합 상대를 찾지 못한 공격은 속도순 일방 공격으로 남는다.
            result.AddOneSide(slot);
            usedSlots.Add(slot);
        }

        return result;
    }

    private void EnqueueSlots(
        Queue<ActionSlot> queue,
        IReadOnlyList<ActionSlot> slots)
    {
        if (queue == null ||
            slots == null)
        {
            return;
        }

        foreach (ActionSlot slot in slots)
        {
            if (slot != null)
                queue.Enqueue(slot);
        }
    }

    private void PrintSlots(
        IReadOnlyList<ActionSlot> slots)
    {
        if (!BattleDebugLog.ShowClashBuild)
            return;

        Debug.Log(
            "===== SLOT CONNECTION =====");

        if (slots == null)
        {
            Debug.Log("Slots : NULL");
            return;
        }

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
            {
                Debug.Log("NULL SLOT");
                continue;
            }

            string log =
                $"Id={slot.ActionId}, " +
                $"Index={slot.ActionIndex}, " +
                $"{GetCharacterName(slot.Owner)} " +
                $"({GetPartName(slot.Part)}) -> " +
                $"{GetCharacterName(slot.TargetCharacter)} " +
                $"({GetPartName(slot.TargetPart)}) / " +
                $"Skill={GetSkillName(slot.Skill)} / " +
                $"Speed={slot.Speed} / " +
                $"Phase={slot.Phase} / " +
                $"TargetSlot={GetTargetSlotText(slot.TargetSlot)} / " +
                $"Clash={(IsConfirmedClash(slot) ? "YES" : "NO")}";

            Debug.Log(log);
        }
    }

    private bool IsConfirmedClash(
        ActionSlot slot)
    {
        if (slot?.TargetSlot == null)
            return false;

        return
            slot.TargetSlot.TargetSlot == slot &&
            matchPolicy.CanEnterClash(slot) &&
            matchPolicy.CanEnterClash(
                slot.TargetSlot);
    }

    private string GetCharacterName(
        Character character)
    {
        if (character == null)
            return "NULL";

        return character.Data == null
            ? character.name
            : character.Data.CharacterName;
    }

    private string GetPartName(
        BodyPart part)
    {
        return part == null
            ? "NONE"
            : part.Type.ToString();
    }

    private string GetSkillName(
        Skill skill)
    {
        return skill == null
            ? "NULL"
            : skill.SkillName;
    }

    private string GetTargetSlotText(
        ActionSlot targetSlot)
    {
        if (targetSlot == null)
            return "NULL";

        return
            $"{GetCharacterName(targetSlot.Owner)} " +
            $"({GetPartName(targetSlot.Part)}) " +
            $"[Index={targetSlot.ActionIndex}, Id={targetSlot.ActionId}]";
    }
}
