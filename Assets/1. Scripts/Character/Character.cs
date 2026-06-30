using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class Character : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData data;
    public CharacterData Data => data;

    public int CurrentHP => RuntimeStatus.currentHP;

    //--------------------------------
    // Body Parts
    //--------------------------------

    public abstract IReadOnlyList<BodyPart> BodyParts { get; }
    public List<ActionSlot> ActionSlots;

    //--------------------------------
    // Status
    //--------------------------------

    public CurrentStatus CurrentStatus { get; protected set; }

    public RuntimeStatus RuntimeStatus { get; protected set; }

    protected Passive passive;

    public bool IsDead { get; protected set; }

    //--------------------------------
    // 캐릭터 디버프
    //--------------------------------

    protected readonly List<StatusEffect> statusEffects = new();

    public IReadOnlyList<StatusEffect> StatusEffects => statusEffects;

    //--------------------------------

    protected BattleEvent battleEvent;

    //------------------------------------------------

    public virtual void Initialize(BattleEvent battleEvent)
    {
        this.battleEvent = battleEvent;

        RecalculateStatus();

        RuntimeStatus = new RuntimeStatus(CurrentStatus);

        RuntimeStatus.currentHP = BodyParts.Sum(x => (int)x.PartHP);
    }

    //------------------------------------------------

    public virtual void RecalculateStatus()
    {
        CurrentStatus = new CurrentStatus(Data);

        // TODO
        // 장비
        // 패시브
        // 영구버프
    }

    //------------------------------------------------

    public virtual void TurnStart()
    {
    }

    public virtual void TurnEnd()
    {
    }

    //------------------------------------------------
    // Damage
    //------------------------------------------------

    public virtual void TakeDamage(
        BodyPart part,
        int damage,
        bool forceBreak = false)
    {
        if (IsDead)
            return;

        // 1. 부위 처리
        float beforeHP = part.PartHP;
        part.Damage(damage);

        // 2. Disabled 진입
        if (beforeHP > 0 && part.IsDisabled)
        {
            OnBodyPartDisabled(part);
        }

        // 3. Break 처리 (Disabled 이후 체크)
        if (forceBreak || part.PartHP <= 0)
        {
            BreakPart(part);
        }

        // 4. HP 재계산 (단 하나의 진실)
        RecalculateRuntimeHP();

        // 5. 사망 판정
        if (CanDie())
            Die();

        battleEvent?.RaiseDamageTaken(this, damage);
    }
    
    private void RecalculateRuntimeHP()
    {
        RuntimeStatus.currentHP =
            BodyParts.Sum(p => p.PartHP <= 0 ? 0 : (int)p.PartHP);
    }
    
    public void ForceRecalculateHP()
    {
        RuntimeStatus.currentHP = (int)BodyParts.Sum(p => p.PartHP);
    }

    //------------------------------------------------

    public virtual void TakeTrueDamage(
        int damage,
        StatusEffect sourceEffect)
    {
        if (IsDead)
            return;

        RuntimeStatus.currentHP =
            Mathf.Max(RuntimeStatus.currentHP - damage, 0);

        if (CanDie())
            Die();
    }

    //------------------------------------------------

    protected virtual bool CanDie()
    {
        return RuntimeStatus.currentHP <= 0;
    }

    //------------------------------------------------

    public virtual void Die()
    {
        if (IsDead)
            return;

        IsDead = true;

        battleEvent?.RaiseCharacterDeath(this);
    }

    //------------------------------------------------
    // Disabled
    //------------------------------------------------

    protected virtual void OnBodyPartDisabled(BodyPart part)
    {
        StatusEffect effect = CreateDisabledDebuff(part);

        if (effect == null)
            return;

        part.AddStatus(effect, this);
    }

    protected virtual StatusEffect CreateDisabledDebuff(
        BodyPart part)
    {
        return null;
    }

    //------------------------------------------------
    // Break
    //------------------------------------------------

    public void BreakPart(BodyPart part)
    {
        if (part.IsBroken)
            return;

        part.Break();

        //--------------------------------
        // 부위 디버프 → 캐릭터 디버프로 전이
        //--------------------------------

        foreach (var effect in part.StatusEffects.ToArray())
        {
            part.RemoveStatus(effect);

            OnBodyPartBroken(part, effect);
        }

        battleEvent?.RaiseBodyPartDestroyed(this, part);
    }

    //------------------------------------------------

    protected virtual void OnBodyPartBroken(
        BodyPart part,
        StatusEffect disabledDebuff)
    {
        // 기본 구현 :
        // Disabled 디버프를 캐릭터 디버프로 승격

        if (disabledDebuff != null)
        {
            AddStatus(disabledDebuff, this);
        }
    }

    //------------------------------------------------
    // Character Status
    //------------------------------------------------

    public void AddStatus(
        StatusEffect effect,
        Character source)
    {
        if (effect == null)
            return;

        effect.Initialize(this, source);

        effect.OnApply();

        statusEffects.Add(effect);

        battleEvent?.RaiseStatusApplied(this, effect);
    }

    public void RemoveStatus(StatusEffect effect)
    {
        if (effect == null)
            return;

        effect.OnRemove();

        statusEffects.Remove(effect);

        battleEvent?.RaiseStatusRemoved(this, effect);
    }

    //------------------------------------------------

    public void AddPrestige(int amount)
    {
        RuntimeStatus.currentPrestige =
            Mathf.Min(
                RuntimeStatus.currentPrestige + amount,
                CurrentStatus.maxPrestige);
    }

    //------------------------------------------------

    public int ModifyRoll(
        BattleAction action,
        int roll)
    {
        int value = roll;

        //--------------------------------
        // 캐릭터 디버프
        //--------------------------------

        foreach (var effect in statusEffects)
        {
            value = effect.ModifyRoll(action, value);
        }

        //--------------------------------
        // 현재 사용 부위 디버프
        //--------------------------------

        foreach (var effect in action.OwnerPart.StatusEffects)
        {
            value = effect.ModifyRoll(action, value);
        }

        return value;
    }
}