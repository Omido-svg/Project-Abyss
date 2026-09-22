using System.Collections.Generic;
using UnityEngine;

public abstract class StatusEffect
{
    public const int InfiniteDuration = -1;

    public string Name { get; protected set; }
    public int Stack { get; protected set; }
    public int Duration { get; protected set; }

    public virtual string EffectName => GetType().Name;

    protected Character owner;
    protected Character source;
    protected BodyPart ownerPart;
    protected BodyPart sourcePart;

    public Character Owner => owner;
    public Character Source => source;
    public BodyPart OwnerPart => ownerPart;
    public BodyPart SourcePart => sourcePart;

    public bool IsPartEffect => ownerPart != null;
    public bool IsCharacterEffect => ownerPart == null;

    /// <summary>
    /// 0922 상태 저장 분류. 기본값은 Bespoke이며, 공용 수치형/값 없는 지속형만
    /// 각 파생 상태가 명시적으로 override한다.
    /// </summary>
    public virtual StatusEffectStorageKind StorageKind =>
        StatusEffectStorageKind.Bespoke;

    /// <summary>
    /// Generic N/T 수치형에서만 의미가 있는 N 값. Presence/Bespoke는 0을 반환한다.
    /// 기존 Stack 필드는 직렬화/진단 호환을 위해 유지한다.
    /// </summary>
    public virtual int NumericValue =>
        StorageKind == StatusEffectStorageKind.NumericTimed
            ? Mathf.Max(0, Stack)
            : 0;

    public virtual StatusEffectDurationPolicy DurationPolicy =>
        Duration < 0
            ? StatusEffectDurationPolicy.Permanent
            : StatusEffectDurationPolicy.TurnEnd;

    public virtual StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.RefreshDuration;

    // 최신 규칙에서는 부위 파괴 시 상태를 다른 범위로 이전하지 않는다.
    // 필드는 구형 파생 클래스 API 호환을 위해 남겨 둔다.
    public virtual bool TransferToCharacterOnPartBreak => false;

    public bool IsPermanent =>
        DurationPolicy == StatusEffectDurationPolicy.Permanent ||
        Duration < 0;

    /// <summary>
    /// 0922의 ∞ 지속시간을 999 같은 임시 숫자 없이 표현하기 위한 공용 abstraction.
    /// 내부 호환 표현은 InfiniteDuration(-1)을 사용한다.
    /// </summary>
    public StatusEffectDurationKind DurationKind =>
        IsPermanent
            ? StatusEffectDurationKind.Infinite
            : StatusEffectDurationKind.Finite;

    public bool IsInfiniteDuration =>
        DurationKind == StatusEffectDurationKind.Infinite;

    public int RemainingTurns =>
        IsInfiniteDuration
            ? InfiniteDuration
            : Mathf.Max(0, Duration);

    public bool IsExpired =>
        !IsPermanent &&
        Duration <= 0;

    public StatusEffectRemoveReason LastRemoveReason { get; private set; } =
        StatusEffectRemoveReason.Manual;

    public virtual void Initialize(
        Character owner,
        Character source,
        BodyPart ownerPart = null)
    {
        Initialize(owner, source, ownerPart, ownerPart);
    }

    public virtual void Initialize(
        Character owner,
        Character source,
        BodyPart ownerPart,
        BodyPart sourcePart)
    {
        this.owner = owner;
        this.source = source;
        this.ownerPart = ownerPart;
        this.sourcePart = sourcePart;
    }

    public virtual void OnApply() { }
    public virtual void OnTurnStart() { }
    public virtual void OnTurnEnd() { }

    public virtual void OnTurnStart(StatusEffectTickContext context)
    {
        OnTurnStart();
    }

    public virtual void OnTurnEnd(StatusEffectTickContext context)
    {
        OnTurnEnd();
    }

    public virtual void OnRemove() { }

    public virtual void Merge(StatusEffect other)
    {
        if (other == null)
            return;

        switch (StackPolicy)
        {
            case StatusEffectStackPolicy.Ignore:
                return;

            case StatusEffectStackPolicy.RefreshDuration:
                RefreshDurationFrom(other);
                return;

            case StatusEffectStackPolicy.AddStacks:
                Stack += Mathf.Max(0, other.Stack);
                return;

            case StatusEffectStackPolicy.AddStacksAndRefreshDuration:
                Stack += Mathf.Max(0, other.Stack);
                RefreshDurationFrom(other);
                return;

            case StatusEffectStackPolicy.Replace:
                Stack = other.Stack;
                Duration = other.Duration;
                return;
        }
    }

    public virtual bool CanMergeWith(StatusEffect other)
    {
        if (other == null)
            return false;

        return GetMergeKey() == other.GetMergeKey();
    }

    protected virtual string GetMergeKey()
    {
        return GetType().FullName;
    }

    internal StatusEffectApplyResult ApplyIncoming(
        StatusEffect incoming,
        bool wasTransferred = false)
    {
        StatusEffectApplyResult result =
            new StatusEffectApplyResult
            {
                TargetCharacter = owner,
                TargetPart = ownerPart,
                Effect = this,
                IncomingEffect = incoming,
                StackBefore = Stack,
                DurationBefore = Duration,
                WasTransferred = wasTransferred
            };

        if (!CanMergeWith(incoming))
        {
            result.Kind = StatusEffectApplyKind.Rejected;
            result.StackAfter = Stack;
            result.DurationAfter = Duration;
            return result;
        }

        if (StackPolicy == StatusEffectStackPolicy.Ignore)
        {
            result.Kind = StatusEffectApplyKind.Ignored;
            result.StackAfter = Stack;
            result.DurationAfter = Duration;
            return result;
        }

        Merge(incoming);

        if (incoming?.Source != null)
            source = incoming.Source;

        if (incoming?.SourcePart != null)
            sourcePart = incoming.SourcePart;

        result.StackAfter = Stack;
        result.DurationAfter = Duration;

        if (result.StackAfter != result.StackBefore)
            result.Kind = StatusEffectApplyKind.Stacked;
        else if (result.DurationAfter != result.DurationBefore)
            result.Kind = StatusEffectApplyKind.Refreshed;
        else
            result.Kind = StatusEffectApplyKind.Refreshed;

        return result;
    }

    internal void ProcessTurnStart(StatusEffectTickContext context)
    {
        if (context == null)
            return;

        context.CaptureBefore(this);
        OnTurnStart(context);

        if (DurationPolicy == StatusEffectDurationPolicy.TurnStart)
            DecreaseDuration();

        context.CaptureAfter(this);
    }

    internal void ProcessTurnEnd(StatusEffectTickContext context)
    {
        if (context == null)
            return;

        context.CaptureBefore(this);
        OnTurnEnd(context);

        if (DurationPolicy == StatusEffectDurationPolicy.TurnEnd)
            DecreaseDuration();

        context.CaptureAfter(this);
    }

    internal void PrepareRemoval(StatusEffectRemoveReason reason)
    {
        LastRemoveReason = reason;
    }

    /// <summary>
    /// Planning 단계에서 상태 적용을 취소할 때 적용 직전의 정확한 runtime 상태로 되돌린다.
    /// 기본 상태는 Stack/Duration만 복원하며, 별도 내부 상태를 가진 파생 클래스는 override한다.
    /// </summary>
    internal virtual void RestoreStateForPlanning(
        int stack,
        int duration)
    {
        Stack = stack;
        Duration = duration;
    }

    public virtual int ModifyRoll(BattleAction action, int roll)
    {
        return roll;
    }

    public virtual int ModifyExchangeRollCount(
        BattleAction action,
        int rollCount)
    {
        return rollCount;
    }

    public virtual int ModifyDamage(BattleAction action, int damage)
    {
        return damage;
    }

    public virtual float ModifyDamageTaken(BattleAction action, float damage)
    {
        return damage;
    }

    // [0917_CONFIRMED_GAP:STAGGER_STATUS_API]
    // 흐트러짐 내성 적용 뒤 더해지는 flat 보정. 반대 효과는 합산 후 상쇄한다.
    public virtual int GetStaggerDamageTakenFlatModifier(BattleAction action)
    {
        return 0;
    }

    public virtual int ModifyHealing(int amount)
    {
        return amount;
    }

    public virtual int ModifySpeed(BodyPart part, int speed)
    {
        return speed;
    }

    public virtual bool CanUseSkill(BodyPart part, Skill skill)
    {
        return true;
    }

    public virtual bool CanAct()
    {
        return true;
    }

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
        if (owner == null || owner.BodyParts == null)
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

    internal int ConsumeStacks(int amount)
    {
        int safe = Mathf.Max(0, amount);
        int consumed = Mathf.Min(Stack, safe);
        Stack = Mathf.Max(0, Stack - consumed);
        return consumed;
    }

    public void DecreaseDuration()
    {
        if (IsPermanent)
            return;

        if (Duration > 0)
            Duration--;
    }

    protected static int NormalizeTimedDuration(int duration)
    {
        return duration < 0
            ? InfiniteDuration
            : Mathf.Max(1, duration);
    }

    protected void RefreshDurationFrom(StatusEffect other)
    {
        if (other == null)
            return;

        if (IsPermanent)
            return;

        Duration = Mathf.Max(Duration, other.Duration);
    }

    // 기존 파생 클래스 호환용 수동 제거 API.
    protected void RemoveStatus()
    {
        if (owner == null)
            return;

        if (ownerPart != null)
        {
            owner.RemovePartStatus(
                ownerPart,
                this,
                StatusEffectRemoveReason.Manual);
            return;
        }

        owner.RemoveStatus(
            this,
            StatusEffectRemoveReason.Manual);
    }
}