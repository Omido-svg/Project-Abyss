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

    // 이번 턴 속도
    public int Speed;

    // 합에서 사용할 위력
    public float Power;

    // 최종 데미지
    public float Damage;
    
    public ActionPhase Phase;

    public ActionType ActionType => Skill.ActionType;

    public void CalculateSpeed()
    {
        Speed = OwnerPart.CurrentSpeed;
    }

    public bool IsNormalAttack =>
        ActionType == ActionType.NormalAttack;

    public bool IsDuel =>
        ActionType == ActionType.Duel;

    public bool IsPreparation =>
        ActionType == ActionType.Preparation;

    public bool IsPrestige =>
        ActionType == ActionType.Prestige;
        
    // 위력 계산
    public int RollPower()
    {
        return Skill.BasePower + Skill.Resolver.Roll();
    }
}