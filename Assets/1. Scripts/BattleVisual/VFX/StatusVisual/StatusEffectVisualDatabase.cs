using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StatusEffectVisualDatabase",
    menuName = "Battle/Visual/Status Effect Visual Database")]
public class StatusEffectVisualDatabase : ScriptableObject
{
    [SerializeField] private List<StatusEffectVisualDefinition> visuals = new();

    public StatusEffectVisualDefinition GetVisual(
        string statusKey)
    {
        if (string.IsNullOrEmpty(statusKey))
            return null;

        foreach (StatusEffectVisualDefinition visual in visuals)
        {
            if (visual == null)
                continue;

            if (visual.StatusKey == statusKey)
                return visual;
        }

        return null;
    }
}