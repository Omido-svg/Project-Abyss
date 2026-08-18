using UnityEngine;

public sealed class ActionPlanAssignmentRequest
{
    public Character Owner;
    public BodyPart OwnerPart;
    public Skill Skill;
    public int ActionIndex;

    public Character Target;
    public BodyPart TargetPart;
    public TargetSelectionRule TargetRule;
    public ActionSlot TargetSlot;
}

public sealed class ActionPlanAssignmentResult
{
    public bool Success;
    public string FailureReason;
    public ActionSlot PreviousSlot;
    public ActionSlot Slot;
}

/// <summary>
/// Planning ActionSlot의 생성/교체/재지정/삭제를 소유하는 command service.
/// UI는 입력 상태만 결정하고 ActionManager mutation 세부사항을 직접 수행하지 않는다.
/// </summary>
public sealed class BattleActionPlanCommandService
{
    private readonly ActionManager actionManager;
    private readonly SpeedManager speedManager;

    public BattleActionPlanCommandService(
        ActionManager actionManager,
        SpeedManager speedManager)
    {
        this.actionManager = actionManager;
        this.speedManager = speedManager;
    }

    public ActionPlanAssignmentResult TryAssign(
        ActionPlanAssignmentRequest request)
    {
        ActionPlanAssignmentResult result = new();

        if (actionManager == null || speedManager == null)
            return Fail(result, "Planning service가 준비되지 않았습니다.");

        if (request?.Owner == null ||
            request.OwnerPart == null ||
            request.Skill == null ||
            request.Target == null)
        {
            return Fail(result, "행동 주체/스킬/대상 정보가 부족합니다.");
        }

        if (request.ActionIndex < 0)
            return Fail(result, "ActionIndex가 올바르지 않습니다.");

        if (request.Owner.IsDead || request.Target.IsDead)
            return Fail(result, "사망한 캐릭터가 포함되어 있습니다.");

        if (request.OwnerPart.IsBroken)
            return Fail(result, "행동 부위가 파괴되어 있습니다.");

        bool isPreparation =
            request.Skill.ActionType == ActionType.Preparation;

        if (isPreparation)
        {
            if (request.TargetPart == null ||
                request.TargetPart.Owner != request.Target ||
                request.TargetPart.IsBroken ||
                request.Target != request.Owner)
            {
                return Fail(result, "도사림 자기 대상이 올바르지 않습니다.");
            }
        }
        else if (!BattleTargetValidator.IsValid(
                     request.Target,
                     request.TargetPart,
                     request.TargetRule))
        {
            return Fail(result, "대상 계약이 올바르지 않습니다.");
        }

        if (request.Skill.ActionType == ActionType.Prestige)
        {
            if (!IsPrestigeReady(request.Owner))
                return Fail(result, "위세 게이지가 부족합니다.");

            if (request.Skill.PrestigeUsePolicy ==
                    PrestigeUsePolicy.OncePerTurn &&
                HasPrestigeSlotSelected(
                    request.Owner,
                    request.OwnerPart,
                    request.ActionIndex))
            {
                return Fail(result, "이번 턴에 이미 위세 스킬을 선택했습니다.");
            }
        }

        result.PreviousSlot =
            actionManager.FindSlot(
                request.Owner,
                request.OwnerPart,
                request.ActionIndex);

        ActionSlot slot = new()
        {
            Owner = request.Owner,
            Part = request.OwnerPart,
            Skill = request.Skill,
            TargetCharacter = request.Target,
            TargetPart = request.TargetPart,
            Speed = speedManager.GetSpeed(
                request.Owner,
                request.OwnerPart),
            ActionIndex = request.ActionIndex,
            Phase = request.Skill.DefaultPhase,
            TargetSlot = request.TargetSlot
        };

        ActionPlanningSkillContext planningContext =
            new(
                request.Owner,
                request.OwnerPart,
                request.Skill,
                request.ActionIndex,
                actionManager.Slots);

        ActionPlanningMechanicPolicy.ConfigurePlannedSlot(
            planningContext,
            slot);

        if (!actionManager.TryAddOrReplaceSlot(slot))
        {
            return Fail(
                result,
                "에너지 예산 또는 슬롯 계약을 만족하지 못했습니다.");
        }

        result.Success = true;
        result.Slot = slot;
        return result;
    }

    public bool TryRetarget(
        ActionSlot sourceSlot,
        ActionSlot targetSlot,
        out ActionSlot liveSource)
    {
        liveSource = null;

        if (actionManager == null ||
            sourceSlot == null ||
            targetSlot?.Owner == null)
        {
            return false;
        }

        liveSource =
            actionManager.FindSlot(
                sourceSlot.Owner,
                sourceSlot.Part,
                sourceSlot.ActionIndex);

        if (liveSource == null ||
            liveSource.ActionId != sourceSlot.ActionId)
        {
            liveSource = null;
            return false;
        }

        liveSource.TargetCharacter = targetSlot.Owner;
        liveSource.TargetPart = targetSlot.Part;
        liveSource.TargetSlot = targetSlot;
        liveSource.SecondaryTargetPart = null;
        return true;
    }

    public bool Remove(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        return actionManager != null &&
               actionManager.RemoveSlot(
                   owner,
                   part,
                   actionIndex);
    }

    public void ResetOwner(Character owner)
    {
        if (owner != null)
            actionManager?.RemoveSlotsByOwner(owner);
    }

    public bool HasPrestigeSlotSelected(
        Character owner,
        BodyPart ignorePart = null,
        int ignoreActionIndex = -1)
    {
        if (owner == null || actionManager == null)
            return false;

        foreach (ActionSlot slot in actionManager.Slots)
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

    private static bool IsPrestigeReady(Character character)
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

    private static ActionPlanAssignmentResult Fail(
        ActionPlanAssignmentResult result,
        string reason)
    {
        result.Success = false;
        result.FailureReason = reason;
        return result;
    }
}
