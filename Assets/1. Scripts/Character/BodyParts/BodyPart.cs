using System.Collections.Generic;
using UnityEngine;

public enum PartType
{
    HEAD,
    LEFT_HAND,
    RIGHT_HAND,
    LEGS
}

public enum BodyPartState
{
    Normal,
    Weakened,
    Broken
}

public class BodyPart
{
    public PartType Type { get; private set; }

    public float MaxPartHP { get; private set; }
    public float PartHP { get; set; }

    public BodyPartState State { get; set; } = BodyPartState.Normal;

    public Character Owner { get; private set; }

    private readonly List<Skill> availableSkills = new();
    public IReadOnlyList<Skill> AvailableSkills => availableSkills;

    public Skill CurrentSkill { get; set; }

    private readonly List<StatusEffect> statusEffects = new();
    public IReadOnlyList<StatusEffect> StatusEffects => statusEffects;

    public bool IsBroken => State == BodyPartState.Broken;
    public bool IsWeakened => State == BodyPartState.Weakened;
    public bool IsUsable => State != BodyPartState.Broken;

    //------------------------------------------------
    // 생성자
    //------------------------------------------------

    public BodyPart(
        PartType type,
        float maxPartHP,
        Skill[] skills)
    {
        Type = type;

        MaxPartHP = maxPartHP;
        PartHP = maxPartHP;

        State = BodyPartState.Normal;

        if (skills != null)
        {
            availableSkills.AddRange(skills);
        }

        if (availableSkills.Count > 0)
        {
            CurrentSkill = availableSkills[0];
        }
    }

    //------------------------------------------------
    // 초기화
    //------------------------------------------------

    public void Initialize(Character owner)
    {
        Owner = owner;

        PartHP = MaxPartHP;
        State = BodyPartState.Normal;

        statusEffects.Clear();

        if (availableSkills.Count > 0 && CurrentSkill == null)
        {
            CurrentSkill = availableSkills[0];
        }
    }

    //------------------------------------------------
    // 약화
    //------------------------------------------------

    public void Weaken()
    {
        if (State == BodyPartState.Broken)
            return;

        State = BodyPartState.Weakened;
        PartHP = 1f;

        Debug.Log($"{OwnerName()}의 {Type} 부위 약화");
    }

    //------------------------------------------------
    // 파괴
    //------------------------------------------------

    public void Break()
    {
        if (State == BodyPartState.Broken)
            return;

        State = BodyPartState.Broken;
        PartHP = 0f;

        Debug.Log($"{OwnerName()}의 {Type} 부위 파괴");
    }

    //------------------------------------------------
    // 회복
    //------------------------------------------------

    public void Recover()
    {
        State = BodyPartState.Normal;
        PartHP = MaxPartHP;

        ClearStatusEffects();

        Debug.Log($"{OwnerName()}의 {Type} 부위 회복");
    }

    //------------------------------------------------
    // 부위 상태이상
    //------------------------------------------------

    public void AddStatus(
        StatusEffect effect,
        Character source)
    {
        if (effect == null)
            return;

        if (Owner == null)
        {
            Debug.LogWarning($"{Type} 부위에 Owner가 없습니다.");
            return;
        }

        foreach (StatusEffect existing in statusEffects)
        {
            if (existing.GetType() == effect.GetType())
            {
                existing.Merge(effect);
                return;
            }
        }

        effect.Initialize(Owner, source, this);

        effect.OnApply();

        statusEffects.Add(effect);
    }

    public void RemoveStatus(StatusEffect effect)
    {
        if (effect == null)
            return;

        if (!statusEffects.Contains(effect))
            return;

        effect.OnRemove();

        statusEffects.Remove(effect);
    }

    public void ClearStatusEffects()
    {
        foreach (StatusEffect effect in statusEffects.ToArray())
        {
            RemoveStatus(effect);
        }
    }

    //------------------------------------------------

    private string OwnerName()
    {
        if (Owner == null)
            return "NULL";

        if (Owner.Data == null)
            return Owner.name;

        return Owner.Data.CharacterName;
    }
    
    public void SetDebugState(
        float currentHP,
        float maxHP,
        bool isWeakened,
        bool isBroken)
    {
        MaxPartHP = Mathf.Max(1f, maxHP);
        PartHP = Mathf.Clamp(currentHP, 0f, MaxPartHP);

        if (isBroken || PartHP <= 0f)
        {
            State = BodyPartState.Broken;
            PartHP = 0f;
            return;
        }

        if (isWeakened)
        {
            State = BodyPartState.Weakened;

            if (PartHP <= 0f)
                PartHP = 1f;

            return;
        }

        State = BodyPartState.Normal;
    }
    
    public void ReplaceSkills(
        IEnumerable<Skill> newSkills)
    {
        availableSkills.Clear();

        if (newSkills != null)
        {
            foreach (Skill skill in newSkills)
            {
                if (skill == null)
                    continue;

                availableSkills.Add(skill);
            }
        }

        if (availableSkills.Count > 0)
        {
            CurrentSkill = availableSkills[0];
        }
        else
        {
            CurrentSkill = null;
        }
    }
    
    public void IncreaseMaxHPPercent(
        float percent,
        bool healByIncreaseAmount = true)
    {
        if (percent <= 0f)
            return;

        float oldMaxHP = MaxPartHP;

        float increase =
            oldMaxHP * percent;

        MaxPartHP =
            Mathf.Max(1f, oldMaxHP + increase);

        if (healByIncreaseAmount)
        {
            PartHP =
                Mathf.Clamp(
                    PartHP + increase,
                    0f,
                    MaxPartHP);
        }
        else
        {
            PartHP =
                Mathf.Clamp(
                    PartHP,
                    0f,
                    MaxPartHP);
        }
    }
}