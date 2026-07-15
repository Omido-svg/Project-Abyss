using System.Collections.Generic;
using UnityEngine;

public sealed class BattleVfxPool : MonoBehaviour
{
    private readonly Dictionary<BattleVfxPoolKey, Stack<BattleVfxInstance>> pools = new();
    private readonly HashSet<BattleVfxInstance> activeInstances = new();

    private bool isShuttingDown;

    public static BattleVfxPool GetOrCreate()
    {
        BattleVfxPool existing = FindFirstObjectByType<BattleVfxPool>();

        if (existing != null)
            return existing;

        GameObject root = new GameObject("Battle VFX Pool");
        return root.AddComponent<BattleVfxPool>();
    }

    public BattleVfxInstance Acquire(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        int maxPoolSize)
    {
        if (prefab == null || isShuttingDown)
            return null;

        BattleVfxPoolKey key = new BattleVfxPoolKey(prefab);
        BattleVfxInstance instance = TakeAvailable(key);

        if (instance == null)
        {
            GameObject created = Instantiate(prefab);
            instance = created.GetComponent<BattleVfxInstance>();

            if (instance == null)
                instance = created.AddComponent<BattleVfxInstance>();

            instance.Configure(this, key, maxPoolSize);
        }

        instance.Configure(this, key, maxPoolSize);
        activeInstances.Add(instance);
        instance.PrepareForUse(position, rotation, parent);
        return instance;
    }

    public void Prewarm(
        GameObject prefab,
        int count,
        int maxPoolSize)
    {
        if (prefab == null || count <= 0 || isShuttingDown)
            return;

        BattleVfxPoolKey key = new BattleVfxPoolKey(prefab);
        Stack<BattleVfxInstance> stack = GetStack(key);
        int targetCount = Mathf.Min(Mathf.Max(0, maxPoolSize), count);
        int totalCount = stack.Count + CountActive(key);

        while (totalCount < targetCount)
        {
            GameObject created = Instantiate(prefab, transform);
            BattleVfxInstance instance = created.GetComponent<BattleVfxInstance>();

            if (instance == null)
                instance = created.AddComponent<BattleVfxInstance>();

            instance.Configure(this, key, maxPoolSize);
            instance.ResetForPool(transform);
            stack.Push(instance);
            totalCount++;
        }
    }

    internal void Return(BattleVfxInstance instance, int maxPoolSize)
    {
        if (instance == null)
            return;

        activeInstances.Remove(instance);

        if (isShuttingDown || maxPoolSize <= 0 || !instance.PoolKey.IsValid)
        {
            instance.DestroyImmediately();
            return;
        }

        Stack<BattleVfxInstance> stack = GetStack(instance.PoolKey);

        if (stack.Count >= maxPoolSize)
        {
            instance.DestroyImmediately();
            return;
        }

        instance.ResetForPool(transform);
        stack.Push(instance);
    }


    internal void NotifyDestroyed(BattleVfxInstance instance)
    {
        if (instance == null)
            return;

        activeInstances.Remove(instance);
    }

    public void ReleaseAllActive()
    {
        if (activeInstances.Count == 0)
            return;

        BattleVfxInstance[] snapshot = new BattleVfxInstance[activeInstances.Count];
        activeInstances.CopyTo(snapshot);

        foreach (BattleVfxInstance instance in snapshot)
            instance?.Release();
    }


    private int CountActive(BattleVfxPoolKey key)
    {
        int count = 0;

        foreach (BattleVfxInstance instance in activeInstances)
        {
            if (instance != null && instance.PoolKey == key)
                count++;
        }

        return count;
    }

    private BattleVfxInstance TakeAvailable(BattleVfxPoolKey key)
    {
        if (!pools.TryGetValue(key, out Stack<BattleVfxInstance> stack))
            return null;

        while (stack.Count > 0)
        {
            BattleVfxInstance instance = stack.Pop();

            if (instance != null)
                return instance;
        }

        return null;
    }

    private Stack<BattleVfxInstance> GetStack(BattleVfxPoolKey key)
    {
        if (!pools.TryGetValue(key, out Stack<BattleVfxInstance> stack))
        {
            stack = new Stack<BattleVfxInstance>();
            pools.Add(key, stack);
        }

        return stack;
    }

    private void OnDestroy()
    {
        isShuttingDown = true;

        BattleVfxInstance[] activeSnapshot =
            new BattleVfxInstance[activeInstances.Count];

        activeInstances.CopyTo(activeSnapshot);
        activeInstances.Clear();

        foreach (BattleVfxInstance instance in activeSnapshot)
        {
            if (instance != null)
                Destroy(instance.gameObject);
        }

        foreach (Stack<BattleVfxInstance> stack in pools.Values)
        {
            while (stack.Count > 0)
            {
                BattleVfxInstance instance = stack.Pop();

                if (instance != null)
                    Destroy(instance.gameObject);
            }
        }

        pools.Clear();
    }
}
