using System;

public class BattleEvent
{
    public event Action<int> OnTurnStart;
    public event Action<int> OnTurnEnd;

    public void RaiseTurnStart(int turn) =>
        OnTurnStart?.Invoke(turn);

    public void RaiseTurnEnd(int turn) =>
        OnTurnEnd?.Invoke(turn);

    public event Action<BattleAction> OnActionStart;
    public event Action<BattleAction> OnActionEnd;

    public void RaiseActionStart(BattleAction action) =>
        OnActionStart?.Invoke(action);

    public void RaiseActionEnd(BattleAction action) =>
        OnActionEnd?.Invoke(action);

    public event Action<Character, Character> OnClashStart;
    public event Action<BattleAction, BattleAction> OnClashWin;
    public event Action<BattleAction, BattleAction> OnClashLose;

    public void RaiseClashStart(
        Character attacker,
        Character defender) =>
        OnClashStart?.Invoke(attacker, defender);

    public void RaiseClashWin(
        BattleAction winnerAction,
        BattleAction loserAction) =>
        OnClashWin?.Invoke(winnerAction, loserAction);

    public void RaiseClashLose(
        BattleAction loserAction,
        BattleAction winnerAction) =>
        OnClashLose?.Invoke(loserAction, winnerAction);

    //-----------------------------------
    // Damage
    //-----------------------------------

    public event Action<Character, int> OnDamageTaken;
    public event Action<Character, int> OnDamageDealt;

    public event Action<DamageContext> OnDamageResolved;
    public event Action<DamageEventResult> OnDamageEventResolved;

    public void RaiseDamageTaken(
        Character target,
        int damage) =>
        OnDamageTaken?.Invoke(target, damage);

    public void RaiseDamageDealt(
        Character attacker,
        int damage) =>
        OnDamageDealt?.Invoke(attacker, damage);

    public void RaiseDamageResolved(
        DamageContext context) =>
        OnDamageResolved?.Invoke(context);

    public void RaiseDamageEventResolved(
        DamageEventResult result) =>
        OnDamageEventResolved?.Invoke(result);

    //-----------------------------------
    // Status
    //-----------------------------------

    public event Action<Character, StatusEffect> OnStatusApplied;
    public event Action<Character, StatusEffect> OnStatusRemoved;

    public void RaiseStatusApplied(
        Character target,
        StatusEffect effect) =>
        OnStatusApplied?.Invoke(target, effect);

    public void RaiseStatusRemoved(
        Character target,
        StatusEffect effect) =>
        OnStatusRemoved?.Invoke(target, effect);

    public event Action<StatusEffectApplyResult> OnStatusApplyResolved;
    public event Action<StatusEffectTickContext> OnStatusTicked;
    public event Action<
        Character,
        BodyPart,
        StatusEffect,
        StatusEffectRemoveReason>
        OnStatusRemovedDetailed;

    public void RaiseStatusApplyResolved(
        StatusEffectApplyResult result) =>
        OnStatusApplyResolved?.Invoke(result);

    public void RaiseStatusTicked(
        StatusEffectTickContext context) =>
        OnStatusTicked?.Invoke(context);

    public void RaiseStatusRemovedDetailed(
        Character target,
        BodyPart part,
        StatusEffect effect,
        StatusEffectRemoveReason reason) =>
        OnStatusRemovedDetailed?.Invoke(
            target,
            part,
            effect,
            reason);

    //-----------------------------------
    // Body-part transitions
    //-----------------------------------

    // 기존 단순 이벤트는 UI와 기존 메커닉 호환용으로 유지한다.
    public event Action<Character, BodyPart> OnBodyPartWeakened;
    public event Action<Character, BodyPart> OnBodyPartDestroyed;
    public event Action<Character, BodyPart> OnBodyPartRecovered;

    // 신규 상세 이벤트는 원인과 DamageContext를 보존한다.
    public event Action<BodyPartWeakenEventContext>
        OnBodyPartWeakenResolved;

    public event Action<BodyPartBreakEventContext>
        OnBodyPartBreakResolved;

    public void RaiseBodyPartWeakened(
        BodyPartWeakenEventContext context)
    {
        if (context == null ||
            context.Target == null ||
            context.Part == null)
        {
            return;
        }

        OnBodyPartWeakenResolved?.Invoke(context);
        OnBodyPartWeakened?.Invoke(
            context.Target,
            context.Part);
    }

    public void RaiseBodyPartDestroyed(
        BodyPartBreakEventContext context)
    {
        if (context == null ||
            context.Target == null ||
            context.Part == null)
        {
            return;
        }

        OnBodyPartBreakResolved?.Invoke(context);
        OnBodyPartDestroyed?.Invoke(
            context.Target,
            context.Part);
    }

    // 기존 호출부 호환.
    public void RaiseBodyPartWeakened(
        Character target,
        BodyPart part)
    {
        RaiseBodyPartWeakened(
            BodyPartWeakenEventContext.External(
                null,
                target,
                part));
    }

    // 기존 호출부 호환.
    public void RaiseBodyPartDestroyed(
        Character target,
        BodyPart part)
    {
        RaiseBodyPartDestroyed(
            BodyPartBreakEventContext.External(
                null,
                target,
                part));
    }

    public void RaiseBodyPartRecovered(
        Character target,
        BodyPart part) =>
        OnBodyPartRecovered?.Invoke(target, part);

    //-----------------------------------
    // Part status
    //-----------------------------------

    public event Action<
        Character,
        BodyPart,
        StatusEffect>
        OnBodyPartStatusApplied;

    public event Action<
        Character,
        BodyPart,
        StatusEffect>
        OnBodyPartStatusRemoved;

    public void RaiseBodyPartStatusApplied(
        Character target,
        BodyPart part,
        StatusEffect effect) =>
        OnBodyPartStatusApplied?.Invoke(
            target,
            part,
            effect);

    public void RaiseBodyPartStatusRemoved(
        Character target,
        BodyPart part,
        StatusEffect effect) =>
        OnBodyPartStatusRemoved?.Invoke(
            target,
            part,
            effect);

    //-----------------------------------
    // Death / Kill
    //-----------------------------------

    // 기존 단순 이벤트.
    public event Action<Character> OnCharacterDeath;
    public event Action<Character, Character> OnKill;

    // 신규 상세 이벤트.
    public event Action<KillEventContext>
        OnCharacterDeathResolved;

    public event Action<KillEventContext>
        OnKillResolved;

    public void RaiseCharacterDeath(
        KillEventContext context)
    {
        if (context == null ||
            context.Victim == null)
        {
            return;
        }

        OnCharacterDeathResolved?.Invoke(context);
        OnCharacterDeath?.Invoke(context.Victim);
    }

    public void RaiseKill(
        KillEventContext context)
    {
        if (context == null ||
            !context.HasKiller)
        {
            return;
        }

        OnKillResolved?.Invoke(context);
        OnKill?.Invoke(
            context.Killer,
            context.Victim);
    }

    // 기존 호출부 호환.
    public void RaiseCharacterDeath(
        Character target)
    {
        RaiseCharacterDeath(
            KillEventContext.External(
                null,
                target));
    }

    // 기존 호출부 호환.
    public void RaiseKill(
        Character killer,
        Character victim)
    {
        RaiseKill(
            KillEventContext.External(
                killer,
                victim));
    }

    //-----------------------------------
    // Clash result
    //-----------------------------------

    public event Action<ClashResultContext> OnClashResolved;

    public void RaiseClashResolved(
        ClashResultContext context) =>
        OnClashResolved?.Invoke(context);
}
