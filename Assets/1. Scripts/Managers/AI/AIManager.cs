using System.Collections.Generic;
using UnityEngine;

public class AIManager
{
    private readonly BattleContext battleContext;
    private readonly ActionManager actionManager;

    public AIManager(
        BattleContext battleContext,
        ActionManager actionManager)
    {
        this.battleContext = battleContext;
        this.actionManager = actionManager;
    }

    public void DecideEnemyActions()
    {
        if (battleContext?.Enemies == null ||
            actionManager == null)
        {
            return;
        }

        foreach (Character character in battleContext.Enemies)
        {
            if (character is not Enemy enemy)
                continue;

            if (enemy.IsDead)
                continue;

            List<ActionSlot> plannedSlots =
                enemy.DecideSlots(battleContext);

            if (plannedSlots == null)
                continue;

            foreach (ActionSlot slot in plannedSlots)
            {
                if (slot == null)
                    continue;

                if (!TryNormalizeEnemyActionIndex(enemy, slot))
                    continue;

                actionManager.AddOrReplaceSlot(slot);
            }
        }
    }

    private bool TryNormalizeEnemyActionIndex(
        Enemy enemy,
        ActionSlot slot)
    {
        if (slot.Owner == null)
            slot.Owner = enemy;

        int maxSlots =
            GetMaxSlots(slot.Owner, slot.Part);

        if (maxSlots <= 0)
        {
            Debug.LogWarning(
                $"[AI SLOT SKIP] 사용할 수 없는 행동 원천입니다. " +
                $"Owner={GetCharacterName(slot.Owner)}, " +
                $"Part={GetPartName(slot.Part)}");
            return false;
        }

        bool exactIndexOccupied =
            slot.ActionIndex < 0 ||
            actionManager.FindSlot(
                slot.Owner,
                slot.Part,
                slot.ActionIndex) != null;

        if (exactIndexOccupied)
        {
            slot.ActionIndex =
                actionManager.GetNextAvailableActionIndex(
                    slot.Owner,
                    slot.Part);
        }

        if (slot.ActionIndex >= maxSlots)
        {
            Debug.LogWarning(
                $"[AI SLOT SKIP] 최대 슬롯 수 초과. " +
                $"Owner={GetCharacterName(slot.Owner)}, " +
                $"Part={GetPartName(slot.Part)}, " +
                $"Index={slot.ActionIndex}, Max={maxSlots}");
            return false;
        }

        return true;
    }

    private int GetMaxSlots(
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return 0;

        // 이후 일반몹의 OwnerPart == null 행동을 허용한다.
        if (part == null)
            return 1;

        return Mathf.Max(
            0,
            owner.GetMaxActionSlotsForPart(part));
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
}
