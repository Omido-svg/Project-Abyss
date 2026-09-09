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
    private Coroutine deferredDeathRoutine;
    private Coroutine deferredPartVisualStateRoutine;

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
        StopDeferredDeathRoutine();
        StopDeferredPartVisualStateRoutine();
        Unsubscribe();
    }

    private void OnDestroy()
    {
        StopBindRoutine();
        StopDeferredDeathRoutine();
        StopDeferredPartVisualStateRoutine();
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
        StopDeferredDeathRoutine();
        StopDeferredPartVisualStateRoutine();
        Unsubscribe();
    }

    private void OnBodyPartWeakened(
        BodyPartWeakenEventContext context)
    {
        OnBodyPartChanged(
            context?.Target,
            context?.Part,
            deferActionDamage:
                context?.IsDamageDriven == true &&
                context.SourceAction != null);
    }

    private void OnBodyPartBroken(
        BodyPartBreakEventContext context)
    {
        OnBodyPartChanged(
            context?.Target,
            context?.Part,
            deferActionDamage:
                context?.IsDamageDriven == true &&
                context.SourceAction != null);
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
        BodyPart part,
        bool deferActionDamage = false)
    {
        if (target != character ||
            part == null)
        {
            return;
        }

        Debug.Log(
            $"[ViewEvent] 부위 변화 수신 : " +
            $"{target.name} / {part.Type} / " +
            $"Broken={part.IsBroken} / " +
            $"Deferred={deferActionDamage}");

        if (deferActionDamage)
        {
            ScheduleDeferredPartVisualStateRefresh();
            return;
        }

        characterView?.RefreshVisualState();
    }

    private void ScheduleDeferredPartVisualStateRefresh()
    {
        StopDeferredPartVisualStateRoutine();

        if (!isActiveAndEnabled)
            return;

        deferredPartVisualStateRoutine =
            StartCoroutine(
                RefreshPartVisualStateAfterActionPresentation());
    }

    private IEnumerator RefreshPartVisualStateAfterActionPresentation()
    {
        // Damage event는 ActionResolver가 VisualRequest를 시작하기 전에 동기적으로 발생한다.
        // 한 프레임 양보하면 정상 경로에서는 Director가 presentation read model을 바인딩한다.
        yield return null;

        BattleAnimationDirector director =
            FindFirstObjectByType<BattleAnimationDirector>(
                FindObjectsInactive.Include);

        while (isActiveAndEnabled &&
               director != null &&
               director.IsPlaying)
        {
            yield return null;
        }

        if (isActiveAndEnabled)
            characterView?.RefreshVisualState();

        deferredPartVisualStateRoutine = null;
    }

    private void StopDeferredPartVisualStateRoutine()
    {
        if (deferredPartVisualStateRoutine == null)
            return;

        StopCoroutine(deferredPartVisualStateRoutine);
        deferredPartVisualStateRoutine = null;
    }

    private void OnCharacterDeath(
        KillEventContext context)
    {
        if (context?.Victim != character)
            return;

        // Action 기반 피해는 로직 단계에서 먼저 사망 판정된다.
        // 이 이벤트에서 즉시 Dead를 발동하면 공격 Timeline이 타격하기 전에
        // 대상이 먼저 죽기 때문에, 실제 마지막 Hit Event가 Death 반응을 담당한다.
        if (context.SourceAction != null)
        {
            StopDeferredDeathRoutine();
            deferredDeathRoutine =
                StartCoroutine(
                    PlayDeferredDeathFallback());
            return;
        }

        // 턴 종료 상태이상, 강제 처형처럼 별도 공격 Timeline이 없는 사망은 즉시 표시한다.
        characterView?.PlayDead();
    }

    private IEnumerator PlayDeferredDeathFallback()
    {
        // OnCharacterDeathResolved는 ActionResolver가 VisualRequest를 재생하기 직전에
        // 동기적으로 발생한다. 한 프레임 양보해 Director가 현재 연출을 잡도록 한다.
        yield return null;

        BattleAnimationDirector director =
            FindFirstObjectByType<BattleAnimationDirector>(
                FindObjectsInactive.Include);

        while (isActiveAndEnabled &&
               director != null &&
               director.IsPlaying)
        {
            yield return null;
        }

        if (isActiveAndEnabled &&
            character != null &&
            character.IsDead &&
            characterView != null &&
            !characterView.DeathPresentationStarted)
        {
            // Timeline 누락/취소처럼 Hit Event가 끝내 오지 않은 경우에만
            // 연출 종료 후 안전한 fallback으로 Dead를 한 번 재생한다.
            characterView.PlayDead();
        }

        deferredDeathRoutine = null;
    }

    private void StopDeferredDeathRoutine()
    {
        if (deferredDeathRoutine == null)
            return;

        StopCoroutine(deferredDeathRoutine);
        deferredDeathRoutine = null;
    }
}