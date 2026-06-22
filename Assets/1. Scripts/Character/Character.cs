using System.Collections.Generic;
using UnityEngine;

public abstract class Character : MonoBehaviour
{
    public string CharacterName { get; protected set; }
    public BaseStatus BaseStatus { get; protected set; }
    public RuntimeStatus RuntimeStatus { get; protected set; }
    public SkillSet SkillSet { get; protected set; }
    protected Passive passive;
    public bool IsDead { get; set; }
    public abstract IReadOnlyList<BodyPart> BodyParts { get; }
    protected List<StatusEffect> statusEffects = new();
    protected BattleEvent battleEvent;

    

    //--------------------------------
    
    public virtual void Initialize(BattleEvent battleEvent)
    {
        this.battleEvent = battleEvent;
    }

    public virtual void TurnStart() { }
    public virtual void TurnEnd() { }
    public virtual void TakeDamage(int damage)
    {
        RuntimeStatus.currentHP -= damage;

        battleEvent?.RaiseDamageTaken(this, damage);

        if (RuntimeStatus.currentHP <= 0)
            Die();
    }
    
    public virtual void Heal(int amount)
    {
        RuntimeStatus.currentHP += amount;

        RuntimeStatus.currentHP =
            Mathf.Min(
                RuntimeStatus.currentHP,
                BaseStatus.maxHP);
    }
    
    public virtual void UseSkill(Skill skill, Character target) { }
    public virtual void Die()
    {
        IsDead = true;
        battleEvent?.RaiseCharacterDeath(this);
    }
    
    public virtual bool CanAct()
    {
        return true;
    }

    public IReadOnlyList<StatusEffect> StatusEffects => statusEffects;

    public void AddStatus(
        StatusEffect effect,
        Character source)
    {
        effect.Initialize(this, source);

        effect.OnApply();

        statusEffects.Add(effect);

        battleEvent?.RaiseStatusApplied(this, effect);
    }
    
    public void RemoveStatus(StatusEffect effect)
    {
        statusEffects.Remove(effect);

        battleEvent?.RaiseStatusRemoved(this, effect);
    }
    
    public void RecoverBrokenParts(int count)
    {
        foreach (BodyPart part in BodyParts)
        {
            if (!part.IsBroken)
                continue;

            part.Recover();

            count--;

            if (count == 0)
                break;
        }
    }
    
    
}