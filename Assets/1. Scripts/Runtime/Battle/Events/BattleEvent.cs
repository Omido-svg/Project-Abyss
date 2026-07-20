using System;
using UnityEngine;

public class BattleEvent : IDisposable
{
    public bool IsDisposed { get; private set; }

    //-----------------------------------
    // Battle lifecycle
    //-----------------------------------

    public event Action OnBattleStarted;
    public event Action OnBattleEnded;

    public void RaiseBattleStarted()
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnBattleStarted,
            nameof(OnBattleStarted));
    }

    public void RaiseBattleEnded()
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnBattleEnded,
            nameof(OnBattleEnded));
    }

    //-----------------------------------
    // Turn
    //-----------------------------------

    public event Action<int> OnTurnStart;
    public event Action<int> OnTurnEnd;

    public void RaiseTurnStart(int turn)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnTurnStart,
            turn,
            nameof(OnTurnStart));
    }

    public void RaiseTurnEnd(int turn)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnTurnEnd,
            turn,
            nameof(OnTurnEnd));
    }

    //-----------------------------------
    // Action
    //-----------------------------------

    public event Action<BattleAction> OnActionStart;
    public event Action<BattleAction> OnActionEnd;

    public void RaiseActionStart(BattleAction action)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnActionStart,
            action,
            nameof(OnActionStart));
    }

    public void RaiseActionEnd(BattleAction action)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnActionEnd,
            action,
            nameof(OnActionEnd));
    }

    //-----------------------------------
    // Clash
    //-----------------------------------

    public event Action<Character, Character> OnClashStart;
    public event Action<BattleAction, BattleAction> OnClashWin;
    public event Action<BattleAction, BattleAction> OnClashLose;

    public void RaiseClashStart(
        Character attacker,
        Character defender)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnClashStart,
            attacker,
            defender,
            nameof(OnClashStart));
    }

    public void RaiseClashWin(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnClashWin,
            winnerAction,
            loserAction,
            nameof(OnClashWin));
    }

    public void RaiseClashLose(
        BattleAction loserAction,
        BattleAction winnerAction)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnClashLose,
            loserAction,
            winnerAction,
            nameof(OnClashLose));
    }

    //-----------------------------------
    // Damage
    //-----------------------------------

    public event Action<Character, int> OnDamageTaken;
    public event Action<Character, int> OnDamageDealt;

    public event Action<DamageContext> OnDamageResolved;
    public event Action<DamageEventResult> OnDamageEventResolved;

    public void RaiseDamageTaken(
        Character target,
        int damage)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnDamageTaken,
            target,
            damage,
            nameof(OnDamageTaken));
    }

    public void RaiseDamageDealt(
        Character attacker,
        int damage)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnDamageDealt,
            attacker,
            damage,
            nameof(OnDamageDealt));
    }

    public void RaiseDamageResolved(
        DamageContext context)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnDamageResolved,
            context,
            nameof(OnDamageResolved));
    }

    public void RaiseDamageEventResolved(
        DamageEventResult result)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnDamageEventResolved,
            result,
            nameof(OnDamageEventResolved));
    }

    //-----------------------------------
    // Combat resources
    //-----------------------------------

    public event Action<CombatResourceChangeContext>
        OnCombatResourceChanged;

    public void RaiseCombatResourceChanged(
        CombatResourceChangeContext context)
    {
        if (IsDisposed || context == null)
            return;

        InvokeSafely(
            OnCombatResourceChanged,
            context,
            nameof(OnCombatResourceChanged));
    }

    //-----------------------------------
    // Status
    //-----------------------------------

    public event Action<Character, StatusEffect> OnStatusApplied;
    public event Action<Character, StatusEffect> OnStatusRemoved;

    public void RaiseStatusApplied(
        Character target,
        StatusEffect effect)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnStatusApplied,
            target,
            effect,
            nameof(OnStatusApplied));
    }

    public void RaiseStatusRemoved(
        Character target,
        StatusEffect effect)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnStatusRemoved,
            target,
            effect,
            nameof(OnStatusRemoved));
    }

    public event Action<StatusEffectApplyResult> OnStatusApplyResolved;
    public event Action<StatusEffectTickContext> OnStatusTicked;
    public event Action<
        Character,
        BodyPart,
        StatusEffect,
        StatusEffectRemoveReason>
        OnStatusRemovedDetailed;

    public void RaiseStatusApplyResolved(
        StatusEffectApplyResult result)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnStatusApplyResolved,
            result,
            nameof(OnStatusApplyResolved));
    }

    public void RaiseStatusTicked(
        StatusEffectTickContext context)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnStatusTicked,
            context,
            nameof(OnStatusTicked));
    }

    public void RaiseStatusRemovedDetailed(
        Character target,
        BodyPart part,
        StatusEffect effect,
        StatusEffectRemoveReason reason)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnStatusRemovedDetailed,
            target,
            part,
            effect,
            reason,
            nameof(OnStatusRemovedDetailed));
    }

    //-----------------------------------
    // Body-part transitions
    //-----------------------------------

    public event Action<Character, BodyPart> OnBodyPartWeakened;
    public event Action<Character, BodyPart> OnBodyPartDestroyed;
    public event Action<Character, BodyPart> OnBodyPartRecovered;

    public event Action<BodyPartWeakenEventContext>
        OnBodyPartWeakenResolved;

    public event Action<BodyPartBreakEventContext>
        OnBodyPartBreakResolved;

    public void RaiseBodyPartWeakened(
        BodyPartWeakenEventContext context)
    {
        if (IsDisposed ||
            context == null ||
            context.Target == null ||
            context.Part == null)
        {
            return;
        }

        InvokeSafely(
            OnBodyPartWeakenResolved,
            context,
            nameof(OnBodyPartWeakenResolved));

        InvokeSafely(
            OnBodyPartWeakened,
            context.Target,
            context.Part,
            nameof(OnBodyPartWeakened));
    }

    public void RaiseBodyPartDestroyed(
        BodyPartBreakEventContext context)
    {
        if (IsDisposed ||
            context == null ||
            context.Target == null ||
            context.Part == null)
        {
            return;
        }

        InvokeSafely(
            OnBodyPartBreakResolved,
            context,
            nameof(OnBodyPartBreakResolved));

        InvokeSafely(
            OnBodyPartDestroyed,
            context.Target,
            context.Part,
            nameof(OnBodyPartDestroyed));
    }

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
        BodyPart part)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnBodyPartRecovered,
            target,
            part,
            nameof(OnBodyPartRecovered));
    }

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
        StatusEffect effect)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnBodyPartStatusApplied,
            target,
            part,
            effect,
            nameof(OnBodyPartStatusApplied));
    }

    public void RaiseBodyPartStatusRemoved(
        Character target,
        BodyPart part,
        StatusEffect effect)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnBodyPartStatusRemoved,
            target,
            part,
            effect,
            nameof(OnBodyPartStatusRemoved));
    }

    //-----------------------------------
    // Death / Kill
    //-----------------------------------

    public event Action<Character> OnCharacterDeath;
    public event Action<Character, Character> OnKill;

    public event Action<KillEventContext>
        OnCharacterDeathResolved;

    public event Action<KillEventContext>
        OnKillResolved;

    public void RaiseCharacterDeath(
        KillEventContext context)
    {
        if (IsDisposed ||
            context == null ||
            context.Victim == null)
        {
            return;
        }

        InvokeSafely(
            OnCharacterDeathResolved,
            context,
            nameof(OnCharacterDeathResolved));

        InvokeSafely(
            OnCharacterDeath,
            context.Victim,
            nameof(OnCharacterDeath));
    }

    public void RaiseKill(
        KillEventContext context)
    {
        if (IsDisposed ||
            context == null ||
            !context.HasKiller)
        {
            return;
        }

        InvokeSafely(
            OnKillResolved,
            context,
            nameof(OnKillResolved));

        InvokeSafely(
            OnKill,
            context.Killer,
            context.Victim,
            nameof(OnKill));
    }

    public void RaiseCharacterDeath(
        Character target)
    {
        RaiseCharacterDeath(
            KillEventContext.External(
                null,
                target));
    }

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
        ClashResultContext context)
    {
        if (IsDisposed)
            return;

        InvokeSafely(
            OnClashResolved,
            context,
            nameof(OnClashResolved));
    }

    //-----------------------------------
    // Lifetime
    //-----------------------------------

    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;

        OnBattleStarted = null;
        OnBattleEnded = null;

        OnTurnStart = null;
        OnTurnEnd = null;

        OnActionStart = null;
        OnActionEnd = null;

        OnClashStart = null;
        OnClashWin = null;
        OnClashLose = null;
        OnClashResolved = null;

        OnDamageTaken = null;
        OnDamageDealt = null;
        OnDamageResolved = null;
        OnDamageEventResolved = null;

        OnCombatResourceChanged = null;

        OnStatusApplied = null;
        OnStatusRemoved = null;
        OnStatusApplyResolved = null;
        OnStatusTicked = null;
        OnStatusRemovedDetailed = null;

        OnBodyPartWeakened = null;
        OnBodyPartDestroyed = null;
        OnBodyPartRecovered = null;
        OnBodyPartWeakenResolved = null;
        OnBodyPartBreakResolved = null;

        OnBodyPartStatusApplied = null;
        OnBodyPartStatusRemoved = null;

        OnCharacterDeath = null;
        OnKill = null;
        OnCharacterDeathResolved = null;
        OnKillResolved = null;
    }

    //-----------------------------------
    // Safe invocation
    //-----------------------------------

    private static void InvokeSafely(
        Action handlers,
        string eventName)
    {
        if (handlers == null)
            return;

        foreach (Delegate subscriber
                 in handlers.GetInvocationList())
        {
            try
            {
                ((Action)subscriber)();
            }
            catch (Exception exception)
            {
                LogSubscriberException(
                    eventName,
                    subscriber,
                    exception);
            }
        }
    }

    private static void InvokeSafely<T1>(
        Action<T1> handlers,
        T1 arg1,
        string eventName)
    {
        if (handlers == null)
            return;

        foreach (Delegate subscriber
                 in handlers.GetInvocationList())
        {
            try
            {
                ((Action<T1>)subscriber)(arg1);
            }
            catch (Exception exception)
            {
                LogSubscriberException(
                    eventName,
                    subscriber,
                    exception);
            }
        }
    }

    private static void InvokeSafely<T1, T2>(
        Action<T1, T2> handlers,
        T1 arg1,
        T2 arg2,
        string eventName)
    {
        if (handlers == null)
            return;

        foreach (Delegate subscriber
                 in handlers.GetInvocationList())
        {
            try
            {
                ((Action<T1, T2>)subscriber)(
                    arg1,
                    arg2);
            }
            catch (Exception exception)
            {
                LogSubscriberException(
                    eventName,
                    subscriber,
                    exception);
            }
        }
    }

    private static void InvokeSafely<T1, T2, T3>(
        Action<T1, T2, T3> handlers,
        T1 arg1,
        T2 arg2,
        T3 arg3,
        string eventName)
    {
        if (handlers == null)
            return;

        foreach (Delegate subscriber
                 in handlers.GetInvocationList())
        {
            try
            {
                ((Action<T1, T2, T3>)subscriber)(
                    arg1,
                    arg2,
                    arg3);
            }
            catch (Exception exception)
            {
                LogSubscriberException(
                    eventName,
                    subscriber,
                    exception);
            }
        }
    }

    private static void InvokeSafely<T1, T2, T3, T4>(
        Action<T1, T2, T3, T4> handlers,
        T1 arg1,
        T2 arg2,
        T3 arg3,
        T4 arg4,
        string eventName)
    {
        if (handlers == null)
            return;

        foreach (Delegate subscriber
                 in handlers.GetInvocationList())
        {
            try
            {
                ((Action<T1, T2, T3, T4>)subscriber)(
                    arg1,
                    arg2,
                    arg3,
                    arg4);
            }
            catch (Exception exception)
            {
                LogSubscriberException(
                    eventName,
                    subscriber,
                    exception);
            }
        }
    }

    private static void LogSubscriberException(
        string eventName,
        Delegate subscriber,
        Exception exception)
    {
        string targetName =
            subscriber?.Target?.GetType().Name ??
            "STATIC";

        string methodName =
            subscriber?.Method?.Name ??
            "UNKNOWN";

        Debug.LogError(
            "[BattleEvent] 구독자 예외 격리 / " +
            $"Event={eventName}, " +
            $"Target={targetName}, " +
            $"Method={methodName}");

        Debug.LogException(exception);
    }
}