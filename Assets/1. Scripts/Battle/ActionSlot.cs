public class ActionSlot
{
    public Character Owner;
    public BodyPart Part;

    public Skill Skill;

    public int Speed;
    public int ActionIndex;
    public ActionPhase Phase;

    public Character TargetCharacter;
    public BodyPart TargetPart;

    // 합 판정용
    public ActionSlot TargetSlot;
}

public class ActionSlotPolicyContext
{
    public Character Owner;
    public BodyPart Part;

    public int MaxSlots = 1;
}