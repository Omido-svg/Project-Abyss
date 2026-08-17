/// <summary>
/// 월드 슬롯 드래그와 스킬 카드 드래그가 공유하는 단일 페이로드.
/// 카드→적 슬롯은 스킬을 배치하고, 아군 머리 위 슬롯→적 슬롯은 이미 배치된 합 대상을 재지정한다.
/// </summary>
public static class BattleSkillDragContext
{
    public static Skill Skill { get; private set; }
    public static int ActionIndex { get; private set; }
    public static BattleSkillCardButtonUI Source { get; private set; }
    public static ActionSlot PlannedSlot { get; private set; }

    public static bool HasSkillPayload => Skill != null;
    public static bool HasSlotPayload => PlannedSlot != null;
    public static bool HasPayload => HasSkillPayload || HasSlotPayload;

    public static void Begin(BattleSkillCardButtonUI source, Skill skill, int actionIndex)
    {
        Clear();
        Source = source;
        Skill = skill;
        ActionIndex = actionIndex;
    }

    public static void BeginSlot(ActionSlot slot)
    {
        Clear();
        PlannedSlot = slot;
        ActionIndex = slot?.ActionIndex ?? 0;
    }

    public static void Clear()
    {
        Source = null;
        Skill = null;
        PlannedSlot = null;
        ActionIndex = 0;
    }
}
