using UnityEngine;

public enum PrestigeUsePolicy
{
    None,
    OncePerTurn,
    Unlimited
}

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
    // 초기화용 참조
    //--------------------------------

    protected Character owner;
    protected BattleEvent battleEvent;

    //--------------------------------
    // 실행 Phase
    //--------------------------------

    public virtual ActionPhase DefaultPhase
    {
        get
        {
            return ActionType switch
            {
                ActionType.Prestige => ActionPhase.PRETURN,
                ActionType.Preparation => ActionPhase.FORESIGHT,
                _ => ActionPhase.COMBAT
            };
        }
    }

    //--------------------------------
    // 전투 옵션
    //--------------------------------

    public virtual bool CanClash
    {
        get
        {
            return ActionType == ActionType.NormalAttack ||
                   ActionType == ActionType.Duel;
        }
    }

    // 기본적으로 결투 스킬만 합 승리 시 위세 획득
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
    // 위세 사용 정책
    //--------------------------------

    public virtual PrestigeUsePolicy PrestigeUsePolicy
    {
        get
        {
            if (this.ActionType == ActionType.Prestige)
                return PrestigeUsePolicy.OncePerTurn;

            return PrestigeUsePolicy.None;
        }
    }

    //--------------------------------
    // 자원 조건
    //--------------------------------

    public virtual bool CanUseByResource(Character owner)
    {
        if (owner == null)
            return false;

        // 위세 스킬이 아니면 별도 위세 자원 검사를 하지 않음
        if (this.ActionType != ActionType.Prestige)
            return true;

        if (owner.CurrentStatus == null ||
            owner.RuntimeStatus == null)
            return false;

        if (owner.CurrentStatus.maxPrestige <= 0)
            return false;

        return owner.RuntimeStatus.currentPrestige >=
               owner.CurrentStatus.maxPrestige;
    }
    
    public virtual void ConsumeResource(Character owner)
    {
        if (owner == null)
            return;

        if (this.ActionType != ActionType.Prestige)
            return;

        if (owner.RuntimeStatus == null)
            return;

        owner.RuntimeStatus.currentPrestige = 0;

        Debug.Log(
            $"{owner.Data.CharacterName} 위세 게이지 소모 : 0");
    }

    //--------------------------------
    // AI 사용 가능 여부
    //--------------------------------

    public virtual bool CanAIUse(
        Character owner,
        BodyPart part,
        BattleContext context)
    {
        return true;
    }

    //--------------------------------
    // 합 승리 보너스 확장 지점
    //--------------------------------

    public virtual int GetMomentumPushBonus(
        BattleAction action)
    {
        return 0;
    }

    public virtual int GetPrestigeGainBonus(
        BattleAction action)
    {
        return 0;
    }

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
    // 초기화
    //--------------------------------

    public virtual void Initialize(
        Character owner,
        BattleEvent battleEvent)
    {
        this.owner = owner;
        this.battleEvent = battleEvent;
    }

    public virtual void Register()
    {
    }

    public virtual void Unregister()
    {
    }

    //--------------------------------
    // 순수 스킬 굴림
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