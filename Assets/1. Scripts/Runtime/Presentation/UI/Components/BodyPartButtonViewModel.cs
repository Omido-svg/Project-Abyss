public struct BodyPartButtonViewModel
{
    public string PartText;
    public string HpText;
    public string SpeedText;
    public string SlotText;
    public string SkillText;
    public System.Collections.Generic.IReadOnlyList<Skill> AssignedSkills;

    public bool Interactable;

    public bool IsOwnerSelected;
    public bool IsTargetSelected;
    public bool IsWeakened;
    public bool IsBroken;
    public bool IsCharacterTarget;
    public bool HasHpOverride;

    public int ActionCount;
    public int MaxActionSlots;
}