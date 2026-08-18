using System.Collections.Generic;

/// <summary>
/// 캐릭터 고유 메커닉이 Planning presentation에 concrete type 노출 없이
/// 선택 제약/편집 예외/슬롯 옵션을 제공하기 위한 Core 계약.
/// </summary>
public interface IActionPlanningRule
{
    string GetSkillSelectionBlockReason(
        ActionPlanningSkillContext context);

    bool IsEnergyReservationExempt(
        ActionPlanningSkillContext context);

    void ConfigurePlannedSlot(
        ActionPlanningSkillContext context,
        ActionSlot slot);

    void RestorePlanningState(
        ActionSlot slot);
}

public readonly struct ActionPlanningSkillContext
{
    public Character Owner { get; }
    public BodyPart Part { get; }
    public Skill Skill { get; }
    public int ActionIndex { get; }
    public IReadOnlyList<ActionSlot> PlannedSlots { get; }

    public ActionPlanningSkillContext(
        Character owner,
        BodyPart part,
        Skill skill,
        int actionIndex,
        IReadOnlyList<ActionSlot> plannedSlots)
    {
        Owner = owner;
        Part = part;
        Skill = skill;
        ActionIndex = actionIndex;
        PlannedSlots = plannedSlots;
    }
}
