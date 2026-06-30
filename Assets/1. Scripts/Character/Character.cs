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

        IsDead = false;

        //--------------------------------
        // BodyPart 초기화
        //--------------------------------

        foreach (BodyPart part in BodyParts)
        {
            if (part == null)
                continue;

            part.Initialize(this);

            foreach (Skill skill in part.AvailableSkills)
            {
                if (skill == null)
                    continue;

                skill.Initialize(this, battleEvent);
            }
        }

        RecalculateStatus();

        RuntimeStatus = new RuntimeStatus(CurrentStatus);

        RuntimeStatus.currentHP = CalculateInitialHP();
    }

    //------------------------------------------------
    
    public T GetStatus<T>() where T : StatusEffect
    {
        foreach (StatusEffect effect in statusEffects)
        {
            if (effect is T result)
                return result;
        }

        return null;
    }
    
    public BodyPart GetRandomUsablePart()
    {
        List<BodyPart> candidates = new();

        foreach (BodyPart part in BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken)
                continue;

            candidates.Add(part);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[
            UnityEngine.Random.Range(0, candidates.Count)];
    }

    private int CalculateInitialHP()
    {
        int hp = 0;

        foreach (BodyPart part in BodyParts)
        {
            if (part == null)
                continue;

            if (part.State == BodyPartState.Broken)
                continue;

            hp += Mathf.Max(0, Mathf.RoundToInt(part.PartHP));
        }

        return hp;
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
        foreach (StatusEffect effect in statusEffects.ToArray())
        {
            effect.OnTurnEnd();
        }

        foreach (BodyPart part in BodyParts)
        {
            if (part == null)
                continue;

            foreach (StatusEffect effect in part.StatusEffects.ToArray())
            {
                effect.OnTurnEnd();
            }
        }
    }

    //------------------------------------------------
    // Damage
    //------------------------------------------------

    public void TakeDamage(
        BodyPart targetPart,
        int damage,
        bool canBreakPart)
    {
        if (IsDead)
            return;

        if (damage <= 0)
            return;

        //-----------------------------------
        // 대상 부위가 없으면 전체 체력 직접 피해
        //-----------------------------------

        if (targetPart == null)
        {
            TakeDirectDamage(damage);
            return;
        }

        //-----------------------------------
        // 이미 파괴된 부위
        // 피해는 전체 체력으로 직접 들어감
        //-----------------------------------

        if (targetPart.State == BodyPartState.Broken)
        {
            TakeDirectDamage(damage);
            return;
        }

        //-----------------------------------
        // 이미 약화된 부위
        // 일반 공격이면 전체 체력 직접 피해
        // canBreakPart라면 슬롯 파괴까지 발생
        //-----------------------------------

        if (targetPart.State == BodyPartState.Weakened)
        {
            TakeDirectDamage(damage);

            if (canBreakPart)
            {
                BreakPart(targetPart);
            }

            return;
        }

        //-----------------------------------
        // 정상 부위
        //-----------------------------------

        if (targetPart.State == BodyPartState.Normal)
        {
            ApplyNormalPartDamage(targetPart, damage);

            //-----------------------------------
            // 중요:
            // canBreakPart여도 정상 부위를 바로 파괴하지 않는다.
            // 정상 부위는 먼저 Weakened까지 간다.
            // 이미 약화된 부위를 canBreakPart로 때릴 때만 Broken 처리.
            //-----------------------------------
        }

        CheckDead();
    }

    //------------------------------------------------
    // 일반 부위 피해
    // 부위는 1에서 멈추고 Weakened 상태가 됨
    //------------------------------------------------

    private void ApplyNormalPartDamage(
        BodyPart targetPart,
        int damage)
    {
        if (targetPart == null)
            return;

        if (damage <= 0)
            return;

        //-----------------------------------
        // 전체 체력은 실제 피해량만큼 감소
        //-----------------------------------

        ReduceCurrentHP(damage);

        //-----------------------------------
        // 부위 HP는 1까지만 감소
        //-----------------------------------

        float damageToWeaken =
            Mathf.Max(0f, targetPart.PartHP - 1f);

        if (damage < damageToWeaken)
        {
            targetPart.PartHP -= damage;

            Debug.Log(
                $"{Data.CharacterName}의 {targetPart.Type} 부위에 {damage} 피해 " +
                $"HP : {targetPart.PartHP}");

            return;
        }

        //-----------------------------------
        // 1 이하로 내려가면 1에서 멈추고 약화
        //-----------------------------------

        targetPart.PartHP = 1f;

        WeakenPart(targetPart);
    }

    //------------------------------------------------
    // 약화 처리
    //------------------------------------------------

    private void WeakenPart(BodyPart part)
    {
        if (part == null)
            return;

        if (part.State != BodyPartState.Normal)
            return;

        part.Weaken();

        battleEvent?.RaiseBodyPartWeakened(this, part);

        OnBodyPartDisabled(part);
    }

    //------------------------------------------------
    // 전체 체력 직접 피해
    //------------------------------------------------

    private void TakeDirectDamage(int damage)
    {
        if (damage <= 0)
            return;

        ReduceCurrentHP(damage);

        Debug.Log(
            $"{Data.CharacterName} 전체 체력에 {damage} 직접 피해");

        CheckDead();
    }

    //------------------------------------------------

    private void ReduceCurrentHP(int damage)
    {
        if (RuntimeStatus == null)
            return;

        RuntimeStatus.currentHP =
            Mathf.Max(
                RuntimeStatus.currentHP - damage,
                0);
    }

    //------------------------------------------------

    public void ForceRecalculateHP()
    {
        //-----------------------------------
        // 주의:
        // 전투 중에는 가급적 호출하지 않는 것이 좋음.
        // 약화/파괴 부위에 들어간 직접 피해 기록이 사라질 수 있음.
        //-----------------------------------

        RuntimeStatus.currentHP = CalculateInitialHP();
    }

    //------------------------------------------------

    public virtual void TakeTrueDamage(
        int damage,
        StatusEffect sourceEffect)
    {
        if (IsDead)
            return;

        if (damage <= 0)
            return;

        ReduceCurrentHP(damage);

        Debug.Log(
            $"{Data.CharacterName}이 고정 피해 {damage}를 받음");

        CheckDead();
    }

    //------------------------------------------------

    protected virtual void CheckDead()
    {
        if (CanDie())
        {
            Die();
        }
    }

    //------------------------------------------------

    protected virtual bool CanDie()
    {
        if (RuntimeStatus.currentHP <= 0)
            return true;

        //-----------------------------------
        // 모든 부위가 파괴되어도 사망
        //-----------------------------------

        bool allBroken = true;

        foreach (BodyPart part in BodyParts)
        {
            if (part == null)
                continue;

            if (!part.IsBroken)
            {
                allBroken = false;
                break;
            }
        }

        return allBroken;
    }

    //------------------------------------------------

    public virtual void Die()
    {
        if (IsDead)
            return;

        IsDead = true;

        Debug.Log($"{Data.CharacterName} 사망");

        battleEvent?.RaiseCharacterDeath(this);
    }

    //------------------------------------------------
    // Disabled / Weakened
    //------------------------------------------------

    protected virtual void OnBodyPartDisabled(BodyPart part)
    {
        StatusEffect effect = CreateDisabledDebuff(part);

        if (effect == null)
            return;

        part.AddStatus(effect, this);

        battleEvent?.RaiseBodyPartStatusApplied(this, part, effect);
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
        if (part == null)
            return;

        if (part.IsBroken)
            return;

        int remainingPartHP =
            Mathf.Max(0, Mathf.RoundToInt(part.PartHP));

        if (remainingPartHP > 0)
        {
            ReduceCurrentHP(remainingPartHP);
        }

        part.Break();

        //--------------------------------
        // 부위 약화 디버프 제거
        //--------------------------------

        foreach (var effect in part.StatusEffects.ToArray())
        {
            part.RemoveStatus(effect);

            battleEvent?.RaiseBodyPartStatusRemoved(this, part, effect);
        }

        //--------------------------------
        // 캐릭터 파괴 디버프 새로 부여
        //--------------------------------

        OnBodyPartBroken(part, null);

        battleEvent?.RaiseBodyPartDestroyed(this, part);

        CheckDead();
    }

    //------------------------------------------------
    // 고유 파괴 루트용
    // 올라프 출혈 3스택, 유진 처형, 김삿갓 뼈 스택 등에서 사용
    //------------------------------------------------

    public void ForceBreakPart(BodyPart part)
    {
        if (part == null)
            return;

        if (part.IsBroken)
            return;

        BreakPart(part);
    }

    //------------------------------------------------

    protected virtual void OnBodyPartBroken(
        BodyPart part,
        StatusEffect disabledDebuff)
    {
        StatusEffect brokenStatus =
            CreateBrokenPartStatus(part);

        if (brokenStatus == null)
            return;

        AddStatus(brokenStatus, this);
    }
    
    protected virtual StatusEffect CreateBrokenPartStatus(
        BodyPart part)
    {
        if (part == null)
            return null;

        return part.Type switch
        {
            PartType.HEAD => new BrokenHead(),
            PartType.LEFT_HAND => new BrokenArm(PartType.LEFT_HAND),
            PartType.RIGHT_HAND => new BrokenArm(PartType.RIGHT_HAND),
            PartType.LEGS => new BrokenLegs(),
            _ => null
        };
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

        //--------------------------------
        // 같은 타입 상태이상이 이미 있으면 Merge
        //--------------------------------

        foreach (StatusEffect existing in statusEffects)
        {
            if (existing.GetType() == effect.GetType())
            {
                existing.Merge(effect);

                battleEvent?.RaiseStatusApplied(this, existing);

                return;
            }
        }

        //--------------------------------
        // 새 상태이상 추가
        //--------------------------------

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

        if (action != null &&
            action.OwnerPart != null)
        {
            foreach (var effect in action.OwnerPart.StatusEffects)
            {
                value = effect.ModifyRoll(action, value);
            }
        }

        return value;
    }
    
    public bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        if (part == null)
            return false;

        if (skill == null)
            return false;

        if (part.IsBroken)
            return false;

        foreach (StatusEffect effect in statusEffects)
        {
            if (effect == null)
                continue;

            if (!effect.CanUseSkill(part, skill))
                return false;
        }

        foreach (StatusEffect effect in part.StatusEffects)
        {
            if (effect == null)
                continue;

            if (!effect.CanUseSkill(part, skill))
                return false;
        }

        return true;
    }
}