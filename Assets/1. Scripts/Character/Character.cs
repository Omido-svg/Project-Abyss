using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class Character : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData data;
    public CharacterData Data => data;
    public int CurrentHP => (int)BodyParts.Sum(x => x.PartHP);
    
    // 캐릭터 별로 부위를 다르게 설정
    public abstract IReadOnlyList<BodyPart> BodyParts { get; }

    // 아이템, 증강, 패시브 등이 모두 적용된 현재 스탯
    public CurrentStatus CurrentStatus { get; protected set; }

    // 전투 중 계속 변하는 값
    public RuntimeStatus RuntimeStatus { get; protected set; }

    public List<Skill> SkillPool { get; protected set; } = new();

    protected Passive passive;

    public bool IsDead { get; protected set; }

    protected readonly List<StatusEffect> statusEffects = new();

    public IReadOnlyList<StatusEffect> StatusEffects => statusEffects;

    protected BattleEvent battleEvent;

    //------------------------------------------------

    public virtual void Initialize(BattleEvent battleEvent)
    {
        this.battleEvent = battleEvent;

        // 1. 스탯 계산
        RecalculateStatus();

        // 2. Runtime 생성
        RuntimeStatus = new RuntimeStatus(CurrentStatus);

        // 3. 실제 현재 체력 설정
        RuntimeStatus.currentHP = CurrentHP;
    }

    // 스탯 재계산
    public virtual void RecalculateStatus()
    {
        // CharacterData 복사
        CurrentStatus = new CurrentStatus(Data);

        // TODO
        // 장비 적용
        // 증강 적용
        // 패시브 적용
        // 영구 버프 적용
    }

    //------------------------------------------------

    public virtual void TurnStart()
    {
        
    }

    public virtual void TurnEnd()
    {

    }

    public virtual void TakeDamage(
        BodyPart part,
        int damage,
        bool forceBreak = false)
    {
        bool hitBrokenPart = part.IsBroken;

        // 전체 체력 감소
        RuntimeStatus.currentHP -= damage;

        // 멀쩡한 부위였다면 부위 HP 감소
        if (!hitBrokenPart)
        {
            part.PartHP -= damage;
            part.PartHP = Mathf.Max(0, part.PartHP);

            // 부위 파괴
            if ((part.PartHP <= 0 || forceBreak) && !part.IsBroken)
            {
                ForceBreakPart(part);
            }
        }

        battleEvent?.RaiseDamageTaken(this, damage);

        if (RuntimeStatus.currentHP <= 0 || IsAllBodyPartsBroken())
        {
            Die();
        }
    }

    //------------------------------------------------
    
    public bool IsAllBodyPartsBroken()
    {
        foreach (var part in BodyParts)
        {
            if (!part.IsBroken)
                return false;
        }

        return true;
    }
    
    public virtual void Die()
    {
        IsDead = true;

        battleEvent?.RaiseCharacterDeath(this);
    }

    //------------------------------------------------

    public void AddStatus(
        StatusEffect effect,
        Character source)
    {
        effect.Initialize(this, source);

        effect.OnApply();

        statusEffects.Add(effect);

        battleEvent?.RaiseStatusApplied(this, effect);
    }

    //------------------------------------------------

    public void RemoveStatus(StatusEffect effect)
    {
        statusEffects.Remove(effect);

        battleEvent?.RaiseStatusRemoved(this, effect);
    }
    
    public void ForceBreakPart(BodyPart part)
    {
        if(part.IsBroken)
            return;

        part.PartHP = 0;
        part.IsBroken = true;

        battleEvent?.RaiseBodyPartDestroyed(this, part);
    }
    
    public void AddPrestige(int amount)
    {
        RuntimeStatus.currentPrestige =
            Mathf.Min(
                RuntimeStatus.currentPrestige + amount,
                CurrentStatus.maxPrestige);
    }
    
    public int ModifyRoll(BattleAction action, int roll)
    {
        int value = roll;

        foreach (StatusEffect effect in statusEffects)
        {
            value = effect.ModifyRoll(action, value);
        }

        return value;
    }
}