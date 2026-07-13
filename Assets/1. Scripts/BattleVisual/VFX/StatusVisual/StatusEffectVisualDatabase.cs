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
        RebuildLookup();
    }

    private void OnValidate()
    {
        visualsByKey = null;
    }

    public StatusEffectVisualDefinition GetVisual(
        string statusKey)
    {
        if (string.IsNullOrEmpty(statusKey))
            return null;

        EnsureLookup();

        return visualsByKey.TryGetValue(
            statusKey,
            out StatusEffectVisualDefinition visual)
                ? visual
                : null;
    }

    private void EnsureLookup()
    {
        if (visualsByKey == null)
            RebuildLookup();
    }

    private void RebuildLookup()
    {
        visualsByKey =
            new Dictionary<string, StatusEffectVisualDefinition>(
                StringComparer.Ordinal);

        if (visuals == null)
            return;

        foreach (StatusEffectVisualDefinition visual in visuals)
        {
            if (visual == null ||
                string.IsNullOrEmpty(visual.StatusKey) ||
                visualsByKey.ContainsKey(visual.StatusKey))
            {
                continue;
            }

            visualsByKey.Add(
                visual.StatusKey,
                visual);
        }
    }
}
