using UnityEngine;

public abstract class Skill
{
    //--------------------------------
    // 기본 정보
    //--------------------------------

    public string SkillName { get; protected set; }

    // 평타 / 결투 / 도사림 / 위세
    public abstract ActionType ActionType { get; }

    //--------------------------------
    // 위력
    //--------------------------------

    public int BasePower { get; protected set; }

    // 주사위 / 코인 / 슬롯머신 등
    public RandomResolver Resolver { get; protected set; }

    //--------------------------------
    // 전투 옵션
    //--------------------------------

    // 기본적으로 평타/결투만 합 가능
    public virtual bool CanClash
    {
        get
        {
            return ActionType == ActionType.NormalAttack ||
                   ActionType == ActionType.Duel;
        }
    }

    // 결투 승리 시 위세 획득
    public virtual bool GainPrestige
    {
        get
        {
            return ActionType == ActionType.Duel;
        }
    }

    // 방어도 무시 비율
    public virtual float IgnoreBlock => 0f;

    //--------------------------------
    // UI 표시용
    //--------------------------------

    public int MinPower
    {
        get
        {
            if (Resolver == null)
                return BasePower;

            return BasePower + Resolver.MinValue;
        }
    }

    public int MaxPower
    {
        get
        {
            if (Resolver == null)
                return BasePower;

            return BasePower + Resolver.MaxValue;
        }
    }

    //--------------------------------

    protected Character owner;
    protected BattleEvent battleEvent;

    //--------------------------------
    // 초기화
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

    //--------------------------------
    // 순수 스킬 굴림
    //--------------------------------
    // 주의:
    // 이 함수는 상태이상 보정 없는 순수 굴림만 담당.
    // 실제 전투에서는 BattleAction.RollPower()를 사용하는 게 좋음.
    //--------------------------------

    public virtual int RollRawPower()
    {
        if (Resolver == null)
            return BasePower;

        return BasePower + Resolver.Roll();
    }

    //--------------------------------
    // 스킬 효과
    //--------------------------------

    public abstract void Execute(BattleAction action);
}