public class ActionSlot
{
    // ActionManager가 최초 등록할 때 부여한다.
    // 전투 전체에서 가능한 한 재사용하지 않는 런타임 식별자다.
    public long ActionId;

    public Character Owner;
    public BodyPart Part;

    public Skill Skill;

    public int Speed;

    // 같은 Owner + Part에서 몇 번째 행동인지 나타낸다.
    // 기본 행동은 0, 추가 행동은 1 이상을 사용한다.
    public int ActionIndex;

    public ActionPhase Phase;

    public Character TargetCharacter;
    public BodyPart TargetPart;

    // ClashBuilder가 실행 계획을 만들 때 설정한다.
    public ActionSlot TargetSlot;

    public bool HasActionId => ActionId > 0;

    public bool HasSameKey(ActionSlot other)
    {
        if (other == null)
            return false;

        return HasSameKey(
            other.Owner,
            other.Part,
            other.ActionIndex);
    }

    public bool HasSameKey(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        return
            Owner == owner &&
            Part == part &&
            ActionIndex == actionIndex;
    }
}

public class ActionSlotPolicyContext
{
    public Character Owner;
    public BodyPart Part;

    public int MaxSlots = 1;
}
