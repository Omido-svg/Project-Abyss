public enum PartsType
{
    HANDS,
    Body,
    LEGS,
    HEAD
}

public class BodyPart
{
    public PartsType type;
    public int BaseSpeed;
    public int CurrentSpeed;
    public bool IsBroken;
    public float PartMaxHP;
    public float PartHP;
    public Skill CurrentSkill;
    
    public BodyPart(PartsType type, int speed, float currentHP)
    {
        this.type = type;
        this.BaseSpeed = speed;
        CurrentSpeed = BaseSpeed;
        IsBroken = false;
        PartMaxHP = PartHP = currentHP;
    }
    
    public void Recover()
    {
        IsBroken = false;
        PartHP = PartMaxHP;
    }
}
