using System;
using System.Collections;
using UnityEngine;

public sealed class BattleUIEventBinder :
    MonoBehaviour,
    IBattleEventListener
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private BattleUIRefreshScheduler refreshScheduler;

    private readonly BattleEventSubscriptionGroup subscriptions = new();

    private BattleEvent battleEvent;
    private Coroutine bindRoutine;

    public bool IsSubscribed =>
        battleEvent != null &&
        subscriptions.Count > 0;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        StopBindRoutine();
        Unsubscribe();
    }

    private void OnDestroy()
    {
        StopBindRoutine();
        Unsubscribe();
        subscriptions.Dispose();
    }

    public void Subscribe()
    {
        if (!isActiveAndEnabled)
            return;

        ResolveReferences();

        if (TryBindCurrentEvent())
            return;

        if (bindRoutine == null)
        {
            bindRoutine =
                StartCoroutine(
                    BindWhenReady());
        }
    }

    public void Unsubscribe()
    {
        subscriptions.Clear();
        battleEvent = null;
    }

    // 기존 외부 호출부 호환.
    public void Unbind()
    {
        Unsubscribe();
    }

    private IEnumerator BindWhenReady()
    {
        try
        {
            while (isActiveAndEnabled)
            {
                ResolveReferences();

                if (TryBindCurrentEvent())
                    yield break;

                yield return null;
            }
        }
        finally
        {
            bindRoutine = null;
        }
    }

    private bool TryBindCurrentEvent()
    {
        BattleEvent current =
            battleManager?.BattleContext?._battleEvent;

        if (current == null ||
            current.IsDisposed)
        {
            return false;
        }

        if (ReferenceEquals(current, battleEvent) &&
            IsSubscribed)
        {
            return true;
        }

        Bind(current);
        return IsSubscribed;
    }

    private void Bind(BattleEvent nextBattleEvent)
    {
        Unsubscribe();

        if (nextBattleEvent == null ||
            nextBattleEvent.IsDisposed)
        {
            return;
        }

        battleEvent = nextBattleEvent;

        try
        {
            SubscribeBattleEvents(
                nextBattleEvent);

            Debug.Log(
                "[BattleUIEventBinder] BattleEvent subscribed");
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[BattleUIEventBinder] BattleEvent 구독 실패");

            Debug.LogException(exception);
            Unsubscribe();
        }
    }

    private void SubscribeBattleEvents(
        BattleEvent source)
    {
        subscriptions.Subscribe(
            () => source.OnBattleEnded += HandleBattleEnded,
            () => source.OnBattleEnded -= HandleBattleEnded,
            "OnBattleEnded");

        subscriptions.Subscribe(
            () => source.OnTurnStart += HandleTurnStart,
            () => source.OnTurnStart -= HandleTurnStart,
            "OnTurnStart");

        subscriptions.Subscribe(
            () => source.OnTurnEnd += HandleTurnEnd,
            () => source.OnTurnEnd -= HandleTurnEnd,
            "OnTurnEnd");

        subscriptions.Subscribe(
            () => source.OnActionStart += HandleActionChanged,
            () => source.OnActionStart -= HandleActionChanged,
            "OnActionStart");

        subscriptions.Subscribe(
            () => source.OnActionEnd += HandleActionChanged,
            () => source.OnActionEnd -= HandleActionChanged,
            "OnActionEnd");

        subscriptions.Subscribe(
            () => source.OnDamageEventResolved += HandleDamageEventResolved,
            () => source.OnDamageEventResolved -= HandleDamageEventResolved,
            "OnDamageEventResolved");

        subscriptions.Subscribe(
            () => source.OnStatusApplyResolved += HandleStatusApplied,
            () => source.OnStatusApplyResolved -= HandleStatusApplied,
            "OnStatusApplyResolved");

        subscriptions.Subscribe(
            () => source.OnStatusTicked += HandleStatusTicked,
            () => source.OnStatusTicked -= HandleStatusTicked,
            "OnStatusTicked");

        subscriptions.Subscribe(
            () => source.OnStatusRemovedDetailed += HandleStatusRemoved,
            () => source.OnStatusRemovedDetailed -= HandleStatusRemoved,
            "OnStatusRemovedDetailed");

        subscriptions.Subscribe(
            () => source.OnBodyPartWeakenResolved += HandlePartWeakened,
            () => source.OnBodyPartWeakenResolved -= HandlePartWeakened,
            "OnBodyPartWeakenResolved");

        subscriptions.Subscribe(
            () => source.OnBodyPartBreakResolved += HandlePartBroken,
            () => source.OnBodyPartBreakResolved -= HandlePartBroken,
            "OnBodyPartBreakResolved");

        subscriptions.Subscribe(
            () => source.OnBodyPartRecovered += HandlePartRecovered,
            () => source.OnBodyPartRecovered -= HandlePartRecovered,
            "OnBodyPartRecovered");

        subscriptions.Subscribe(
            () => source.OnCharacterDeathResolved += HandleCharacterDeath,
            () => source.OnCharacterDeathResolved -= HandleCharacterDeath,
            "OnCharacterDeathResolved");

        subscriptions.Subscribe(
            () => source.OnClashResolved += HandleClashResolved,
            () => source.OnClashResolved -= HandleClashResolved,
            "OnClashResolved");
    }

    private void ResolveReferences()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (uiManager == null)
            uiManager = FindFirstObjectByType<BattleUIManager>();

        if (refreshScheduler == null)
        {
            refreshScheduler =
                FindFirstObjectByType<BattleUIRefreshScheduler>();
        }
    }

    private void StopBindRoutine()
    {
        if (bindRoutine == null)
            return;

        StopCoroutine(bindRoutine);
        bindRoutine = null;
    }

    private void HandleBattleEnded()
    {
        refreshScheduler?.MarkAllDirty();
        Unsubscribe();
    }

    private void HandleTurnStart(int turn)
    {
        uiManager?.ResetTurnInputState();
        refreshScheduler?.MarkAllDirty();
    }

    private void HandleTurnEnd(int turn)
    {
        refreshScheduler?.MarkAllDirty();
    }

    private void HandleActionChanged(BattleAction action)
    {
        refreshScheduler?.MarkActionDirty(action);
    }

    private void HandleDamageEventResolved(
        DamageEventResult result)
    {
        DamageContext context = result?.Context;

        if (context == null)
        {
            refreshScheduler?.MarkAllDirty();
            return;
        }

        refreshScheduler?.MarkBodyPartDirty(
            context.Target,
            context.TargetPart);

        refreshScheduler?.MarkCharacterDirty(
            context.Attacker);
    }

    private void HandleStatusApplied(
        StatusEffectApplyResult result)
    {
        refreshScheduler?.MarkBodyPartDirty(
            result?.TargetCharacter,
            result?.TargetPart);
    }

    private void HandleStatusTicked(
        StatusEffectTickContext context)
    {
        refreshScheduler?.MarkBodyPartDirty(
            context?.TargetCharacter,
            context?.TargetPart);
    }

    private void HandleStatusRemoved(
        Character target,
        BodyPart part,
        StatusEffect effect,
        StatusEffectRemoveReason reason)
    {
        refreshScheduler?.MarkBodyPartDirty(
            target,
            part);
    }

    private void HandlePartWeakened(
        BodyPartWeakenEventContext context)
    {
        refreshScheduler?.MarkBodyPartDirty(
            context?.Target,
            context?.Part);
    }

    private void HandlePartBroken(
        BodyPartBreakEventContext context)
    {
        refreshScheduler?.MarkBodyPartDirty(
            context?.Target,
            context?.Part);

        refreshScheduler?.MarkAllDirty();
    }

    private void HandlePartRecovered(
        Character target,
        BodyPart part)
    {
        refreshScheduler?.MarkBodyPartDirty(
            target,
            part);

        refreshScheduler?.MarkAllDirty();
    }

    private void HandleCharacterDeath(
        KillEventContext context)
    {
        refreshScheduler?.MarkCharacterDirty(
            context?.Victim);

        refreshScheduler?.MarkAllDirty();
    }

    private void HandleClashResolved(
        ClashResultContext context)
    {
        refreshScheduler?.MarkAllDirty();
    }
}
