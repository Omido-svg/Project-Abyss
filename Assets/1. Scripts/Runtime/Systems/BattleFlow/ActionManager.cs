using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ActionManager : IDisposable
{
    private readonly List<ActionSlot> slots = new();

    private long nextActionId = 1;
    private bool isDisposed;
    private bool hasLoggedDisposedMutation;

    public IReadOnlyList<ActionSlot> Slots => slots;
    public bool IsDisposed => isDisposed;

    public void Clear()
    {
        if (isDisposed)
            return;

        ClearInternal();
    }

    public void ResetForBattle()
    {
        if (!EnsureWritable(nameof(ResetForBattle)))
            return;

        ClearInternal();
        nextActionId = 1;
        hasLoggedDisposedMutation = false;
    }

    public IReadOnlyList<ActionSlot> CreateExecutionSnapshot()
    {
        if (isDisposed || slots.Count == 0)
            return Array.Empty<ActionSlot>();

        return GetAllSlots();
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        ClearInternal();
        isDisposed = true;
    }

    private void ClearInternal()
    {
        ClearTargetSlotLinks();
        slots.Clear();
    }

    //--------------------------------
    // 단순 추가
    // 같은 키가 이미 있으면 다음 ActionIndex를 자동 할당한다.
    //--------------------------------

    public void AddSlot(ActionSlot slot)
    {
        TryAddSlot(slot);
    }

    public bool TryAddSlot(ActionSlot slot)
    {
        if (!EnsureWritable(nameof(TryAddSlot)) ||
            slot == null)
        {
            return false;
        }

        NormalizeForAdd(slot);
        slot.Owner?.ConfigureActionSlot(slot);

        if (slot.Owner != null && !slot.Owner.CanUseActionSlot(slot))
            return false;

        if (!string.IsNullOrWhiteSpace(slot.SlotId) &&
            FindSlot(slot.Owner, slot.SlotId) != null)
        {
            Debug.LogWarning(
                $"[ActionManager] 이미 계획된 SlotId입니다: {slot.SlotId}");
            return false;
        }

        if (!CanAddByConfiguredSlotLimit(slot, null))
        {
            LogConfiguredSlotLimitFailure(slot);
            return false;
        }

        if (!CanReserveEnergy(
                slot.Owner,
                slot.Skill,
                slot.Part,
                slot.ActionIndex,
                slot.SlotId))
        {
            LogEnergyReservationFailure(slot);
            return false;
        }

        EnsureActionId(slot);
        slots.Add(slot);

        Debug.Log(
            "[ActionManager AddSlot]\n" +
            FormatSlot(slot));

        return true;
    }

    //--------------------------------
    // Owner + Part + ActionIndex가 같은 슬롯만 교체한다.
    //--------------------------------

    public void AddOrReplaceSlot(ActionSlot slot)
    {
        TryAddOrReplaceSlot(slot);
    }

    public bool TryAddOrReplaceSlot(ActionSlot slot)
    {
        if (!EnsureWritable(nameof(TryAddOrReplaceSlot)) ||
            slot == null)
        {
            return false;
        }

        if (slot.ActionIndex < 0)
            slot.ActionIndex = 0;

        slot.Owner?.ConfigureActionSlot(slot);
        if (slot.Owner != null && !slot.Owner.CanUseActionSlot(slot))
            return false;

        ActionSlot oldSlot =
            !string.IsNullOrWhiteSpace(slot.SlotId)
                ? FindSlot(slot.Owner, slot.SlotId)
                : FindSlot(
                    slot.Owner,
                    slot.Part,
                    slot.ActionIndex);

        if (oldSlot == slot)
            return true;

        if (!CanAddByConfiguredSlotLimit(slot, oldSlot))
        {
            LogConfiguredSlotLimitFailure(slot);
            return false;
        }

        if (!CanReserveEnergy(
                slot.Owner,
                slot.Skill,
                slot.Part,
                slot.ActionIndex,
                slot.SlotId))
        {
            LogEnergyReservationFailure(slot);
            return false;
        }

        if (oldSlot != null)
        {
            int oldIndex =
                slots.IndexOf(oldSlot);

            ClearReferencesTo(oldSlot);

            // 같은 논리 슬롯을 수정하는 것이므로
            // 기존 ActionId를 보존한다.
            slot.ActionId = oldSlot.ActionId;

            slots[oldIndex] = slot;

            Debug.Log(
                "[ActionManager ReplaceSlot - Old]\n" +
                FormatSlot(oldSlot));

            BattleDebugLog.ActionSlot(
                "[ActionManager AddOrReplaceSlot - New]\n" +
                FormatSlot(slot));

            return true;
        }

        EnsureActionId(slot);
        slots.Add(slot);

        BattleDebugLog.ActionSlot(
            "[ActionManager AddOrReplaceSlot - New]\n" +
            FormatSlot(slot));

        return true;
    }

    public bool RemoveSlot(ActionSlot slot)
    {
        if (!EnsureWritable(nameof(RemoveSlot)) ||
            slot == null)
            return false;

        ActionSlot storedSlot = slot;

        if (!slots.Contains(storedSlot) &&
            slot.ActionId > 0)
        {
            storedSlot =
                FindSlotById(slot.ActionId);
        }

        if (storedSlot == null)
            return false;

        bool removed =
            slots.Remove(storedSlot);

        if (!removed)
            return false;

        ClearReferencesTo(storedSlot);

        Debug.Log(
            "[ActionManager RemoveSlot]\n" +
            FormatSlot(storedSlot));

        return true;
    }

    public bool RemoveSlotById(long actionId)
    {
        if (actionId <= 0)
            return false;

        return RemoveSlot(
            FindSlotById(actionId));
    }

    public bool RemoveSlot(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        return RemoveSlot(
            FindSlot(
                owner,
                part,
                actionIndex));
    }

    //--------------------------------
    // 기존 호출부 호환:
    // 같은 Owner + Part 중 가장 낮은 ActionIndex 하나만 제거한다.
    // Character의 기존 while 루프는 이 메서드를 반복 호출하여 전부 제거한다.
    //--------------------------------

    public void RemoveSlot(
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return;

        RemoveSlot(
            FindSlot(owner, part));
    }

    public int RemoveSlots(
        Character owner,
        BodyPart part)
    {
        if (!EnsureWritable(nameof(RemoveSlots)))
            return 0;

        return RemoveWhere(
            slot =>
                slot.Owner == owner &&
                slot.Part == part);
    }

    public void RemoveSlotsByOwner(
        Character owner)
    {
        if (!EnsureWritable(nameof(RemoveSlotsByOwner)) ||
            owner == null)
            return;

        RemoveWhere(
            slot => slot.Owner == owner);
    }

    public ActionSlot FindSlot(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        if (isDisposed)
            return null;

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.HasSameKey(
                    owner,
                    part,
                    actionIndex))
            {
                return slot;
            }
        }

        return null;
    }

    //--------------------------------
    // 기존 호출부 호환:
    // 해당 부위의 가장 낮은 ActionIndex를 반환한다.
    //--------------------------------

    public ActionSlot FindSlot(
        Character owner,
        BodyPart part)
    {
        if (isDisposed)
            return null;

        ActionSlot result = null;

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.Owner != owner ||
                slot.Part != part)
            {
                continue;
            }

            if (result == null ||
                slot.ActionIndex < result.ActionIndex ||
                (slot.ActionIndex == result.ActionIndex &&
                 slot.ActionId < result.ActionId))
            {
                result = slot;
            }
        }

        return result;
    }

    public ActionSlot FindSlot(
        Character owner,
        string slotId)
    {
        if (isDisposed ||
            owner == null ||
            string.IsNullOrWhiteSpace(slotId))
        {
            return null;
        }

        foreach (ActionSlot slot in slots)
        {
            if (slot?.Owner != owner)
                continue;

            if (string.Equals(
                    slot.SlotId,
                    slotId,
                    StringComparison.Ordinal))
            {
                return slot;
            }
        }

        return null;
    }

    public bool RemoveSlot(
        Character owner,
        string slotId)
    {
        return RemoveSlot(
            FindSlot(owner, slotId));
    }

    public ActionSlot FindSlotById(
        long actionId)
    {
        if (isDisposed ||
            actionId <= 0)
            return null;

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.ActionId == actionId)
                return slot;
        }

        return null;
    }

    public List<ActionSlot> FindSlots(
        Character owner,
        BodyPart part)
    {
        List<ActionSlot> result = new();

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.Owner == owner &&
                slot.Part == part)
            {
                result.Add(slot);
            }
        }

        result.Sort(CompareByActionIndex);
        return result;
    }

    public int GetNextAvailableActionIndex(
        Character owner,
        BodyPart part)
    {
        int actionIndex = 0;

        while (FindSlot(
                   owner,
                   part,
                   actionIndex) != null)
        {
            actionIndex++;
        }

        return actionIndex;
    }

    public int CountSlots(
        Character owner)
    {
        if (isDisposed)
            return 0;

        int count = 0;

        foreach (ActionSlot slot in slots)
        {
            if (slot != null &&
                slot.Owner == owner)
            {
                count++;
            }
        }

        return count;
    }


    public int CountCombatSlots(Character owner)
    {
        if (owner == null)
            return 0;

        int count = 0;
        foreach (ActionSlot slot in slots)
        {
            if (slot?.Owner == owner &&
                slot.Phase == ActionPhase.COMBAT)
            {
                count++;
            }
        }
        return count;
    }

    private bool CanAddByConfiguredSlotLimit(
        ActionSlot incoming,
        ActionSlot replacing)
    {
        if (incoming?.Owner == null)
            return false;

        int count = CountSlots(incoming.Owner);

        if (replacing != null &&
            replacing.Owner == incoming.Owner)
        {
            count--;
        }

        return incoming.Owner.CanUseActionSlot(incoming) &&
               count < incoming.Owner.GetMaxActionSlots();
    }

    private void LogConfiguredSlotLimitFailure(ActionSlot slot)
    {
        Debug.LogWarning(
            $"[ActionManager] 행동 슬롯 상한 초과 / " +
            $"Owner={slot?.Owner?.Data?.CharacterName}, " +
            $"Limit={slot?.Owner?.GetMaxActionSlots() ?? 0}, " +
            $"Current={CountSlots(slot?.Owner)}");
    }

    public int GetPlannedEnergyCost(
        Character owner,
        BodyPart excludedPart = null,
        int excludedActionIndex = -1,
        string excludedSlotId = null)
    {
        if (isDisposed || owner == null)
            return 0;

        long total = 0;

        foreach (ActionSlot slot in slots)
        {
            if (slot?.Owner != owner || slot.Skill == null)
                continue;

            bool excludedBySlotId =
                !string.IsNullOrWhiteSpace(excludedSlotId) &&
                string.Equals(
                    slot.SlotId,
                    excludedSlotId,
                    StringComparison.Ordinal);

            bool excludedByLegacyKey =
                excludedActionIndex >= 0 &&
                slot.Part == excludedPart &&
                slot.ActionIndex == excludedActionIndex;

            if (excludedBySlotId || excludedByLegacyKey)
                continue;

            total += Mathf.Max(0, slot.Skill.EnergyCost);

            if (total >= int.MaxValue)
                return int.MaxValue;
        }

        return (int)total;
    }

    public int GetRemainingEnergyAfterPlan(
        Character owner,
        BodyPart excludedPart = null,
        int excludedActionIndex = -1,
        string excludedSlotId = null)
    {
        if (owner == null)
            return 0;

        return Mathf.Max(
            0,
            owner.CurrentEnergy -
            GetPlannedEnergyCost(
                owner,
                excludedPart,
                excludedActionIndex,
                excludedSlotId));
    }

    public bool CanReserveEnergy(
        Character owner,
        Skill skill,
        BodyPart excludedPart = null,
        int excludedActionIndex = -1,
        string excludedSlotId = null)
    {
        if (owner == null || skill == null)
            return false;

        int planned = GetPlannedEnergyCost(
            owner,
            excludedPart,
            excludedActionIndex,
            excludedSlotId);

        long required =
            (long)planned + Mathf.Max(0, skill.EnergyCost);

        return required <= owner.CurrentEnergy;
    }

    private void LogEnergyReservationFailure(ActionSlot slot)
    {
        if (slot?.Owner == null || slot.Skill == null)
            return;

        int planned = GetPlannedEnergyCost(
            slot.Owner,
            slot.Part,
            slot.ActionIndex,
            slot.SlotId);

        Debug.LogWarning(
            $"[ActionManager] 에너지 예산 초과로 슬롯 등록 거부 / " +
            $"Owner={slot.Owner.Data?.CharacterName ?? slot.Owner.name}, " +
            $"Skill={slot.Skill.SkillName}, Cost={slot.Skill.EnergyCost}, " +
            $"Planned={planned}, Current={slot.Owner.CurrentEnergy}");
    }

    public List<ActionSlot> GetAllSlots()
    {
        if (isDisposed)
            return new List<ActionSlot>();

        List<ActionSlot> result =
            new(slots);

        result.Sort(CompareForExecution);
        return result;
    }

    public void PrintSlots(
        string title = "ACTION MANAGER SLOTS")
    {
        if (!BattleDebugLog.ShowActionSlot)
            return;

        StringBuilder sb = new();

        sb.AppendLine($"========== {title} ==========");
        sb.AppendLine($"Count : {slots.Count}");
        sb.AppendLine();

        for (int i = 0; i < slots.Count; i++)
        {
            sb.AppendLine($"[{i}]");
            sb.AppendLine(FormatSlot(slots[i]));
            sb.AppendLine("--------------------------------");
        }

        sb.AppendLine("================================");

        Debug.Log(sb.ToString());
    }

    private void NormalizeForAdd(
        ActionSlot slot)
    {
        if (slot.ActionIndex < 0)
        {
            slot.ActionIndex =
                GetNextAvailableActionIndex(
                    slot.Owner,
                    slot.Part);

            return;
        }

        if (FindSlot(
                slot.Owner,
                slot.Part,
                slot.ActionIndex) != null)
        {
            slot.ActionIndex =
                GetNextAvailableActionIndex(
                    slot.Owner,
                    slot.Part);
        }
    }

    private void EnsureActionId(
        ActionSlot slot)
    {
        if (slot == null)
            return;

        if (slot.ActionId > 0 &&
            FindSlotById(slot.ActionId) == null)
        {
            nextActionId =
                Math.Max(
                    nextActionId,
                    slot.ActionId + 1);

            return;
        }

        slot.ActionId = nextActionId;
        nextActionId++;
    }

    private int RemoveWhere(
        Predicate<ActionSlot> predicate)
    {
        if (predicate == null)
            return 0;

        List<ActionSlot> removed = new();

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            ActionSlot slot = slots[i];

            if (slot == null)
            {
                slots.RemoveAt(i);
                continue;
            }

            if (!predicate(slot))
                continue;

            removed.Add(slot);
            slots.RemoveAt(i);
        }

        foreach (ActionSlot removedSlot in removed)
        {
            ClearReferencesTo(removedSlot);
        }

        return removed.Count;
    }

    private void ClearReferencesTo(
        ActionSlot removedSlot)
    {
        if (removedSlot == null)
            return;

        removedSlot.TargetSlot = null;

        foreach (ActionSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.TargetSlot == removedSlot ||
                (removedSlot.ActionId > 0 &&
                 slot.TargetSlot != null &&
                 slot.TargetSlot.ActionId == removedSlot.ActionId))
            {
                slot.TargetSlot = null;
            }
        }
    }

    private void ClearTargetSlotLinks()
    {
        foreach (ActionSlot slot in slots)
        {
            if (slot != null)
                slot.TargetSlot = null;
        }
    }

    private static int CompareByActionIndex(
        ActionSlot a,
        ActionSlot b)
    {
        if (ReferenceEquals(a, b))
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int indexCompare =
            a.ActionIndex.CompareTo(
                b.ActionIndex);

        if (indexCompare != 0)
            return indexCompare;

        return a.ActionId.CompareTo(
            b.ActionId);
    }

    private static int CompareForExecution(
        ActionSlot a,
        ActionSlot b)
    {
        if (ReferenceEquals(a, b))
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        int speedCompare =
            b.Speed.CompareTo(a.Speed);

        if (speedCompare != 0)
            return speedCompare;

        int actionIndexCompare =
            a.ActionIndex.CompareTo(
                b.ActionIndex);

        if (actionIndexCompare != 0)
            return actionIndexCompare;

        return a.ActionId.CompareTo(
            b.ActionId);
    }

    private string FormatSlot(
        ActionSlot slot)
    {
        if (slot == null)
            return "NULL SLOT";

        string ownerName =
            GetCharacterName(slot.Owner);

        string partName =
            GetPartName(slot.Part);

        string skillName =
            slot.Skill == null
                ? "NULL"
                : slot.Skill.SkillName;

        string targetName =
            GetCharacterName(
                slot.TargetCharacter);

        string targetPartName =
            GetPartName(
                slot.TargetPart);

        string targetSlotName =
            slot.TargetSlot == null
                ? "NULL"
                : $"{GetCharacterName(slot.TargetSlot.Owner)} " +
                  $"{GetPartName(slot.TargetSlot.Part)} " +
                  $"[Index={slot.TargetSlot.ActionIndex}, Id={slot.TargetSlot.ActionId}]";

        return
            $"ActionId   : {slot.ActionId}\n" +
            $"ActionIndex: {slot.ActionIndex}\n" +
            $"SlotId     : {slot.SlotId ?? "NULL"}\n" +
            $"Owner      : {ownerName}\n" +
            $"Part       : {partName}\n" +
            $"Skill      : {skillName}\n" +
            $"Target     : {targetName}\n" +
            $"TargetPart : {targetPartName}\n" +
            $"Speed      : {slot.Speed}\n" +
            $"Phase      : {slot.Phase}\n" +
            $"TargetSlot : {targetSlotName}";
    }

    private bool EnsureWritable(string operation)
    {
        if (!isDisposed)
            return true;

        if (!hasLoggedDisposedMutation)
        {
            hasLoggedDisposedMutation = true;

            Debug.LogWarning(
                "[ActionManager] Dispose 이후 변경 요청을 무시합니다. " +
                $"Operation={operation}");
        }

        return false;
    }

    private static string GetCharacterName(
        Character character)
    {
        if (character == null)
            return "NULL";

        if (character.Data == null)
            return character.name;

        return character.Data.CharacterName;
    }

    private static string GetPartName(
        BodyPart part)
    {
        return part == null
            ? "NONE"
            : part.Type.ToString();
    }
}