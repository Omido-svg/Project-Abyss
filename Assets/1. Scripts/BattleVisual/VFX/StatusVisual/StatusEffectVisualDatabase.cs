using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StatusEffectVisualDatabase",
    menuName = "Battle/Visual/Status Effect Visual Database")]
public class StatusEffectVisualDatabase : ScriptableObject
{
    [SerializeField] private List<StatusEffectVisualDefinition> visuals = new();

    private Dictionary<string, StatusEffectVisualDefinition> visualsByKey;

    private void OnEnable()
    {
        RebuildLookup(logWarnings: false);
    }

    private void OnValidate()
    {
        RebuildLookup(logWarnings: true);
    }

    public StatusEffectVisualDefinition GetVisual(string statusKey)
    {
        if (string.IsNullOrWhiteSpace(statusKey))
            return null;

        EnsureLookup();

        return visualsByKey.TryGetValue(
            Normalize(statusKey),
            out StatusEffectVisualDefinition visual)
                ? visual
                : null;
    }

    public IEnumerable<StatusEffectVisualDefinition> EnumerateVisuals()
    {
        return visuals ?? new List<StatusEffectVisualDefinition>();
    }

    private void EnsureLookup()
    {
        if (visualsByKey == null)
            RebuildLookup(logWarnings: false);
    }

    private void RebuildLookup(bool logWarnings)
    {
        visualsByKey = new Dictionary<string, StatusEffectVisualDefinition>(
            StringComparer.OrdinalIgnoreCase);

        if (visuals == null)
            return;

        foreach (StatusEffectVisualDefinition visual in visuals)
        {
            if (visual == null || string.IsNullOrWhiteSpace(visual.StatusKey))
                continue;

            string key = Normalize(visual.StatusKey);

            if (visualsByKey.ContainsKey(key))
            {
                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[STATUS VISUAL VALIDATION] 중복 StatusKey / Key={key}",
                        this);
                }

                continue;
            }

            visualsByKey.Add(key, visual);
        }
    }

    private static string Normalize(string key)
    {
        return key?.Trim() ?? string.Empty;
    }
}
