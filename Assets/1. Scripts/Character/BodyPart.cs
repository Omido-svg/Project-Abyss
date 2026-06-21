public enum PartsType
{
    HAND,
    Body,
    LEG,
    HEAD
}

public enum BodyPartState
{
    Alive,
    Broken
}

public class BodyPart
{
    public PartsType type;
    public BodyPartState bodyState;
    public int BaseSpeed;
    public int CurrentSpeed;
    public bool IsBroken;
    public float HP;
    public Skill SelectedSkill;
    public Character SelectedTarget;
    public BodyPart SelectedTargetPart;
}