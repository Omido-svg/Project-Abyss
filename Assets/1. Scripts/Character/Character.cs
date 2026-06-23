using System.Collections.Generic;
using UnityEngine;

public abstract class Character : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData data;
    public CharacterData Data => data;

    //------------------------------------------------
    // Status
    //------------------------------------------------

    // 아이템, 증강, 패시브 등이 모두 적용된 현재 스탯
    public CurrentStatus CurrentStatus { get; protected set; }

    // 전투 중 계속 변하는 값
    public RuntimeStatus RuntimeStatus { get; protected set; }

    //------------------------------------------------

    public List<Skill> SkillPool { get; protected set; } = new();

    protected Passive passive;

    public bool IsDead { get; protected set; }

    public abstract IReadOnlyList<BodyPart> BodyParts { get; }

    protected readonly List<StatusEffect> statusEffects = new();

    public IReadOnlyList<StatusEffect> StatusEffects => statusEffects;

    protected BattleEvent battleEvent;

    //------------------------------------------------

    public virtual void Initialize(BattleEvent battleEvent)
    {
        this.battleEvent = battleEvent;

        RecalculateStatus();

        RuntimeStatus = new RuntimeStatus(CurrentStatus);
    }

    //------------------------------------------------
    // 스탯 재계산
    //------------------------------------------------

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

    //------------------------------------------------

    public virtual void TakeDamage(
        BodyPart part,
        int damage)
    {
        RuntimeStatus.currentHP -= Mathf.CeilToInt(damage * 0.3f);

        part.PartHP -= damage;

        if (part.PartHP <= 0 && !part.IsBroken)
        {
            part.IsBroken = true;

            battleEvent?.RaiseBodyPartDestroyed(this, part);
        }

        battleEvent?.RaiseDamageTaken(this, damage);

        if (RuntimeStatus.currentHP <= 0)
            Die();
    }

    //------------------------------------------------

    public virtual void Heal(int amount)
    {
        RuntimeStatus.currentHP += amount;

        RuntimeStatus.currentHP =
            Mathf.Min(
                RuntimeStatus.currentHP,
                CurrentStatus.maxHP);
    }

    //------------------------------------------------

    public virtual void UseSkill(
        Skill skill,
        Character target)
    {

    }

    //------------------------------------------------

    public virtual void Die()
    {
        IsDead = true;

        battleEvent?.RaiseCharacterDeath(this);
    }

    //------------------------------------------------

    public virtual bool CanAct()
    {
        return true;
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

    //------------------------------------------------

    public void RecoverBrokenParts(int count)
    {
        foreach (BodyPart part in BodyParts)
        {
            if (!part.IsBroken)
                continue;

            part.Recover();

            count--;

            if (count <= 0)
                break;
        }
    }

    //------------------------------------------------
    // 턴 시작 시 사용할 스킬 배정
    //------------------------------------------------

    public virtual void AssignSkills()
    {
        List<Skill> temp = new(SkillPool);

        Utils.Shuffle(temp);

        int index = 0;

        foreach (BodyPart part in BodyParts)
        {
            if (part.IsBroken)
            {
                part.CurrentSkill = null;
                continue;
            }

            if (index >= temp.Count)
            {
                part.CurrentSkill = null;
                continue;
            }

            part.CurrentSkill = temp[index];
            index++;
        }
    }
}