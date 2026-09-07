using System.Collections.Generic;
using UnityEngine;

public class AIManager
{
    private readonly BattleContext battleContext;
    private readonly ActionManager actionManager;
    private readonly AIActionPlanner actionPlanner;

    public AIManager(
        BattleContext battleContext,
        ActionManager actionManager)
    {
        this.battleContext = battleContext;
        this.actionManager = actionManager;

        AITargetSelector targetSelector =
            new AITargetSelector(actionManager);

        AISkillSelector skillSelector =
            new AISkillSelector(targetSelector);

        AISlotPlanner slotPlanner =
            new AISlotPlanner();

        actionPlanner =
            new AIActionPlanner(
                slotPlanner,
                skillSelector);
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
            if (character is not Enemy enemy ||
                enemy.IsDead ||
                !enemy.IsInitialized)
            {
                continue;
            }

            // 이전 계획이 남아 있으면 부위 파괴나 슬롯 수 변경 뒤에도
            // stale slot이 실행될 수 있으므로 적 단위로 먼저 정리한다.
            actionManager.RemoveSlotsByOwner(enemy);

            List<ActionSlot> plannedSlots =
                actionPlanner.PlanEnemy(
                    enemy,
                    battleContext);

            foreach (ActionSlot slot in plannedSlots)
            {
                if (!IsValidFinalSlot(enemy, slot))
                    continue;

                actionManager.AddOrReplaceSlot(slot);
            }
        }
    }

    private bool IsValidFinalSlot(
        Enemy enemy,
        ActionSlot slot)
    {
        if (enemy == null ||
            slot == null ||
            slot.Owner != enemy ||
            slot.Skill == null ||
            slot.TargetCharacter == null)
        {
            return false;
        }

        if (slot.Owner.IsDead ||
            slot.TargetCharacter.IsDead)
        {
            return false;
        }

        if (slot.Part != null)
        {
            if (slot.Part.Owner != null &&
                slot.Part.Owner != enemy)
            {
                return false;
            }

            if (slot.Part.IsBroken)
                return false;
        }
        else if (!enemy.IsSingleHpTarget &&
                 (enemy.CombatRulesRuntime?.GetSlotCountForPart(null) ?? 0) <= 0)
        {
            return false;
        }

        int maxSlots =
            Mathf.Max(
                0,
                enemy.GetMaxActionSlotsForPart(
                    slot.Part));

        if (slot.ActionIndex < 0 ||
            slot.ActionIndex >= maxSlots)
        {
            Debug.LogWarning(
                $"[AI SLOT SKIP] 최대 슬롯 수 초과 / " +
                $"Owner={GetCharacterName(enemy)}, " +
                $"Part={GetPartName(slot.Part)}, " +
                $"Index={slot.ActionIndex}, Max={maxSlots}");

            return false;
        }

        if (!enemy.CanUseSkill(
                slot.Part,
                slot.Skill))
        {
            return false;
        }

        bool allowBrokenTarget =
            AITargetSelector.ShouldIncludeBrokenTargets(
                slot.Skill);

        if (!slot.TargetCharacter.IsValidTargetPart(
                slot.TargetPart,
                allowBrokenTarget))
        {
            return false;
        }

        return true;
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