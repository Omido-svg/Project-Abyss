using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class SkillResourceAccess
{
    private static readonly Dictionary<
        Character,
        Dictionary<string, int>> fallback = new();

    public static int Get(
        Character owner,
        string key)
    {
        if (owner == null ||
            string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        object bank = owner.Resources;

        if (TryInvokeGetter(
                bank,
                key,
                out int bankValue))
        {
            return bankValue;
        }

        return GetFallback(owner, key);
    }

    public static int Set(
        Character owner,
        string key,
        int value)
    {
        if (owner == null ||
            string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        int finalValue = Mathf.Max(0, value);
        object bank = owner.Resources;

        if (TryInvokeSetter(
                bank,
                key,
                finalValue))
        {
            return Get(owner, key);
        }

        SetFallback(owner, key, finalValue);
        return finalValue;
    }

    public static int Modify(
        Character owner,
        string key,
        int amount,
        int minimum = 0,
        int maximum = int.MaxValue)
    {
        if (owner == null ||
            string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        int current = Get(owner, key);
        int finalValue = Mathf.Clamp(
            current + amount,
            minimum,
            maximum);

        return Set(owner, key, finalValue);
    }

    private static bool TryInvokeGetter(
        object bank,
        string key,
        out int value)
    {
        value = 0;

        if (bank == null)
            return false;

        string[] names =
        {
            "Get",
            "GetValue",
            "GetResource",
            "GetAmount"
        };

        foreach (string name in names)
        {
            MethodInfo method =
                bank.GetType().GetMethod(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public,
                    null,
                    new[] { typeof(string) },
                    null);

            if (method == null)
                continue;

            object result = method.Invoke(
                bank,
                new object[] { key });

            if (result is int intValue)
            {
                value = intValue;
                return true;
            }
        }

        return false;
    }

    private static bool TryInvokeSetter(
        object bank,
        string key,
        int value)
    {
        if (bank == null)
            return false;

        string[] names =
        {
            "Set",
            "SetValue",
            "SetResource",
            "SetAmount"
        };

        foreach (string name in names)
        {
            MethodInfo method =
                bank.GetType().GetMethod(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(int)
                    },
                    null);

            if (method == null)
                continue;

            method.Invoke(
                bank,
                new object[] { key, value });
            return true;
        }

        return false;
    }

    private static int GetFallback(
        Character owner,
        string key)
    {
        if (!fallback.TryGetValue(
                owner,
                out Dictionary<string, int> resources))
        {
            return 0;
        }

        return resources.TryGetValue(
            key,
            out int value)
            ? value
            : 0;
    }

    private static void SetFallback(
        Character owner,
        string key,
        int value)
    {
        if (!fallback.TryGetValue(
                owner,
                out Dictionary<string, int> resources))
        {
            resources = new Dictionary<string, int>();
            fallback.Add(owner, resources);
        }

        resources[key] = value;
    }
}
