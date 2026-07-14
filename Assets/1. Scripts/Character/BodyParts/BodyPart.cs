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

    public BodyPartState State { get; set; } =
        BodyPartState.Normal;

    public Character Owner { get; private set; }

    public int Revision { get; private set; }

    private readonly List<Skill> availableSkills = new();
    public IReadOnlyList<Skill> AvailableSkills =>
        availableSkills;

    public Skill CurrentSkill { get; set; }

    private readonly List<StatusEffect> statusEffects = new();
    public IReadOnlyList<StatusEffect> StatusEffects =>
        statusEffects;

    public bool IsBroken =>
        State == BodyPartState.Broken;

    public bool IsWeakened =>
        State == BodyPartState.Weakened;

    public bool IsUsable =>
        State != BodyPartState.Broken;

    public BodyPart(
        PartType type,
        float maxPartHP,
        Skill[] skills)
    {
        Type = type;
        MaxPartHP = Mathf.Max(1f, maxPartHP);
        PartHP = MaxPartHP;
        State = BodyPartState.Normal;

        ReplaceSkills(skills);
    }

    public void Initialize(Character owner)
    {
        // 재사용되는 BodyPart라면 남아 있던 상태 객체의 정리 훅을 먼저 호출한다.
        ClearStatusEffects();

        Owner = owner;
        PartHP = MaxPartHP;
        State = BodyPartState.Normal;
        Revision++;

        if (availableSkills.Count > 0 &&
            CurrentSkill == null)
        {
            CurrentSkill = availableSkills[0];
        }
    }

    public int ApplyDamage(int damage)
    {
        if (damage <= 0 || State != BodyPartState.Normal)
            return 0;

        int before = Mathf.Max(
            0,
            Mathf.CeilToInt(PartHP));

        int applied = Mathf.Min(before, damage);
        PartHP = Mathf.Max(0f, PartHP - applied);

        if (applied > 0)
            Revision++;

        return applied;
    }

    public void Weaken()
    {
        if (State == BodyPartState.Broken ||
            State == BodyPartState.Weakened)
        {
            return;
        }

        State = BodyPartState.Weakened;
        PartHP = 0f;
        Revision++;

        Debug.Log(
            $"{OwnerName()}의 {Type} 부위 약화");
    }

    public void Break()
    {
        if (State == BodyPartState.Broken)
            return;

        State = BodyPartState.Broken;
        PartHP = 0f;
        Revision++;

        Debug.Log(
            $"{OwnerName()}의 {Type} 부위 파괴");
    }

    public void Recover()
    {
        State = BodyPartState.Normal;
        PartHP = MaxPartHP;
        Revision++;

        Debug.Log(
            $"{OwnerName()}의 {Type} 부위 회복");
    }

    public void AddStatus(StatusEffect effect)
    {
        if (effect == null || statusEffects.Contains(effect))
            return;

        statusEffects.Add(effect);
        Revision++;
    }

    // 기존 코드 호환용. 새 코드는 CharacterStatusController를 사용한다.
    public void AddStatus(
        StatusEffect effect,
        Character source)
    {
        if (effect == null)
            return;

        if (Owner == null)
        {
            Debug.LogWarning(
                $"{Type} 부위에 Owner가 없습니다.");
            return;
        }

        foreach (StatusEffect existing in statusEffects)
        {
            if (existing == null)
                continue;

            if (existing.CanMergeWith(effect))
            {
                existing.Merge(effect);
                Revision++;
                return;
            }
        }

        effect.Initialize(Owner, source, this);
        effect.OnApply();
        statusEffects.Add(effect);
        Revision++;
    }

    public void RemoveStatus(StatusEffect effect)
    {
        if (effect == null ||
            !statusEffects.Contains(effect))
        {
            return;
        }

        effect.OnRemove();
        statusEffects.Remove(effect);
        Revision++;
    }

    public void ClearStatusEffects()
    {
        foreach (StatusEffect effect in statusEffects.ToArray())
        {
            RemoveStatus(effect);
        }
    }

    public void SetDebugState(
        float currentHP,
        float maxHP,
        bool isWeakened,
        bool isBroken)
    {
        MaxPartHP = Mathf.Max(1f, maxHP);

        if (isBroken)
        {
            State = BodyPartState.Broken;
            PartHP = 0f;
            Revision++;
            return;
        }

        if (isWeakened || currentHP <= 0f)
        {
            State = BodyPartState.Weakened;
            PartHP = 0f;
            Revision++;
            return;
        }

        State = BodyPartState.Normal;
        PartHP = Mathf.Clamp(
            currentHP,
            1f,
            MaxPartHP);
        Revision++;
    }

    public void ReplaceSkills(
        IEnumerable<Skill> newSkills)
    {
        availableSkills.Clear();

        if (newSkills != null)
        {
            foreach (Skill skill in newSkills)
            {
                if (skill != null)
                    availableSkills.Add(skill);
            }
        }

        CurrentSkill =
            availableSkills.Count > 0
                ? availableSkills[0]
                : null;

        Revision++;
    }

    public void IncreaseMaxHPPercent(
        float percent,
        bool healByIncreaseAmount = true)
    {
        if (percent <= 0f)
            return;

        float oldMaxHP = MaxPartHP;
        float increase = oldMaxHP * percent;

        MaxPartHP = Mathf.Max(
            1f,
            oldMaxHP + increase);

        PartHP = healByIncreaseAmount
            ? Mathf.Clamp(
                PartHP + increase,
                0f,
                MaxPartHP)
            : Mathf.Clamp(
                PartHP,
                0f,
                MaxPartHP);

        Revision++;
    }

    private string OwnerName()
    {
        if (Owner == null)
            return "NULL";

        return Owner.Data?.CharacterName ??
               Owner.name;
    }
}
