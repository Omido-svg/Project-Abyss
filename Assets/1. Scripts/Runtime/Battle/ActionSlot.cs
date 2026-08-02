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