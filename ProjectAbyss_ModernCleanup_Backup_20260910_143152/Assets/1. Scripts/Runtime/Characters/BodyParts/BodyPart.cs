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
        float maxPartHP)
    {
        Type = type;
        MaxPartHP = Mathf.Max(1f, maxPartHP);
        PartHP = MaxPartHP;
        State = BodyPartState.Normal;
    }

    public void Initialize(Character owner)
    {
        // 재사용되는 BodyPart라면 남아 있던 상태 객체의 정리 훅을 먼저 호출한다.
        ClearStatusEffects();

        Owner = owner;
        PartHP = MaxPartHP;
        State = BodyPartState.Normal;
        Revision++;
    }

    public int ApplyDamage(int damage)
    {
        if (damage <= 0 || State != BodyPartState.Normal)
            return 0;

        int before = Mathf.Max(
            0,
            Mathf.CeilToInt(PartHP));

        int maximumApplicable = Mathf.Max(0, before - 1);
        int applied = Mathf.Min(maximumApplicable, damage);
        PartHP = Mathf.Max(1f, PartHP - applied);

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
        PartHP = 1f;
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

    /// <summary>
    /// 무력화 게이지처럼 일시적으로 부여된 약화를 해제한다.
    /// 일반 Recover와 달리 부위 HP를 최대치로 치유하지 않고 약화 전 HP를 복원한다.
    /// </summary>
    public void RestoreTemporaryWeaken(float restoredHp)
    {
        if (State != BodyPartState.Weakened)
            return;

        State = BodyPartState.Normal;
        PartHP = Mathf.Clamp(restoredHp, 1f, MaxPartHP);
        Revision++;

        Debug.Log(
            $"{OwnerName()}의 {Type} 일시 약화 해제 / HP={PartHP}/{MaxPartHP}");
    }

    public void AddStatus(StatusEffect effect)
    {
        if (effect == null || statusEffects.Contains(effect))
            return;

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

        if (isWeakened || currentHP <= 1f)
        {
            State = BodyPartState.Weakened;
            PartHP = 1f;
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