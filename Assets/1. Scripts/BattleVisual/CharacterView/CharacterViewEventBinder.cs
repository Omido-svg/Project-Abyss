using System;
using System.Collections;
using UnityEngine;

public class CharacterViewEventBinder :
    MonoBehaviour,
    IBattleEventListener
{
    [Header("References")]
    [SerializeField] private Character character;
    [SerializeField] private CharacterView characterView;

    private readonly BattleEventSubscriptionGroup subscriptions = new();

    private BattleEvent battleEvent;
    private Coroutine bindRoutine;

    public bool IsSubscribed =>
        battleEvent != null &&
        subscriptions.Count > 0;

    private void Reset()
    {
        character = GetComponent<Character>();
        characterView = GetComponent<CharacterView>();
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

    private IEnumerator BindWhenReady()
    {
        try
        {
            while (isActiveAndEnabled &&
                   character != null)
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
            character?.BattleContext?._battleEvent;

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
            subscriptions.Subscribe(
                () => nextBattleEvent.OnBattleEnded += HandleBattleEnded,
                () => nextBattleEvent.OnBattleEnded -= HandleBattleEnded,
                "OnBattleEnded");

            subscriptions.Subscribe(
                () => nextBattleEvent.OnBodyPartWeakenResolved += OnBodyPartWeakened,
                () => nextBattleEvent.OnBodyPartWeakenResolved -= OnBodyPartWeakened,
                "OnBodyPartWeakenResolved");

            subscriptions.Subscribe(
                () => nextBattleEvent.OnBodyPartBreakResolved += OnBodyPartBroken,
                () => nextBattleEvent.OnBodyPartBreakResolved -= OnBodyPartBroken,
                "OnBodyPartBreakResolved");

            subscriptions.Subscribe(
                () => nextBattleEvent.OnBodyPartRecovered += OnBodyPartRecovered,
                () => nextBattleEvent.OnBodyPartRecovered -= OnBodyPartRecovered,
                "OnBodyPartRecovered");

            subscriptions.Subscribe(
                () => nextBattleEvent.OnCharacterDeathResolved += OnCharacterDeath,
                () => nextBattleEvent.OnCharacterDeathResolved -= OnCharacterDeath,
                "OnCharacterDeathResolved");

            characterView?.RefreshVisualState();

            Debug.Log(
                $"{name} CharacterViewEventBinder 연결 완료");
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[{nameof(CharacterViewEventBinder)}] 구독 실패 / Object={name}");

            Debug.LogException(exception);
            Unsubscribe();
        }
    }

    private void ResolveReferences()
    {
        if (character == null)
            character = GetComponent<Character>();

        if (characterView == null)
            characterView = GetComponent<CharacterView>();
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
        Unsubscribe();
    }

    private void OnBodyPartWeakened(
        BodyPartWeakenEventContext context)
    {
        OnBodyPartChanged(
            context?.Target,
            context?.Part);
    }

    private void OnBodyPartBroken(
        BodyPartBreakEventContext context)
    {
        OnBodyPartChanged(
            context?.Target,
            context?.Part);
    }

    private void OnBodyPartRecovered(
        Character target,
        BodyPart part)
    {
        OnBodyPartChanged(
            target,
            part);
    }

    private void OnBodyPartChanged(
        Character target,
        BodyPart part)
    {
        if (target != character ||
            part == null)
        {
            return;
        }

        Debug.Log(
            $"[ViewEvent] 부위 변화 수신 : " +
            $"{target.name} / {part.Type} / " +
            $"Broken={part.IsBroken}");

        characterView?.RefreshVisualState();
    }

    private void OnCharacterDeath(
        KillEventContext context)
    {
        if (context?.Victim != character)
            return;

        characterView?.PlayDead();
    }
}
