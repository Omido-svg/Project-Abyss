public class ActionSlot
{
    public long ActionId;
    public Character Owner;
    public BodyPart Part;
    public Skill Skill;
    public int Speed;
    public int ActionIndex;
    public string SlotId;
    public CharacterSlotConfig SlotConfig;
    public ActionPhase Phase;
    public Character TargetCharacter;
    public BodyPart TargetPart;
    public BodyPart SecondaryTargetPart;
    public ActionSlot TargetSlot;

    // 유진 결투 스킬처럼 캐릭터 고유 재굴림 자원을 자동 사용한다.
    public bool UseCharacterRerollResource;

    // 캐릭터 고유 2단계 Planning 선택 (예: 유진 환형의 목표 무기).
    public string PlanningChoiceId;
    public bool PlanningEffectCommitted;

    // 0915 planning contract: 비용은 계획 시 실제 commit한다.
    public bool ResourceCostCommitted;

    // 전투 중 슬롯 소실(C-04)과 사용자의 Planning 취소를 구분하기 위한
    // 정확한 commit 금액. Runtime 제거는 이 값을 환불하지 않고,
    // PlanningActionCancellationService만 명시적 취소에서 환불한다.
    public int CommittedEnergyCost;

    // Planning에서 즉시 실행된 도사림의 역연산 journal.
    // AutoPlan preview ActionSlot은 undo를 사용하지 않으므로 실제 접근 시에만 생성한다.
    // 일반 Planning에서 PlanningUndo를 사용하는 기존 호출 의미는 그대로 유지된다.
    private PlanningUndoJournal planningUndo;

    public PlanningUndoJournal PlanningUndo =>
        planningUndo ??=
            new PlanningUndoJournal();

    /// <summary>
    /// Preview/AutoPlan scratch 슬롯이 journal을 생성하지 않고
    /// 이미 존재하는 journal만 초기화할 수 있게 한다.
    /// </summary>
    public void ClearPlanningUndoIfCreated()
    {
        planningUndo?.Clear();
    }

    // 도사림/위세처럼 계획 단계에서 이미 실행한 행동은 Resolution queue에 다시 넣지 않는다.
    public bool SkipResolution;

    // 「일기토」 같은 룰브레이커가 같은 TargetSlot을 향한 다른 슬롯의
    // 일방타격만 포기시키기 위한 런타임 표식. 합 자체는 건드리지 않는다.
    public bool SuppressOneSidedResolution;

    public bool HasActionId => ActionId > 0;

    public bool AllowsSkill(Skill skill)
    {
        if (skill == null) return false;
        if (SlotConfig != null && !SlotConfig.Allows(skill.ActionType)) return false;
        if (SlotConfig?.HasLinkedPart == true && Part?.IsBroken == true) return false;
        return true;
    }

    public bool HasSameKey(ActionSlot other)
    {
        if (other == null || Owner != other.Owner)
            return false;

        if (!string.IsNullOrWhiteSpace(SlotId) &&
            !string.IsNullOrWhiteSpace(other.SlotId))
        {
            return string.Equals(
                SlotId,
                other.SlotId,
                System.StringComparison.Ordinal);
        }

        return HasSameKey(
            other.Owner,
            other.Part,
            other.ActionIndex);
    }

    public bool HasSameKey(Character owner, BodyPart part, int actionIndex) =>
        Owner == owner && Part == part && ActionIndex == actionIndex;
}

public class ActionSlotPolicyContext
{
    public Character Owner;
    public BodyPart Part;
    public int MaxSlots = 1;
}
