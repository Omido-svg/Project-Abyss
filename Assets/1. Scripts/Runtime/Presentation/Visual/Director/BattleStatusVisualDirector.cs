using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleStatusVisualDirector : MonoBehaviour
{
    [Header("Battle Event Source")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleAnimationDirector actionDirector;

    [SerializeField] private StatusEffectVisualDatabase visualDatabase;
    [SerializeField] private BattleVfxManager vfxManager;
    [SerializeField] private DamageNumberManager damageNumberManager;

    [Header("Timing")]
    [SerializeField] private float delayBetweenRequests = 0.15f;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private Coroutine queueRoutine;
    private readonly Queue<QueuedStatusVisual> queue = new();
    private BattleEvent boundBattleEvent;
    private BattleAnimationDirector boundActionDirector;

    private readonly Dictionary<BattleAction, HashSet<int>>
        presentedExchanges = new();

    private readonly HashSet<BattleAction>
        completedActions = new();

    private bool IsPresentationSuppressed =>
        battleManager?.BattleContext?.SuppressPresentation == true;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        BindBattleEventSource();
    }

    private void Start()
    {
        // BattleManager가 이 컴포넌트보다 늦게 Initialize되는 씬도 허용한다.
        BindBattleEventSource();
    }

    private void OnDisable()
    {
        UnbindBattleEventSource();
        BindActionDirector(null);

        if (queueRoutine != null)
            StopCoroutine(queueRoutine);

        queueRoutine = null;
        queue.Clear();
        presentedExchanges.Clear();
        completedActions.Clear();
    }


    private void BindBattleEventSource()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (battleManager == null)
            return;

        battleManager.BattlePrepared -= HandleBattlePrepared;
        battleManager.BattlePrepared += HandleBattlePrepared;

        BindBattleEvent(
            battleManager.BattleContext?._battleEvent);
    }

    private void UnbindBattleEventSource()
    {
        if (battleManager != null)
            battleManager.BattlePrepared -= HandleBattlePrepared;

        BindBattleEvent(null);
    }

    private void HandleBattlePrepared(BattleContext context)
    {
        BindBattleEvent(context?._battleEvent);
    }

    private void BindBattleEvent(BattleEvent source)
    {
        if (ReferenceEquals(boundBattleEvent, source))
            return;

        if (boundBattleEvent != null)
        {
            boundBattleEvent.OnStatusApplyResolved -=
                HandleStatusApplied;
            boundBattleEvent.OnStatusTicked -=
                HandleStatusTicked;
            boundBattleEvent.OnStatusRemovedDetailed -=
                HandleStatusRemoved;
        }

        boundBattleEvent = source;

        if (boundBattleEvent == null ||
            boundBattleEvent.IsDisposed)
        {
            boundBattleEvent = null;
            return;
        }

        boundBattleEvent.OnStatusApplyResolved +=
            HandleStatusApplied;
        boundBattleEvent.OnStatusTicked +=
            HandleStatusTicked;
        boundBattleEvent.OnStatusRemovedDetailed +=
            HandleStatusRemoved;
    }

    private void HandleStatusApplied(
        StatusEffectApplyResult result)
    {
        if (IsPresentationSuppressed ||
            result?.Effect == null ||
            result.TargetCharacter == null ||
            result.Kind == StatusEffectApplyKind.Ignored ||
            result.Kind == StatusEffectApplyKind.Rejected ||
            result.WasTransferred)
        {
            return;
        }

        StatusEffectVisualPhase phase =
            result.Kind switch
            {
                StatusEffectApplyKind.Stacked =>
                    StatusEffectVisualPhase.Stacked,
                StatusEffectApplyKind.Refreshed =>
                    StatusEffectVisualPhase.Refreshed,
                _ =>
                    StatusEffectVisualPhase.Applied
            };

        ShowStatusLifecycle(
            new StatusEffectLifecycleVisualRequest
            {
                Source = result.Effect.Source,
                Target = result.TargetCharacter,
                TargetPart = result.TargetPart,
                SourceAction = result.SourceAction,
                SourceExchangeIndex = result.SourceExchangeIndex,
                SourceEffectTiming = result.SourceEffectTiming,
                HasSourceEffectTiming = result.HasSourceEffectTiming,
                StatusKey = result.Effect.EffectName,
                Phase = phase,
                Stack = result.Effect.Stack,
                Duration = result.Effect.Duration
            });
    }

    private void HandleStatusTicked(
        StatusEffectTickContext context)
    {
        if (IsPresentationSuppressed ||
            context?.SourceEffect == null ||
            context.TargetCharacter == null ||
            context.AppliedDamage <= 0)
        {
            return;
        }

        ShowStatusDamage(
            new StatusDamageVisualRequest
            {
                Source = context.SourceEffect.Source,
                Target = context.TargetCharacter,
                TargetPart = context.TargetPart,
                Damage = context.AppliedDamage,
                StatusKey = context.SourceEffect.EffectName,
                DamageContext = context.DamageContext
            });
    }

    private void HandleStatusRemoved(
        Character target,
        BodyPart part,
        StatusEffect effect,
        StatusEffectRemoveReason reason)
    {
        if (IsPresentationSuppressed ||
            target == null ||
            effect == null ||
            reason == StatusEffectRemoveReason.Transferred)
        {
            return;
        }

        ShowStatusLifecycle(
            new StatusEffectLifecycleVisualRequest
            {
                Source = effect.Source,
                Target = target,
                TargetPart = part,
                StatusKey = effect.EffectName,
                Phase = reason == StatusEffectRemoveReason.Expired
                    ? StatusEffectVisualPhase.Expired
                    : StatusEffectVisualPhase.Removed,
                Stack = effect.Stack,
                Duration = effect.Duration,
                RemoveReason = reason
            });
    }

    public void ShowStatusDamage(StatusDamageVisualRequest request)
    {
        if (request == null || request.Target == null || !isActiveAndEnabled)
            return;

        queue.Enqueue(QueuedStatusVisual.ForDamage(request));
        EnsureQueueRoutine();
    }

    public void ShowStatusLifecycle(StatusEffectLifecycleVisualRequest request)
    {
        if (request == null || request.Target == null || !isActiveAndEnabled)
            return;

        queue.Enqueue(QueuedStatusVisual.ForLifecycle(request));
        EnsureQueueRoutine();
    }

    private void EnsureQueueRoutine()
    {
        if (queueRoutine == null)
            queueRoutine = StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        try
        {
            while (queue.Count > 0)
            {
                // 상태 이벤트는 전투 로직에서 Timeline보다 먼저 발생할 수 있다.
                // SourceAction/Exchange가 있으면 해당 교환의 첫 실제 HitFrame까지 기다리고,
                // 상관관계가 없는 상태는 기존 action-end 안전 fallback을 사용한다.
                QueuedStatusVisual nextVisual = queue.Peek();
                yield return WaitForPresentationWindow(nextVisual);

                QueuedStatusVisual visual = queue.Dequeue();

                if (visual.DamageRequest != null &&
                    visual.DamageRequest.Target != null)
                {
                    PlayDamage(visual.DamageRequest);
                }
                else if (visual.LifecycleRequest != null &&
                         visual.LifecycleRequest.Target != null)
                {
                    PlayLifecycle(visual.LifecycleRequest);
                }

                if (delayBetweenRequests <= 0f)
                {
                    yield return null;
                }
                else if (useUnscaledTime)
                {
                    yield return
                        BattlePlaybackSpeedController
                            .WaitForBattleUnscaledSeconds(delayBetweenRequests);
                }
                else
                {
                    yield return new WaitForSeconds(delayBetweenRequests);
                }
            }
        }
        finally
        {
            queueRoutine = null;

            if (actionDirector == null ||
                !actionDirector.IsPlaying)
            {
                presentedExchanges.Clear();
                completedActions.Clear();
            }
        }
    }

    private IEnumerator WaitForPresentationWindow(
        QueuedStatusVisual visual)
    {
        BattleAction sourceAction =
            visual?.LifecycleRequest?.SourceAction;

        int sourceExchangeIndex =
            visual?.LifecycleRequest?.SourceExchangeIndex ?? -1;

        if (sourceAction == null ||
            sourceExchangeIndex < 0)
        {
            yield return WaitForActionPresentationWindow();
            yield break;
        }

        // 논리 결과 발행과 Director.Play 시작이 같은 프레임에 이어질 수 있다.
        yield return null;
        ResolveReferences();

        const int startupGraceFrames = 2;
        int remainingGraceFrames = startupGraceFrames;

        while (isActiveAndEnabled)
        {
            if (HasPresentedExchange(
                    sourceAction,
                    sourceExchangeIndex) ||
                completedActions.Contains(sourceAction))
            {
                yield break;
            }

            if (actionDirector != null &&
                actionDirector.IsPlaying)
            {
                yield return null;
                continue;
            }

            if (remainingGraceFrames-- > 0)
            {
                yield return null;
                ResolveReferences();
                continue;
            }

            // Timeline/VisualRequest가 없는 효과는 영원히 대기하지 않는다.
            yield break;
        }
    }

    private IEnumerator WaitForActionPresentationWindow()
    {
        // 이벤트 발행과 Director.Play 시작이 같은 프레임에 이어질 수 있으므로
        // 먼저 한 프레임 양보해 현재 action presentation을 관찰한다.
        yield return null;

        ResolveReferences();

        while (isActiveAndEnabled &&
               actionDirector != null &&
               actionDirector.IsPlaying)
        {
            yield return null;
        }
    }

    private void PlayLifecycle(StatusEffectLifecycleVisualRequest request)
    {
        if (request == null || request.Target == null)
            return;

        Character target = request.Target;

        ResolveReferences();

        StatusEffectVisualDefinition visual =
            visualDatabase?.GetVisual(request.StatusKey);

        if (visual == null)
            return;

        CharacterPersistentVfxController persistentController =
            target.GetComponentInChildren<CharacterPersistentVfxController>(true);

        bool isApplyLike =
            request.Phase == StatusEffectVisualPhase.Applied ||
            request.Phase == StatusEffectVisualPhase.Refreshed ||
            request.Phase == StatusEffectVisualPhase.Stacked;

        if (visual.PersistentVfx != null && persistentController != null)
        {
            if (isApplyLike)
                persistentController.Play(visual.PersistentVfx);
            else
                persistentController.Stop(visual.PersistentVfx);
        }

        BattleVfxDefinition definition = visual.GetLifecycleVfx(request.Phase);

        if (definition != null && vfxManager != null)
        {
            CharacterView targetView =
                BattleCameraTargetResolver.GetView(target);

            BattleVfxContext context = BattleVfxContext.ForStatus(
                target,
                request.TargetPart,
                request.StatusKey,
                request.Phase,
                damage: 0,
                request.DamageContext);

            context.Attacker = request.Source ?? context.Attacker;
            context.TargetView = targetView;

            vfxManager.PlayVfx(
                definition,
                request.TargetPart != null
                    ? BattleVfxAnchorType.TargetBodyPart
                    : BattleVfxAnchorType.TargetRoot,
                context);
        }

        if (logDebug)
        {
            Debug.Log(
                $"[BattleStatusVisualDirector] Lifecycle / " +
                $"Status={request.StatusKey}, Phase={request.Phase}, " +
                $"Target={target.Data?.CharacterName}, " +
                $"Part={(request.TargetPart == null ? "NONE" : request.TargetPart.Type.ToString())}");
        }
    }

    private void PlayDamage(StatusDamageVisualRequest request)
    {
        if (request == null || request.Target == null)
            return;

        Character target = request.Target;

        ResolveReferences();

        StatusEffectVisualDefinition visual =
            visualDatabase?.GetVisual(request.StatusKey);

        if (visual == null)
            return;

        CharacterView targetView =
            BattleCameraTargetResolver.GetView(target);

        Color damageColor = request.HasDamageColorOverride
            ? request.DamageColor
            : visual.DamageNumberColor;

        if (targetView != null &&
            damageNumberManager != null &&
            request.Damage > 0)
        {
            Vector3 position = targetView.GetDamageNumberPosition(request.TargetPart);
            damageNumberManager.ShowDamage(position, request.Damage, damageColor);
        }

        if (visual.TickDamageVfx != null && vfxManager != null)
        {
            BattleVfxContext context = BattleVfxContext.ForStatus(
                target,
                request.TargetPart,
                request.StatusKey,
                StatusEffectVisualPhase.Stacked,
                request.Damage,
                request.DamageContext);

            context.Attacker = request.Source ?? context.Attacker;
            context.TargetView = targetView;

            vfxManager.PlayVfx(
                visual.TickDamageVfx,
                request.TargetPart != null
                    ? BattleVfxAnchorType.TargetBodyPart
                    : BattleVfxAnchorType.TargetRoot,
                context);
        }

        if (logDebug)
        {
            Debug.Log(
                $"[BattleStatusVisualDirector] Tick / " +
                $"Status={request.StatusKey}, Damage={request.Damage}, " +
                $"Target={target.Data?.CharacterName}");
        }
    }

    private void ResolveReferences()
    {
        if (actionDirector == null)
        {
            actionDirector =
                FindFirstObjectByType<BattleAnimationDirector>(
                    FindObjectsInactive.Include);
        }

        if (vfxManager == null)
            vfxManager = FindFirstObjectByType<BattleVfxManager>();

        if (damageNumberManager == null)
            damageNumberManager = FindFirstObjectByType<DamageNumberManager>();

        BindActionDirector(actionDirector);
    }

    private void BindActionDirector(
        BattleAnimationDirector source)
    {
        if (ReferenceEquals(boundActionDirector, source))
            return;

        if (boundActionDirector != null)
        {
            boundActionDirector.HitFramePresented -=
                HandleHitFramePresented;
            boundActionDirector.VisualRequestCompleted -=
                HandleVisualRequestCompleted;
        }

        boundActionDirector = source;

        if (boundActionDirector == null)
            return;

        boundActionDirector.HitFramePresented +=
            HandleHitFramePresented;
        boundActionDirector.VisualRequestCompleted +=
            HandleVisualRequestCompleted;
    }

    private void HandleHitFramePresented(
        BattleAction action,
        int exchangeIndex,
        int hitIndex)
    {
        if (action == null || exchangeIndex < 0)
            return;

        if (!presentedExchanges.TryGetValue(
                action,
                out HashSet<int> exchanges))
        {
            exchanges = new HashSet<int>();
            presentedExchanges[action] = exchanges;
        }

        exchanges.Add(exchangeIndex);
    }

    private void HandleVisualRequestCompleted(
        BattleVisualRequest request)
    {
        if (request == null)
            return;

        MarkActionCompleted(request.SourceAction);

        if (request.ClashExchanges == null)
            return;

        foreach (BattleClashVisualExchange exchange
                 in request.ClashExchanges)
        {
            MarkActionCompleted(
                exchange?.AttackRequest?.SourceAction);
        }
    }

    private void MarkActionCompleted(
        BattleAction action)
    {
        if (action != null)
            completedActions.Add(action);
    }

    private bool HasPresentedExchange(
        BattleAction action,
        int exchangeIndex)
    {
        return action != null &&
               exchangeIndex >= 0 &&
               presentedExchanges.TryGetValue(
                   action,
                   out HashSet<int> exchanges) &&
               exchanges.Contains(exchangeIndex);
    }

    private sealed class QueuedStatusVisual
    {
        public StatusDamageVisualRequest DamageRequest;
        public StatusEffectLifecycleVisualRequest LifecycleRequest;

        public static QueuedStatusVisual ForDamage(StatusDamageVisualRequest request)
        {
            return new QueuedStatusVisual { DamageRequest = request };
        }

        public static QueuedStatusVisual ForLifecycle(StatusEffectLifecycleVisualRequest request)
        {
            return new QueuedStatusVisual { LifecycleRequest = request };
        }
    }
}
