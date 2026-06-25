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

    public ActionType ActionType => Skill.ActionType;

    // 항상 현재 부위 속도를 반환
    public int Speed => OwnerPart.CurrentSpeed;

    public bool IsNormalAttack =>
        ActionType == ActionType.NormalAttack;

    public bool IsDuel =>
        ActionType == ActionType.Duel;

    public bool IsPreparation =>
        ActionType == ActionType.Preparation;

    public bool IsPrestige =>
        ActionType == ActionType.Prestige;

    public int RolledPower;

    public int RollPower()
    {
        int roll = Skill.Resolver.Roll();
        roll = Owner.ModifyRoll(this, roll);
        return Skill.BasePower + roll;
    }
}