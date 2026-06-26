using UnityEngine;

public abstract class Skill
{
    // 기본 정보
    public string SkillName { get; protected set; }

    // 평타 / 결투 / 도사림 / 위세
    public virtual ActionType ActionType { get; }
    
    // 위력
    // 스킬 기본 위력
    public int BasePower { get; protected set; }

    // 주사위 / 코인 / 슬롯머신 등
    public RandomResolver Resolver { get; protected set; }
    
    // 전투 옵션
    // 합 참여 여부
    public virtual bool CanClash => ActionType != ActionType.Preparation;

    // 합 승리 시 위세 획득 여부
    public virtual bool GainPrestige => ActionType == ActionType.Duel;

    // 방어도 무시 비율 (0~1)
    public virtual float IgnoreBlock => 0f;

    // UI 표시용
    public int MinPower => BasePower + Resolver.MinValue;
    public int MaxPower => BasePower + Resolver.MaxValue;

    //--------------------------------

    protected Character owner;
    protected BattleEvent battleEvent;

    //--------------------------------

    public virtual void Initialize(
        Character owner,
        BattleEvent battleEvent)
    {
        this.owner = owner;
        this.battleEvent = battleEvent;
    }

    public virtual void Register() { }

    public virtual void Unregister() { }
    

    // 합 위력 계산
    public virtual int RollPower()
    {
        return BasePower + Resolver.Roll();
    }

    // 스킬 효과
    public abstract void Execute(BattleAction action);
}