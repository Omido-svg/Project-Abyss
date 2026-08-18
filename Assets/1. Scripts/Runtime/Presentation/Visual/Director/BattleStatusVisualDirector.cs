using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleStatusVisualDirector : MonoBehaviour
{
    [Header("Battle Event Source")]
    [SerializeField] private BattleManager battleManager;

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

        if (queueRoutine != null)
            StopCoroutine(queueRoutine);

        queueRoutine = null;
        queue.Clear();
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
                    yield return new WaitForSecondsRealtime(delayBetweenRequests);
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
        if (vfxManager == null)
            vfxManager = FindFirstObjectByType<BattleVfxManager>();

        if (damageNumberManager == null)
            damageNumberManager = FindFirstObjectByType<DamageNumberManager>();
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