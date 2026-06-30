public enum ActionType
{
    NormalAttack,   // 일반 공격
    Duel,           // 결투 (합)
    Preparation,    // 도사림
    Prestige        // 위세 
}

public class BattleAction
{
    public ActionSlot Slot;

    public Character Owner => Slot.Owner;
    public Character Target => Slot.TargetCharacter;

    public BodyPart OwnerPart => Slot.Part;
    public BodyPart TargetPart => Slot.TargetPart;

    public Skill Skill => Slot.Skill;
    public int Speed => Slot.Speed;
    public ActionPhase Phase => Slot.Phase;
    public ActionType ActionType => Skill.ActionType;

    public int RolledPower;
    public int finalPower;
    public bool HasRolled;

    //--------------------------------
    // 로그용 데미지 결과
    //--------------------------------

    public bool HasDamageLog;
    public int LoggedDamage;
    public int LoggedBeforeHP;
    public int LoggedAfterHP;

    public int RollPower()
    {
        int roll = Skill.Resolver.Roll();
        roll = Owner.ModifyRoll(this, roll);
        return Skill.BasePower + roll;
    }

    public void SetDamageLog(
        int damage,
        int beforeHP,
        int afterHP)
    {
        HasDamageLog = true;
        LoggedDamage = damage;
        LoggedBeforeHP = beforeHP;
        LoggedAfterHP = afterHP;
    }
}