using System.Collections.Generic;
using UnityEngine;

public abstract class StatusEffect
{
    //--------------------------------
    // 기본 정보
    //--------------------------------

    public string Name { get; protected set; }

    public int Stack { get; protected set; }

    public int Duration { get; protected set; }

    //--------------------------------
    // 적용 대상
    //--------------------------------

    protected Character owner;
    protected Character source;

    // null이면 캐릭터 디버프
    // null이 아니면 부위 디버프
    protected BodyPart ownerPart;

    //--------------------------------

    public Character Owner => owner;
    public Character Source => source;
    public BodyPart OwnerPart => ownerPart;

    public bool IsPartEffect => ownerPart != null;
    public bool IsCharacterEffect => ownerPart == null;

    //--------------------------------

    public virtual void Initialize(
        Character owner,
        Character source,
        BodyPart ownerPart = null)
    {
        this.owner = owner;
        this.source = source;
        this.ownerPart = ownerPart;
    }

    //--------------------------------
    // Life Cycle
    //--------------------------------

    public virtual void OnApply() { }

    public virtual void OnTurnStart() { }

    public virtual void OnTurnEnd() { }

    public virtual void OnRemove() { }

    public virtual void Merge(StatusEffect other) { }

    //--------------------------------
    // Roll
    //--------------------------------

    public virtual int ModifyRoll(
        BattleAction action,
        int roll)
    {
        return roll;
    }

    //--------------------------------
    // Damage
    //--------------------------------

    public virtual int ModifyDamage(
        BattleAction action,
        int damage)
    {
        return damage;
    }

    public virtual float ModifyDamageTaken(
        BattleAction action,
        float damage)
    {
        return damage;
    }

    //--------------------------------
    // Speed
    //--------------------------------

    public virtual int ModifySpeed(
        BodyPart part,
        int speed)
    {
        return speed;
    }

    //--------------------------------
    // Skill
    //--------------------------------

    public virtual bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        return true;
    }

    public virtual bool CanAct()
    {
        return true;
    }

    //--------------------------------
    // Utility
    //--------------------------------

    protected bool IsMyPart(BodyPart part)
    {
        return ownerPart != null &&
               part == ownerPart;
    }

    protected bool IsMyAction(BattleAction action)
    {
        return action != null &&
               ownerPart != null &&
               action.OwnerPart == ownerPart;
    }

    protected BodyPart GetRandomAlivePart()
    {
        if (owner == null)
            return null;

        List<BodyPart> candidates = new();

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (!part.IsBroken && part.PartHP > 0)
                candidates.Add(part);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[
            Random.Range(0, candidates.Count)];
    }

    //--------------------------------
    // Duration
    //--------------------------------

    public void DecreaseDuration()
    {
        if (Duration > 0)
            Duration--;
    }

    public bool IsExpired()
    {
        return Duration == 0;
    }

    //--------------------------------
    // Remove
    //--------------------------------

    protected void RemoveStatus()
    {
        if (ownerPart != null)
        {
            ownerPart.RemoveStatus(this);
            return;
        }

        if (owner != null)
        {
            owner.RemoveStatus(this);
        }
    }
}