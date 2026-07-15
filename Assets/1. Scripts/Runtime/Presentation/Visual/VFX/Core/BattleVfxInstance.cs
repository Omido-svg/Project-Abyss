using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public sealed class BattleVfxInstance : MonoBehaviour
{
    private readonly List<ParticleSystem> particles = new();
    private readonly List<VisualEffect> visualEffects = new();
    private readonly List<MonoBehaviour> lifecycleBehaviours = new();

    private BattleVfxPool ownerPool;
    private BattleVfxPoolKey poolKey;
    private int maxPoolSize;
    private Coroutine releaseRoutine;
    private bool released = true;

    public GameObject Instance => gameObject;
    public BattleVfxPoolKey PoolKey => poolKey;
    public bool IsReleased => released;

    internal void Configure(
        BattleVfxPool pool,
        BattleVfxPoolKey key,
        int poolLimit)
    {
        ownerPool = pool;
        poolKey = key;
        maxPoolSize = Mathf.Max(0, poolLimit);
        CacheComponents();
    }

    internal void PrepareForUse(
        Vector3 position,
        Quaternion rotation,
        Transform parent)
    {
        CancelScheduledRelease();
        released = false;

        transform.SetParent(parent, true);
        transform.SetPositionAndRotation(position, rotation);
        gameObject.SetActive(true);

        foreach (MonoBehaviour behaviour in lifecycleBehaviours)
        {
            if (behaviour is IBattleVfxPoolLifecycle lifecycle)
                lifecycle.OnBattleVfxTakenFromPool();
        }
    }

    public void Release()
    {
        if (released)
            return;

        CancelScheduledRelease();
        released = true;
        StopEffects();

        foreach (MonoBehaviour behaviour in lifecycleBehaviours)
        {
            if (behaviour is IBattleVfxPoolLifecycle lifecycle)
                lifecycle.OnBattleVfxReturnedToPool();
        }

        if (ownerPool != null)
        {
            ownerPool.Return(this, maxPoolSize);
            return;
        }

        Destroy(gameObject);
    }

    public void ReleaseAfter(float delay, bool useUnscaledTime = false)
    {
        CancelScheduledRelease();

        if (delay <= 0f)
        {
            Release();
            return;
        }

        releaseRoutine = StartCoroutine(
            ReleaseRoutine(delay, useUnscaledTime));
    }

    internal void ResetForPool(Transform poolRoot)
    {
        CancelScheduledRelease();
        transform.SetParent(poolRoot, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        gameObject.SetActive(false);
    }

    internal void DestroyImmediately()
    {
        CancelScheduledRelease();
        Destroy(gameObject);
    }


    private void OnDestroy()
    {
        CancelScheduledRelease();

        if (ownerPool != null)
            ownerPool.NotifyDestroyed(this);
    }

    private IEnumerator ReleaseRoutine(float delay, bool useUnscaledTime)
    {
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(delay);
        else
            yield return new WaitForSeconds(delay);

        releaseRoutine = null;
        Release();
    }

    private void CancelScheduledRelease()
    {
        if (releaseRoutine == null)
            return;

        StopCoroutine(releaseRoutine);
        releaseRoutine = null;
    }

    private void CacheComponents()
    {
        particles.Clear();
        visualEffects.Clear();
        lifecycleBehaviours.Clear();

        GetComponentsInChildren(true, particles);
        GetComponentsInChildren(true, visualEffects);
        GetComponentsInChildren(true, lifecycleBehaviours);
    }

    private void StopEffects()
    {
        foreach (ParticleSystem particle in particles)
        {
            if (particle == null)
                continue;

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Clear(true);
        }

        foreach (VisualEffect visualEffect in visualEffects)
        {
            if (visualEffect == null)
                continue;

            visualEffect.Stop();
            visualEffect.Reinit();
        }
    }
}
