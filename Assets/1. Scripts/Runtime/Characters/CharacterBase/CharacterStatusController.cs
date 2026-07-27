using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CharacterStatusController
{
    private readonly Character owner;

    private readonly List<StatusEffect> characterStatuses = new();

    public IReadOnlyList<StatusEffect> CharacterStatuses =>
        characterStatuses;

    public CharacterStatusController(Character owner)
    {
        this.owner = owner;
    }

    public void ClearAll(
        StatusEffectRemoveReason reason =
            StatusEffectRemoveReason.Cleared,
        bool raiseEvents = false)
    {
        foreach (StatusEffect effect in characterStatuses.ToArray())
        {
            if (effect == null)
                continue;

            if (raiseEvents)
            {
                RemoveStatus(effect, reason);
                continue;
            }

            effect.PrepareRemoval(reason);
            effect.OnRemove();
            characterStatuses.Remove(effect);
        }

        if (owner?.BodyParts == null)
            return;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            foreach (StatusEffect effect in part.StatusEffects.ToArray())
            {
                if (effect == null)
                    continue;

                if (raiseEvents)
                {
                    RemovePartStatus(part, effect, reason);
                    continue;
                }

                effect.PrepareRemoval(reason);
                part.RemoveStatus(effect);
            }
        }
    }

    public StatusEffectApplyResult AddStatus(
        StatusEffect effect,
        Character source)
    {
        return AddCharacterStatusInternal(
            effect, source, null, wasTransferred: false);
    }

    public StatusEffectApplyResult AddStatus(
        StatusEffect effect,
        Character source,
        BodyPart sourcePart)
    {
        return AddCharacterStatusInternal(
            effect, source, sourcePart, wasTransferred: false);
    }

    public StatusEffectApplyResult AddPartStatus(
        BodyPart part,
        StatusEffect effect,
        Character source)
    {
        if (owner == null || effect == null)
            return Rejected(effect, part);

        if (part != null &&
            part.Owner != null &&
            part.Owner != owner)
        {
            return Rejected(effect, part);
        }

        // Single HP 대상 또는 이미 파괴된 부위는 캐릭터 상태로 받는다.
        if (part == null || part.IsBroken)
        {
            return AddCharacterStatusInternal(
                effect, source, part, wasTransferred: part != null);
        }

        effect.Initialize(owner, source, part);

        StatusEffect existing =
            FindSamePartStatus(part, effect);

        if (existing != null)
        {
            StatusEffectApplyResult merged =
                existing.ApplyIncoming(effect);

            RaiseApplyEvents(merged);
            return merged;
        }

        effect.OnApply();
        part.AddStatus(effect);

        StatusEffectApplyResult result =
            CreateAppliedResult(effect, part, false);

        RaiseApplyEvents(result);

        Debug.Log(
            $"{GetOwnerName()} {part.Type} 부위에 " +
            $"{effect.Name} 상태 부여 " +
            $"(Stack={effect.Stack}, Duration={effect.Duration})");

        return result;
    }

    private StatusEffectApplyResult AddCharacterStatusInternal(
        StatusEffect effect,
        Character source,
        BodyPart sourcePart,
        bool wasTransferred)
    {
        if (owner == null || effect == null)
            return Rejected(effect, null);

        effect.Initialize(owner, source, null, sourcePart);

        StatusEffect existing =
            FindSameStatus(effect);

        if (existing != null)
        {
            StatusEffectApplyResult merged =
                existing.ApplyIncoming(
                    effect,
                    wasTransferred);

            RaiseApplyEvents(merged);
            return merged;
        }

        effect.OnApply();
        characterStatuses.Add(effect);

        StatusEffectApplyResult result =
            CreateAppliedResult(
                effect,
                null,
                wasTransferred);

        RaiseApplyEvents(result);

        Debug.Log(
            $"{GetOwnerName()}에게 {effect.Name} 상태 부여 " +
            $"(Stack={effect.Stack}, Duration={effect.Duration})");

        return result;
    }

    public void RemoveStatus(StatusEffect effect)
    {
        RemoveStatus(
            effect,
            StatusEffectRemoveReason.Manual);
    }

    public void RemoveStatus(
        StatusEffect effect,
        StatusEffectRemoveReason reason)
    {
        if (effect == null ||
            !characterStatuses.Contains(effect))
        {
            return;
        }

        effect.PrepareRemoval(reason);
        effect.OnRemove();
        characterStatuses.Remove(effect);

        RaiseRemoveEvents(
            null,
            effect,
            reason);

        Debug.Log(
            $"{GetOwnerName()}의 {effect.Name} 상태 제거 / Reason={reason}");
    }

    public void RemovePartStatus(
        BodyPart part,
        StatusEffect effect)
    {
        RemovePartStatus(
            part,
            effect,
            StatusEffectRemoveReason.Manual);
    }

    public void RemovePartStatus(
        BodyPart part,
        StatusEffect effect,
        StatusEffectRemoveReason reason)
    {
        if (part == null || effect == null)
            return;

        if (!part.StatusEffects.Contains(effect))
            return;

        effect.PrepareRemoval(reason);
        part.RemoveStatus(effect);

        RaiseRemoveEvents(
            part,
            effect,
            reason);

        Debug.Log(
            $"{GetOwnerName()} {part.Type}의 {effect.Name} 상태 제거 / Reason={reason}");
    }

    public void RemoveAllPartStatuses(
        BodyPart part,
        StatusEffectRemoveReason reason)
    {
        if (part == null)
            return;

        foreach (StatusEffect effect in part.StatusEffects.ToArray())
        {
            if (effect == null)
                continue;

            RemovePartStatus(
                part,
                effect,
                reason);
        }
    }

    public void RemoveBrokenStatusForPart(BodyPart part)
    {
        if (part == null)
            return;

        foreach (StatusEffect effect in characterStatuses.ToArray())
        {
            if (effect is not BrokenPartStatus broken)
                continue;

            if (!broken.MatchesPart(part))
                continue;

            RemoveStatus(
                effect,
                StatusEffectRemoveReason.PartRecovered);
        }
    }

    public void TransferPartStatusesToCharacter(BodyPart part)
    {
        if (owner == null || part == null)
            return;

        foreach (StatusEffect effect in part.StatusEffects.ToArray())
        {
            if (effect == null)
                continue;

            RemovePartStatus(
                part,
                effect,
                StatusEffectRemoveReason.PartBroken);
        }
    }

    public void RemoveStatusesFromSourcePart(
        BodyPart sourcePart,
        StatusEffectRemoveReason reason)
    {
        if (sourcePart == null)
            return;

        foreach (StatusEffect effect in characterStatuses.ToArray())
        {
            if (effect?.SourcePart != sourcePart)
                continue;

            RemoveStatus(effect, reason);
        }
    }

    public void OnTurnStart()
    {
        TickCharacterStatuses(
            StatusEffectTickTiming.TurnStart);

        TickPartStatuses(
            StatusEffectTickTiming.TurnStart);
    }

    public void OnTurnEnd()
    {
        TickCharacterStatuses(
            StatusEffectTickTiming.TurnEnd);

        if (owner != null && !owner.IsDead)
        {
            TickPartStatuses(
                StatusEffectTickTiming.TurnEnd);
        }
    }

    private void TickCharacterStatuses(
        StatusEffectTickTiming timing)
    {
        foreach (StatusEffect effect in characterStatuses.ToArray())
        {
            if (owner == null || owner.IsDead)
                break;

            if (effect == null ||
                !characterStatuses.Contains(effect))
            {
                continue;
            }

            TickEffect(
                effect,
                null,
                timing);
        }
    }

    private void TickPartStatuses(
        StatusEffectTickTiming timing)
    {
        if (owner?.BodyParts == null)
            return;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (owner.IsDead)
                break;

            if (part == null || part.IsBroken)
                continue;

            foreach (StatusEffect effect in part.StatusEffects.ToArray())
            {
                if (owner.IsDead)
                    break;

                if (effect == null ||
                    !part.StatusEffects.Contains(effect))
                {
                    continue;
                }

                TickEffect(
                    effect,
                    part,
                    timing);
            }
        }
    }

    private void TickEffect(
        StatusEffect effect,
        BodyPart part,
        StatusEffectTickTiming timing)
    {
        StatusEffectTickContext context =
            new StatusEffectTickContext(
                owner,
                part,
                effect,
                timing);

        if (timing == StatusEffectTickTiming.TurnStart)
            effect.ProcessTurnStart(context);
        else
            effect.ProcessTurnEnd(context);

        owner.BattleEvent?.RaiseStatusTicked(context);

        bool hasMeaningfulChange =
            context.DidApplyDamage ||
            context.AppliedDamage > 0 ||
            context.StackBefore != context.StackAfter ||
            context.DurationBefore != context.DurationAfter ||
            context.ExpiredAfterTick;

        if (hasMeaningfulChange)
        {
            Debug.Log(
                $"[STATUS TICK] {GetOwnerName()} / " +
                $"Timing={timing}, " +
                $"Status={effect.Name}, " +
                $"Part={(part == null ? "NONE" : part.Type.ToString())}, " +
                $"Stack={context.StackBefore}->{context.StackAfter}, " +
                $"Duration={context.DurationBefore}->{context.DurationAfter}, " +
                $"Damage={context.AppliedDamage}");
        }

        if (!effect.IsExpired)
            return;

        if (part == null)
        {
            RemoveStatus(
                effect,
                StatusEffectRemoveReason.Expired);
        }
        else
        {
            RemovePartStatus(
                part,
                effect,
                StatusEffectRemoveReason.Expired);
        }
    }

    public T GetStatus<T>() where T : StatusEffect
    {
        foreach (StatusEffect effect in characterStatuses)
        {
            if (effect is T typedEffect)
                return typedEffect;
        }

        return null;
    }

    public T GetPartStatus<T>(BodyPart part) where T : StatusEffect
    {
        if (part == null)
            return null;

        foreach (StatusEffect effect in part.StatusEffects)
        {
            if (effect is T typedEffect)
                return typedEffect;
        }

        return null;
    }

    public bool HasStatus<T>() where T : StatusEffect
    {
        return GetStatus<T>() != null;
    }

    public bool HasPartStatus<T>(BodyPart part) where T : StatusEffect
    {
        return GetPartStatus<T>(part) != null;
    }

    private StatusEffect FindSameStatus(StatusEffect effect)
    {
        if (effect == null)
            return null;

        foreach (StatusEffect existing in characterStatuses)
        {
            if (existing != null &&
                existing.CanMergeWith(effect))
            {
                return existing;
            }
        }

        return null;
    }

    private StatusEffect FindSamePartStatus(
        BodyPart part,
        StatusEffect effect)
    {
        if (part == null || effect == null)
            return null;

        foreach (StatusEffect existing in part.StatusEffects)
        {
            if (existing != null &&
                existing.CanMergeWith(effect))
            {
                return existing;
            }
        }

        return null;
    }

    private void RaiseApplyEvents(
        StatusEffectApplyResult result)
    {
        if (result == null || !result.Succeeded)
            return;

        if (result.TargetPart == null)
        {
            owner.BattleEvent?.RaiseStatusApplied(
                owner,
                result.Effect);
        }
        else
        {
            owner.BattleEvent?.RaiseBodyPartStatusApplied(
                owner,
                result.TargetPart,
                result.Effect);
        }

        owner.BattleEvent?.RaiseStatusApplyResolved(result);

        owner.BattleContext?.EffectResolver?
            .ShowStatusApplyVisual(result);
    }

    private void RaiseRemoveEvents(
        BodyPart part,
        StatusEffect effect,
        StatusEffectRemoveReason reason)
    {
        if (part == null)
        {
            owner.BattleEvent?.RaiseStatusRemoved(
                owner,
                effect);
        }
        else
        {
            owner.BattleEvent?.RaiseBodyPartStatusRemoved(
                owner,
                part,
                effect);
        }

        owner.BattleEvent?.RaiseStatusRemovedDetailed(
            owner,
            part,
            effect,
            reason);

        owner.BattleContext?.EffectResolver?
            .ShowStatusRemoveVisual(
                owner,
                part,
                effect,
                reason);
    }

    private StatusEffectApplyResult CreateAppliedResult(
        StatusEffect effect,
        BodyPart part,
        bool wasTransferred)
    {
        return new StatusEffectApplyResult
        {
            TargetCharacter = owner,
            TargetPart = part,
            Effect = effect,
            IncomingEffect = effect,
            Kind = StatusEffectApplyKind.Applied,
            StackBefore = 0,
            StackAfter = effect?.Stack ?? 0,
            DurationBefore = 0,
            DurationAfter = effect?.Duration ?? 0,
            WasTransferred = wasTransferred
        };
    }

    private StatusEffectApplyResult Rejected(
        StatusEffect effect,
        BodyPart part)
    {
        return new StatusEffectApplyResult
        {
            TargetCharacter = owner,
            TargetPart = part,
            Effect = effect,
            IncomingEffect = effect,
            Kind = StatusEffectApplyKind.Rejected
        };
    }

    private string GetOwnerName()
    {
        return owner?.Data?.CharacterName ??
               owner?.name ??
               "NULL";
    }
}
