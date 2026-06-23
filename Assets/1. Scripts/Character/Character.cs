using System.Collections.Generic;
using UnityEngine;

public abstract class Character : MonoBehaviour
{
    public string CharacterName { get; protected set; }
    public BaseStatus BaseStatus { get; protected set; }
    public RuntimeStatus RuntimeStatus { get; protected set; }
    public List<Skill> SkillPool { get; protected set; } = new();
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
    public virtual void TakeDamage(
        BodyPart part,
        int damage)
    {
        RuntimeStatus.currentHP -= (int)(damage * 0.3);

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