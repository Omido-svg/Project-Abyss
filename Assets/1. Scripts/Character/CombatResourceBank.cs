using System.Collections.Generic;
using UnityEngine;

public class CombatResourceBank
{
    private readonly Dictionary<string, int> values = new();

    public int Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return 0;

        return values.TryGetValue(key, out int value)
            ? value
            : 0;
    }

    public void Set(string key, int value)
    {
        if (string.IsNullOrEmpty(key))
            return;

        values[key] = Mathf.Max(0, value);
    }

    public void Add(string key, int amount, int max = int.MaxValue)
    {
        if (string.IsNullOrEmpty(key))
            return;

        int current = Get(key);
        int next = Mathf.Clamp(current + amount, 0, max);

        values[key] = next;
    }

    public int ConsumeAll(string key)
    {
        int current = Get(key);
        values[key] = 0;
        return current;
    }
}