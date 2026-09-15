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

                if (!actionManager.TryAddOrReplaceSlot(slot))
                    continue;

                if (!TryCommitPlannedSlot(
                        enemy,
                        slot,
                        out string failureReason))
                {
                    actionManager.RemoveSlot(slot);

                    Debug.LogWarning(
                        $"[AI PLAN COMMIT FAILED] " +
                        $"Owner={GetCharacterDebugName(enemy)}, " +
                        $"Skill={slot.Skill?.SkillName ?? "NULL"}, " +
                        $"Reason={failureReason}");
                }
            }
        }

        // C-04 safety net: 실제 ActionManager에 남아 Resolution으로 갈 적 슬롯은
        // 반드시 계획 단계에서 비용 commit이 끝나 있어야 한다.
        // 동일 표시 이름의 다중 일반 적처럼 개별 AI loop에서 commit 로그가 누락되는
        // 경로가 생겨도 Resolution 직전 invariant를 한 번 더 강제한다.
        EnsureAllEnemySlotsCommitted();
    }


    private void EnsureAllEnemySlotsCommitted()
    {
        if (battleContext?.Enemies == null ||
            actionManager == null)
        {
            return;
        }

        List<ActionSlot> snapshot =
            new List<ActionSlot>(actionManager.Slots);

        foreach (ActionSlot slot in snapshot)
        {
            if (slot?.Owner is not Enemy enemy ||
                slot.Skill == null ||
                !battleContext.Enemies.Contains(enemy))
            {
                continue;
            }

            bool preparationNeedsCommit =
                slot.Skill.ActionType == ActionType.Preparation &&
                (!slot.PlanningEffectCommitted || !slot.SkipResolution);

            if (slot.ResourceCostCommitted &&
                !preparationNeedsCommit)
            {
                continue;
            }

            if (!IsValidFinalSlot(enemy, slot))
            {
                actionManager.RemoveSlot(slot);
                Debug.LogWarning(
                    $"[AI PLAN INVARIANT REMOVE] " +
                    $"Owner={GetCharacterDebugName(enemy)}, " +
                    $"ActionId={slot.ActionId}, " +
                    $"Skill={slot.Skill.SkillName}, " +
                    "Resolution 직전 유효성 검사를 통과하지 못해 슬롯을 제거했습니다.");
                continue;
            }

            if (!TryCommitPlannedSlot(
                    enemy,
                    slot,
                    out string failureReason))
            {
                actionManager.RemoveSlot(slot);
                Debug.LogWarning(
                    $"[AI PLAN INVARIANT COMMIT FAILED] " +
                    $"Owner={GetCharacterDebugName(enemy)}, " +
                    $"ActionId={slot.ActionId}, " +
                    $"Skill={slot.Skill.SkillName}, " +
                    $"Reason={failureReason}");
                continue;
            }

            Debug.Log(
                $"[AI PLAN INVARIANT REPAIRED] " +
                $"Owner={GetCharacterDebugName(enemy)}, " +
                $"ActionId={slot.ActionId}, " +
                $"Skill={slot.Skill.SkillName}, " +
                $"CostCommitted={slot.ResourceCostCommitted}, " +
                $"ImmediatePreparation={slot.PlanningEffectCommitted}");
        }
    }


    /// <summary>
    /// C-03/C-04: 적 AI도 플레이어와 동일하게 계획 확정 시
    /// mechanic hook과 자원 비용을 commit한다.
    /// 이후 Resolution 중 슬롯이 무효화되어도 이미 낸 비용은 환불하지 않는다.
    /// 도사림은 계획 단계에서 즉시 실행하고 Resolution queue에서 제외한다.
    /// </summary>
    private bool TryCommitPlannedSlot(
        Enemy enemy,
        ActionSlot slot,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (enemy == null ||
            slot == null ||
            slot.Owner != enemy ||
            slot.Skill == null)
        {
            failureReason = "AI 계획 슬롯 정보가 올바르지 않습니다.";
            return false;
        }

        // 후처리 invariant pass가 다시 들어와도 도사림 효과를 중복 실행하지 않는다.
        if (slot.ResourceCostCommitted)
        {
            if (slot.Skill.ActionType != ActionType.Preparation)
                return true;

            if (slot.PlanningEffectCommitted)
            {
                slot.SkipResolution = true;
                return true;
            }
        }

        if (!ActionPlanningMechanicPolicy.TryCommitPlannedSlot(
                enemy,
                slot,
                out failureReason))
        {
            if (string.IsNullOrWhiteSpace(failureReason))
                failureReason = "Planning mechanic commit 실패";

            return false;
        }

        BattleAction planningAction =
            new BattleAction
            {
                Slot = slot
            };

        if (!slot.ResourceCostCommitted &&
            !slot.Skill.TryConsumeResource(
                enemy,
                planningAction))
        {
            ActionPlanningMechanicPolicy.RollbackPlannedSlot(
                enemy,
                slot);

            failureReason =
                "계획 확정 시 자원 비용을 지불할 수 없습니다.";
            return false;
        }

        slot.ResourceCostCommitted = true;

        if (slot.Skill.ActionType == ActionType.Preparation)
        {
            enemy.BattleEvent?.RaiseActionStart(
                planningAction);

            slot.Skill.Execute(
                planningAction);

            enemy.BattleEvent?.RaiseActionEnd(
                planningAction);

            slot.PlanningEffectCommitted = true;
            slot.SkipResolution = true;
        }

        Debug.Log(
            $"[AI PLAN COMMIT] " +
            $"Owner={GetCharacterDebugName(enemy)}, " +
            $"Skill={slot.Skill.SkillName}, " +
            $"CostCommitted={slot.ResourceCostCommitted}, " +
            $"Energy={enemy.CurrentEnergy}/{enemy.MaxEnergy}, " +
            $"ImmediatePreparation={slot.PlanningEffectCommitted}");

        return true;
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

    private string GetCharacterDebugName(
        Character character)
    {
        if (character == null)
            return "NULL";

        return
            $"{GetCharacterName(character)}#{character.GetInstanceID()}";
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
