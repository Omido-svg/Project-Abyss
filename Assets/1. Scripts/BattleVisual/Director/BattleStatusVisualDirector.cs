using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleStatusVisualDirector : MonoBehaviour
{
    [SerializeField] private StatusEffectVisualDatabase visualDatabase;
    [SerializeField] private BattleVfxManager vfxManager;
    [SerializeField] private DamageNumberManager damageNumberManager;

    [Header("Timing")]
    [SerializeField] private float delayBetweenTicks = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private Coroutine queueRoutine;
    private readonly Queue<StatusDamageVisualRequest> queue = new();

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
        if (request == null || !isActiveAndEnabled)
            return;

        queue.Enqueue(request);

        if (queueRoutine == null)
            queueRoutine = StartCoroutine(ProcessQueue());
    }

    public void ShowStatusLifecycle(
        StatusEffectLifecycleVisualRequest request)
    {
        if (request == null ||
            request.Target == null ||
            !isActiveAndEnabled)
        {
            return;
        }

        ResolveReferences();

        StatusEffectVisualDefinition visual =
            visualDatabase?.GetVisual(request.StatusKey);

        if (visual == null)
            return;

        BattleVfxDefinition definition =
            request.Phase switch
            {
                StatusEffectVisualPhase.Applied => visual.ApplyVfx,
                StatusEffectVisualPhase.Refreshed => visual.ApplyVfx,
                StatusEffectVisualPhase.Stacked => visual.ApplyVfx,
                StatusEffectVisualPhase.Removed => visual.RemoveVfx,
                StatusEffectVisualPhase.Expired => visual.RemoveVfx,
                _ => null
            };

        if (definition == null || vfxManager == null)
            return;

        CharacterView targetView =
            BattleCameraTargetResolver.GetView(request.Target);

        vfxManager.PlayVfx(
            definition,
            request.TargetPart != null
                ? BattleVfxAnchorType.TargetBodyPart
                : BattleVfxAnchorType.TargetRoot,
            new BattleVfxContext
            {
                Target = request.Target,
                TargetView = targetView,
                TargetPart = request.TargetPart,
                Damage = 0
            });

        if (logDebug)
        {
            Debug.Log(
                $"[BattleStatusVisualDirector] Lifecycle VFX / " +
                $"Status={request.StatusKey}, Phase={request.Phase}, " +
                $"Target={request.Target.Data?.CharacterName}, " +
                $"Part={(request.TargetPart == null ? "NONE" : request.TargetPart.Type.ToString())}");
        }
    }

    private IEnumerator ProcessQueue()
    {
        try
        {
            while (queue.Count > 0)
            {
                PlayDamage(queue.Dequeue());

                if (delayBetweenTicks > 0f)
                    yield return new WaitForSeconds(delayBetweenTicks);
                else
                    yield return null;
            }
        }
        finally
        {
            queueRoutine = null;
        }
    }

    private void PlayDamage(StatusDamageVisualRequest request)
    {
        if (request?.Target == null)
            return;

        ResolveReferences();

        StatusEffectVisualDefinition visual =
            visualDatabase?.GetVisual(request.StatusKey);

        if (visual == null)
            return;

        CharacterView targetView =
            BattleCameraTargetResolver.GetView(request.Target);

        if (targetView != null &&
            damageNumberManager != null &&
            request.Damage > 0)
        {
            Vector3 position =
                targetView.GetDamageNumberPosition(
                    request.TargetPart);

            damageNumberManager.ShowDamage(
                position,
                request.Damage,
                visual.DamageNumberColor);
        }

        if (visual.TickDamageVfx != null &&
            vfxManager != null)
        {
            vfxManager.PlayVfx(
                visual.TickDamageVfx,
                request.TargetPart != null
                    ? BattleVfxAnchorType.TargetBodyPart
                    : BattleVfxAnchorType.TargetRoot,
                new BattleVfxContext
                {
                    Target = request.Target,
                    TargetView = targetView,
                    TargetPart = request.TargetPart,
                    Damage = request.Damage
                });
        }

        if (logDebug)
        {
            Debug.Log(
                $"[BattleStatusVisualDirector] Tick VFX / " +
                $"Status={request.StatusKey}, Damage={request.Damage}, " +
                $"Target={request.Target.Data?.CharacterName}");
        }
    }

    private void ResolveReferences()
    {
        if (vfxManager == null)
            vfxManager = FindFirstObjectByType<BattleVfxManager>();

        if (damageNumberManager == null)
            damageNumberManager = FindFirstObjectByType<DamageNumberManager>();
    }
}
