using UnityEngine;

public abstract class CombatMechanic
{
    protected Character owner;
    protected BattleContext battleContext;
    protected BattleEvent battleEvent;

    public virtual string MechanicName => GetType().Name;

    public bool IsRegistered { get; private set; }

    public virtual void Initialize(
        Character owner,
        BattleContext battleContext)
    {
        this.owner = owner;
        this.battleContext = battleContext;

        if (battleContext != null)
            this.battleEvent = battleContext._battleEvent;
    }

    public void Register()
    {
        if (IsRegistered)
            return;

        if (owner == null)
        {
            Debug.LogWarning($"{MechanicName} Register 실패 : owner가 null입니다.");
            return;
        }

        if (battleContext == null)
        {
            Debug.LogWarning($"{MechanicName} Register 실패 : battleContext가 null입니다.");
            return;
        }

        OnRegister();

        IsRegistered = true;
    }

    public void Unregister()
    {
        if (!IsRegistered)
            return;

        OnUnregister();

        IsRegistered = false;
    }

    public virtual void OnRegister() { }

    public virtual void OnUnregister() { }

    public virtual int ModifyRoll(
        BattleAction action,
        int roll)
    {
        return roll;
    }

    public virtual int ModifyDamageDealt(
        DamageContext context,
        int damage)
    {
        return damage;
    }

    public virtual int ModifyDamageTaken(
        DamageContext context,
        int damage)
    {
        return damage;
    }

    public virtual int ModifyMomentumShift(
        ClashResultContext context,
        int shift)
    {
        return shift;
    }

    public virtual int ModifyPrestigeGain(
        ClashResultContext context,
        int prestigeGain)
    {
        return prestigeGain;
    }

    public virtual void ModifyActionSlotPolicy(
        ActionSlotPolicyContext context)
    {
    }

    public virtual bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        return true;
    }
    
    public virtual bool CanOwnerDie()
    {
        return true;
    }
}