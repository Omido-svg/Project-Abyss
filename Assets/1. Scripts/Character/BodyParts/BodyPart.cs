using System.Collections.Generic;
using UnityEngine;

public enum PartType
{
    HEAD,
    LEFT_HAND,
    RIGHT_HAND,
    LEGS
}

public class BodyPart
{
    //--------------------------------
    // Owner
    //--------------------------------

    public Character Owner { get; private set; }

    public void Initialize(Character owner)
    {
        Owner = owner;
    }

    //--------------------------------
    // 기본 정보
    //--------------------------------

    public PartType Type;

    public int CurrentSpeed;

    //--------------------------------
    // 체력
    //--------------------------------

    public float PartMaxHP;
    public float PartHP;

    //--------------------------------
    // 상태
    //--------------------------------

    public bool IsBroken { get; private set; }

    public bool IsDisabled => PartHP <= 0 && !IsBroken;

    //--------------------------------
    // 스킬
    //--------------------------------

    public Skill CurrentSkill;

    public List<Skill> AvailableSkills { get; }

    //--------------------------------
    // 부위 상태이상
    //--------------------------------

    public List<StatusEffect> StatusEffects { get; } = new();

    //--------------------------------

    public BodyPart(
        PartType type,
        float hp,
        IEnumerable<Skill> availableSkills = null)
    {
        Type = type;

        PartMaxHP = hp;
        PartHP = hp;

        CurrentSpeed = 0;

        IsBroken = false;

        AvailableSkills = availableSkills != null
            ? new List<Skill>(availableSkills)
            : new List<Skill>();
    }

    //--------------------------------
    // 상태이상
    //--------------------------------

    public void AddStatus(
        StatusEffect effect,
        Character source)
    {
        if (effect == null)
            return;

        effect.Initialize(Owner, source);

        effect.SetOwnerPart(this);

        effect.OnApply();

        StatusEffects.Add(effect);
    }

    public void RemoveStatus(StatusEffect effect)
    {
        if (effect == null)
            return;

        effect.OnRemove();

        StatusEffects.Remove(effect);
    }

    public void ClearStatus()
    {
        foreach (var effect in StatusEffects)
            effect.OnRemove();

        StatusEffects.Clear();
    }

    //--------------------------------
    // HP
    //--------------------------------

    public void Heal(float amount)
    {
        if (IsBroken)
            return;

        PartHP = Mathf.Min(PartHP + amount, PartMaxHP);
    }

    public void Damage(float amount)
    {
        if (IsBroken)
            return;

        PartHP = Mathf.Max(0, PartHP - amount);
    }

    //--------------------------------
    // 파괴
    //--------------------------------

    public void Break()
    {
        if (IsBroken)
            return;

        IsBroken = true;
        PartHP = 0;
        CurrentSkill = null;
    }

    //--------------------------------
    // 복구
    //--------------------------------

    public void Recover(float amount)
    {
        if (IsBroken)
            return;

        PartHP = Mathf.Min(PartHP + amount, PartMaxHP);
    }

    //--------------------------------
    // 스킬
    //--------------------------------

    public bool CanUseSkill(Skill skill)
    {
        if (IsBroken)
            return false;

        return AvailableSkills.Contains(skill);
    }

    public void AddSkill(Skill skill)
    {
        if (skill == null)
            return;

        if (!AvailableSkills.Contains(skill))
            AvailableSkills.Add(skill);
    }

    public void RemoveSkill(Skill skill)
    {
        AvailableSkills.Remove(skill);

        if (CurrentSkill == skill)
            CurrentSkill = null;
    }
}