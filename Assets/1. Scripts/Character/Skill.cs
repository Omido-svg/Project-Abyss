public enum SkillType
{
    NORMALATTACK,
    DUEL,
    AMBUSH,
    PRESTIGE
}

public abstract class Skill
{
    public string SkillName { get; protected set; }

    public SkillType SkillType { get; protected set; }

    public int BasePower { get; protected set; }

    public RandomResolver Resolver { get; protected set; }
    protected Character owner;

    //--------------------------------

    // 합 위력 계산
    public virtual int Roll()
    {
        return BasePower + Resolver.Roll();
    }

    //--------------------------------
    
    protected BattleEvent battleEvent;
    public virtual void Initialize(Character owner, BattleEvent battleEvent)
    {
        this.owner = owner;
        this.battleEvent = battleEvent;
    }
    public virtual void Register() { }
    public virtual void Unregister() { }


    // 출혈 부여, 버프, 부위 파괴, 광기 증가 같은 부가 효과 처리스킬 효과
    public abstract void Execute(BattleAction action);
}