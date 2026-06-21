using System.Collections.Generic;

public abstract class Character
{
    public BaseStatus BaseStatus { get; protected set; }
    public RuntimeStatus RuntimeStatus { get; protected set; }
    public SkillSet SkillSet { get; protected set; }
    public RandomResolver Resolver { get; protected set; }
    public bool IsDead { get; set; }
    public List<BodyPart> BodyParts { get; set; }
    protected List<StatusEffect> statusEffects = new();
    

    //--------------------------------

    public virtual void TurnStart() { }
    public virtual void TurnEnd() { }
    public virtual void TakeDamage(int damage) { }
    public virtual void Heal(int amount) { }
    public virtual void UseSkill(Skill skill, Character target) { }
    public virtual void Die() { }
    public virtual bool CanAct()
    {
        return true;
    }


    public List<StatusEffect> StatusEffects => statusEffects;

    public void AddStatus(StatusEffect effect)
    {
        statusEffects.Add(effect);
    }
    
    public void RemoveStatus(StatusEffect effect)
    {
        statusEffects.Remove(effect);
    }
}