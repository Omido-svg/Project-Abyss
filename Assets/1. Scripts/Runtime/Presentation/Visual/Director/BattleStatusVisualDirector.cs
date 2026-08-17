using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleStatusVisualDirector : MonoBehaviour
{
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

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        if (queueRoutine != null)
            StopCoroutine(queueRoutine);

        queueRoutine = null;
        queue.Clear();
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