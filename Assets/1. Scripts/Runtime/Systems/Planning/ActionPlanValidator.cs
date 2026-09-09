using System.Collections.Generic;
using UnityEngine;

public enum ActionPlanValidationCode
{
    None,
    ServiceUnavailable,
    MissingOwner,
    MissingSkill,
    InvalidActionIndex,
    OwnerDead,
    InvalidOwnerPart,
    OwnerPartUnavailable,
    SkillNotSelectable,
    MechanicBlocked,
    PrestigeNotReady,
    SkillConditionOrResourceBlocked,
    EnergyReservationExceeded,
    PrestigeAlreadyPlanned,
    MissingTarget,
    TargetDead,
    InvalidPreparationTarget,
    InvalidTarget
}

public readonly struct ActionPlanValidationResult
{
    public ActionPlanValidationResult(
        bool success,
        ActionPlanValidationCode code,
        string reason)
    {
        Success = success;
        Code = code;
        Reason = reason ?? string.Empty;
    }

    public bool Success { get; }
    public ActionPlanValidationCode Code { get; }
    public string Reason { get; }

    public static ActionPlanValidationResult Valid() =>
        new(true, ActionPlanValidationCode.None, string.Empty);

    public static ActionPlanValidationResult Invalid(
        ActionPlanValidationCode code,
        string reason) =>
        new(false, code, reason);
}

/// <summary>
/// UI/수동 Command/자동 계획이 공유해야 하는 Planning 필수 제약을 한 곳에서 검사한다.
/// 점수 계산이나 AI 전략은 다루지 않고, 실제 계획에 들어가도 되는지에 대한 계약만 소유한다.
/// </summary>
public sealed class ActionPlanValidator
{
    private readonly ActionManager actionManager;

    public ActionPlanValidator(
        ActionManager actionManager)
    {
        this.actionManager = actionManager;
    }

    public ActionPlanValidationResult ValidateSkillSelection(
        Character owner,
        BodyPart part,
        Skill skill,
        int actionIndex)
    {
        return ValidateSkillSelection(
            owner,
            part,
            skill,
            actionIndex,
            actionManager?.Slots,
            useLiveEnergyReservation: true);
    }

    public ActionPlanValidationResult ValidateAssignment(
        ActionPlanAssignmentRequest request)
    {
        if (request == null)
        {
            return Invalid(
                ActionPlanValidationCode.MissingOwner,
                "행동 계획 정보가 없습니다.");
        }

        ActionPlanValidationResult skillResult =
            ValidateSkillSelection(
                request.Owner,
                request.OwnerPart,
                request.Skill,
                request.ActionIndex);

        if (!skillResult.Success)
            return skillResult;

        return ValidateTarget(
            request.Owner,
            request.Skill,
            request.Target,
            request.TargetPart,
            request.TargetRule);
    }

    /// <summary>
    /// 자동 계획처럼 owner 전체를 교체하는 경로의 최종 필수 검증.
    /// 기존 live owner 계획의 예약량은 포함하지 않고 새 후보 집합 자체를 검사한다.
    /// 실제 commit 원자성/슬롯 상한은 ActionManager.TryReplaceOwnerPlan이 다시 보장한다.
    /// </summary>
    public ActionPlanValidationResult ValidateReplacementPlan(
        Character owner,
        IReadOnlyList<ActionSlot> plannedSlots)
    {
        if (actionManager == null)
        {
            return Invalid(
                ActionPlanValidationCode.ServiceUnavailable,
                "Planning service가 준비되지 않았습니다.");
        }

        if (owner == null)
        {
            return Invalid(
                ActionPlanValidationCode.MissingOwner,
                "행동 주체가 없습니다.");
        }

        if (plannedSlots == null)
        {
            return Invalid(
                ActionPlanValidationCode.MissingSkill,
                "자동 계획 후보가 없습니다.");
        }

        long reservedEnergy = 0;
        int prestigeCount = 0;

        foreach (ActionSlot slot in plannedSlots)
        {
            if (slot == null)
                continue;

            if (slot.Owner != owner)
            {
                return Invalid(
                    ActionPlanValidationCode.MissingOwner,
                    "다른 행동 주체의 슬롯이 자동 계획에 포함되어 있습니다.");
            }

            ActionPlanValidationResult skillResult =
                ValidateSkillSelection(
                    owner,
                    slot.Part,
                    slot.Skill,
                    slot.ActionIndex,
                    plannedSlots,
                    useLiveEnergyReservation: false);

            if (!skillResult.Success)
                return skillResult;

            TargetSelectionRule rule =
                slot.Skill?.ActionType == ActionType.Preparation
                    ? TargetSelectionRule.LivingPartOnly
                    : TargetSelectionRule.StandardAttack;

            ActionPlanValidationResult targetResult =
                ValidateTarget(
                    owner,
                    slot.Skill,
                    slot.TargetCharacter,
                    slot.TargetPart,
                    rule);

            if (!targetResult.Success)
                return targetResult;

            ActionPlanningSkillContext context =
                new(
                    owner,
                    slot.Part,
                    slot.Skill,
                    slot.ActionIndex,
                    plannedSlots);

            bool exempt =
                ActionPlanningMechanicPolicy
                    .IsEnergyReservationExempt(context);

            if (!exempt)
            {
                reservedEnergy +=
                    Mathf.Max(0, slot.Skill?.EnergyCost ?? 0);

                if (reservedEnergy > owner.CurrentEnergy)
                {
                    return Invalid(
                        ActionPlanValidationCode.EnergyReservationExceeded,
                        $"계획 에너지 부족 ({owner.CurrentEnergy}/{reservedEnergy})");
                }
            }

            if (slot.Skill?.ActionType == ActionType.Prestige &&
                slot.Skill.PrestigeUsePolicy == PrestigeUsePolicy.OncePerTurn)
            {
                prestigeCount++;
                if (prestigeCount > 1)
                {
                    return Invalid(
                        ActionPlanValidationCode.PrestigeAlreadyPlanned,
                        "이번 턴 위세 사용됨");
                }
            }
        }

        return ActionPlanValidationResult.Valid();
    }

    public bool HasPrestigeSlotSelected(
        Character owner,
        BodyPart ignorePart = null,
        int ignoreActionIndex = -1)
    {
        return HasPrestigeSlotSelected(
            owner,
            actionManager?.Slots,
            ignorePart,
            ignoreActionIndex);
    }

    private ActionPlanValidationResult ValidateSkillSelection(
        Character owner,
        BodyPart part,
        Skill skill,
        int actionIndex,
        IReadOnlyList<ActionSlot> plannedSlots,
        bool useLiveEnergyReservation)
    {
        if (actionManager == null)
        {
            return Invalid(
                ActionPlanValidationCode.ServiceUnavailable,
                "Planning service가 준비되지 않았습니다.");
        }

        if (owner == null)
        {
            return Invalid(
                ActionPlanValidationCode.MissingOwner,
                "행동 부위 미선택");
        }

        if (skill == null)
        {
            return Invalid(
                ActionPlanValidationCode.MissingSkill,
                "스킬 없음");
        }

        if (actionIndex < 0)
        {
            return Invalid(
                ActionPlanValidationCode.InvalidActionIndex,
                "ActionIndex가 올바르지 않습니다.");
        }

        if (owner.IsDead)
        {
            return Invalid(
                ActionPlanValidationCode.OwnerDead,
                "사망한 캐릭터는 행동할 수 없습니다.");
        }

        bool allowsCharacterLevelSlot =
            owner.IsSingleHpTarget ||
            (owner.CombatRulesRuntime?.GetSlotCountForPart(null) ?? 0) > 0;

        if (part == null && !allowsCharacterLevelSlot)
        {
            return Invalid(
                ActionPlanValidationCode.InvalidOwnerPart,
                "행동 부위가 없습니다.");
        }

        if (part != null)
        {
            if (part.Owner != null && part.Owner != owner)
            {
                return Invalid(
                    ActionPlanValidationCode.InvalidOwnerPart,
                    "행동 부위의 소유자가 올바르지 않습니다.");
            }

            if (part.IsBroken || !part.IsUsable)
            {
                return Invalid(
                    ActionPlanValidationCode.OwnerPartUnavailable,
                    "부위 사용 불가");
            }
        }

        IReadOnlyList<Skill> selectable =
            owner.GetSelectableSkills(
                part,
                actionIndex);

        if (!ContainsSkill(selectable, skill))
        {
            CharacterCombatRulesRuntime rules =
                owner.CombatRulesRuntime;

            CharacterSkillLoadoutRuntime loadout =
                rules?.Loadout;

            string reason =
                rules?.CurrentBossPhase == null &&
                loadout?.HasSource == true &&
                skill.Definition != null &&
                !loadout.IsEquipped(skill.Definition)
                    ? "미장착 스킬"
                    : "현재 부위·행동 슬롯에서 사용 불가";

            return Invalid(
                ActionPlanValidationCode.SkillNotSelectable,
                reason);
        }

        ActionPlanningSkillContext planningContext =
            new(
                owner,
                part,
                skill,
                actionIndex,
                plannedSlots);

        string mechanicReason =
            ActionPlanningMechanicPolicy
                .GetSkillSelectionBlockReason(
                    planningContext);

        if (!string.IsNullOrWhiteSpace(mechanicReason))
        {
            return Invalid(
                ActionPlanValidationCode.MechanicBlocked,
                mechanicReason);
        }

        if (skill.ActionType == ActionType.Prestige &&
            !IsPrestigeReady(owner))
        {
            return Invalid(
                ActionPlanValidationCode.PrestigeNotReady,
                "위세 부족");
        }

        bool energyReservationExempt =
            ActionPlanningMechanicPolicy
                .IsEnergyReservationExempt(
                    planningContext);

        if (!energyReservationExempt &&
            !owner.CanUseSkill(part, skill))
        {
            string reason =
                !owner.CanAffordEnergy(skill.EnergyCost)
                    ? $"에너지 부족 ({owner.CurrentEnergy}/{skill.EnergyCost})"
                    : "조건 또는 자원 부족";

            return Invalid(
                ActionPlanValidationCode.SkillConditionOrResourceBlocked,
                reason);
        }

        if (!energyReservationExempt &&
            useLiveEnergyReservation &&
            !actionManager.CanReserveEnergy(
                owner,
                skill,
                part,
                actionIndex))
        {
            int remaining =
                actionManager.GetRemainingEnergyAfterPlan(
                    owner,
                    part,
                    actionIndex);

            return Invalid(
                ActionPlanValidationCode.EnergyReservationExceeded,
                $"계획 에너지 부족 ({remaining}/{skill.EnergyCost})");
        }

        if (skill.ActionType == ActionType.Prestige &&
            skill.PrestigeUsePolicy == PrestigeUsePolicy.OncePerTurn &&
            HasPrestigeSlotSelected(
                owner,
                plannedSlots,
                part,
                actionIndex))
        {
            return Invalid(
                ActionPlanValidationCode.PrestigeAlreadyPlanned,
                "이번 턴 위세 사용됨");
        }

        return ActionPlanValidationResult.Valid();
    }

    private static ActionPlanValidationResult ValidateTarget(
        Character owner,
        Skill skill,
        Character target,
        BodyPart targetPart,
        TargetSelectionRule targetRule)
    {
        if (target == null)
        {
            return Invalid(
                ActionPlanValidationCode.MissingTarget,
                "대상 정보가 부족합니다.");
        }

        if (target.IsDead)
        {
            return Invalid(
                ActionPlanValidationCode.TargetDead,
                "사망한 대상을 선택할 수 없습니다.");
        }

        if (skill?.ActionType == ActionType.Preparation)
        {
            if (owner == null ||
                target != owner ||
                targetPart == null ||
                targetPart.Owner != target ||
                targetPart.IsBroken)
            {
                return Invalid(
                    ActionPlanValidationCode.InvalidPreparationTarget,
                    "도사림 자기 대상이 올바르지 않습니다.");
            }

            return ActionPlanValidationResult.Valid();
        }

        if (!BattleTargetValidator.IsValid(
                target,
                targetPart,
                targetRule))
        {
            return Invalid(
                ActionPlanValidationCode.InvalidTarget,
                "대상 계약이 올바르지 않습니다.");
        }

        return ActionPlanValidationResult.Valid();
    }

    private static bool ContainsSkill(
        IReadOnlyList<Skill> skills,
        Skill targetSkill)
    {
        if (skills == null || targetSkill == null)
            return false;

        foreach (Skill skill in skills)
        {
            if (ReferenceEquals(skill, targetSkill))
                return true;
        }

        return false;
    }

    private static bool HasPrestigeSlotSelected(
        Character owner,
        IReadOnlyList<ActionSlot> plannedSlots,
        BodyPart ignorePart,
        int ignoreActionIndex)
    {
        if (owner == null || plannedSlots == null)
            return false;

        foreach (ActionSlot slot in plannedSlots)
        {
            if (slot == null ||
                slot.Owner != owner ||
                slot.Skill == null)
            {
                continue;
            }

            bool ignored =
                ignoreActionIndex >= 0 &&
                IsSamePart(slot.Part, ignorePart) &&
                slot.ActionIndex == ignoreActionIndex;

            if (!ignored &&
                slot.Skill.ActionType == ActionType.Prestige)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPrestigeReady(
        Character character)
    {
        return character?.CurrentStatus != null &&
               character.RuntimeStatus != null &&
               character.CurrentStatus.maxPrestige > 0 &&
               character.RuntimeStatus.currentPrestige >=
               character.CurrentStatus.maxPrestige;
    }

    private static bool IsSamePart(
        BodyPart first,
        BodyPart second)
    {
        if (first == null || second == null)
            return first == null && second == null;

        return first == second ||
               first.Type == second.Type;
    }

    private static ActionPlanValidationResult Invalid(
        ActionPlanValidationCode code,
        string reason)
    {
        return ActionPlanValidationResult.Invalid(
            code,
            reason);
    }
}
