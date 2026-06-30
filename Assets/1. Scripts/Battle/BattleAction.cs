public enum ActionType
{
    NormalAttack,   // 일반 공격
    Duel,           // 결투 (합)
    Preparation,    // 도사림
    Prestige        // 위세 
}

public class BattleAction
{
    public Character Owner;
    public Character Target;

    public BodyPart OwnerPart;
    public BodyPart TargetPart;

    public Skill Skill;

    public ActionPhase Phase;
    
    public ActionSlot Slot;

    public ActionType ActionType => Skill.ActionType;

    public int Speed;

    public bool IsNormalAttack =>
        ActionType == ActionType.NormalAttack;

    public bool IsDuel =>
        ActionType == ActionType.Duel;

    public bool IsPreparation =>
        ActionType == ActionType.Preparation;

    public bool IsPrestige =>
        ActionType == ActionType.Prestige;
        
    public int finalPower;
    
    public int RolledPower;
    
    public int RollPower()
    {
        int roll = Skill.Resolver.Roll();
        roll = Owner.ModifyRoll(this, roll);
        return Skill.BasePower + roll;
    }
}