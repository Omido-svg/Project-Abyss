using System.Collections.Generic;
using UnityEngine;

public abstract class StatusEffect
{
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

    public void DecreaseDuration()
    {
        if (IsPermanent)
            return;

        if (Duration > 0)
            Duration--;
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