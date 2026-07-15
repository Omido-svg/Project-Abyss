using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CombatResourceDefinition
{
    public string Key;
    public int InitialValue;
    public int MaxValue = int.MaxValue;

    public int SafeMax =>
        Mathf.Max(0, MaxValue);
}

public class CombatResourceBank
{
    private readonly Dictionary<string, int> values = new();
    private readonly Dictionary<string, int> maximums = new();

    public int Count => values.Count;

    public CombatResourceBank()
    {
    }

    public CombatResourceBank(
        IEnumerable<CombatResourceDefinition> definitions)
    {
        Reset(definitions);
    }

    public void Reset(
        IEnumerable<CombatResourceDefinition> definitions = null)
    {
        values.Clear();
        maximums.Clear();

        if (definitions == null)
            return;

        foreach (CombatResourceDefinition definition in definitions)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.Key))
            {
                continue;
            }

            string key = NormalizeKey(definition.Key);
            int max = definition.SafeMax;

            maximums[key] = max;
            values[key] = Mathf.Clamp(
                definition.InitialValue,
                0,
                max);
        }
    }

    public bool Contains(string key)
    {
        key = NormalizeKey(key);
        return !string.IsNullOrEmpty(key) &&
               values.ContainsKey(key);
    }

    public int Get(string key)
    {
        key = NormalizeKey(key);

        if (string.IsNullOrEmpty(key))
            return 0;

        return values.TryGetValue(key, out int value)
            ? value
            : 0;
    }

    public int GetMax(string key)
    {
        key = NormalizeKey(key);

        if (string.IsNullOrEmpty(key))
            return 0;

        return maximums.TryGetValue(key, out int value)
            ? value
            : int.MaxValue;
    }

    public void Configure(
        string key,
        int initialValue,
        int maxValue = int.MaxValue)
    {
        key = NormalizeKey(key);

        if (string.IsNullOrEmpty(key))
            return;

        int safeMax = Mathf.Max(0, maxValue);
        maximums[key] = safeMax;
        values[key] = Mathf.Clamp(
            initialValue,
            0,
            safeMax);
    }

    public void Set(string key, int value)
    {
        Set(
            key,
            value,
            GetMax(key));
    }

    public void Set(
        string key,
        int value,
        int maxValue)
    {
        key = NormalizeKey(key);

        if (string.IsNullOrEmpty(key))
            return;

        int safeMax = Mathf.Max(0, maxValue);
        maximums[key] = safeMax;
        values[key] = Mathf.Clamp(
            value,
            0,
            safeMax);
    }

    public void Add(
        string key,
        int amount,
        int max = int.MaxValue)
    {
        key = NormalizeKey(key);

        if (string.IsNullOrEmpty(key))
            return;

        int configuredMax = GetMax(key);
        int requestedMax = Mathf.Max(0, max);
        int finalMax = Mathf.Min(
            configuredMax,
            requestedMax);

        if (!maximums.ContainsKey(key))
            maximums[key] = finalMax;

        long next = (long)Get(key) + amount;
        values[key] = Mathf.Clamp(
            next > int.MaxValue
                ? int.MaxValue
                : next < int.MinValue
                    ? int.MinValue
                    : (int)next,
            0,
            finalMax);
    }

    public bool TryConsume(
        string key,
        int amount)
    {
        if (amount <= 0)
            return true;

        int current = Get(key);

        if (current < amount)
            return false;

        Set(
            key,
            current - amount);

        return true;
    }

    public int ConsumeAll(string key)
    {
        int current = Get(key);
        Set(key, 0);
        return current;
    }

    public void Clear()
    {
        values.Clear();
        maximums.Clear();
    }

    public Dictionary<string, int> CaptureValues()
    {
        return new Dictionary<string, int>(values);
    }

    public Dictionary<string, int> CaptureMaximums()
    {
        return new Dictionary<string, int>(maximums);
    }

    public void Restore(
        IReadOnlyDictionary<string, int> capturedValues,
        IReadOnlyDictionary<string, int> capturedMaximums = null)
    {
        values.Clear();
        maximums.Clear();

        if (capturedMaximums != null)
        {
            foreach (KeyValuePair<string, int> pair in capturedMaximums)
            {
                string key = NormalizeKey(pair.Key);

                if (string.IsNullOrEmpty(key))
                    continue;

                maximums[key] = Mathf.Max(0, pair.Value);
            }
        }

        if (capturedValues == null)
            return;

        foreach (KeyValuePair<string, int> pair in capturedValues)
        {
            Set(
                pair.Key,
                pair.Value,
                GetMax(pair.Key));
        }
    }

    private string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key)
            ? string.Empty
            : key.Trim();
    }
}
