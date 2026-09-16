using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Progression/Emotion Augment Catalog", fileName = "EmotionAugmentCatalog")]
public sealed class EmotionAugmentCatalog : ScriptableObject
{
    public List<EmotionAugmentDefinition> Entries = new();

    public void GetCandidates(EmotionType emotion, int tier, List<EmotionAugmentDefinition> destination)
    {
        if (destination == null) return;
        destination.Clear();
        if (Entries == null) return;
        int normalizedTier = Mathf.Clamp(tier, 1, 3);
        for (int i = 0; i < Entries.Count; i++)
        {
            EmotionAugmentDefinition entry = Entries[i];
            if (entry != null && entry.Emotion == emotion && Mathf.Clamp(entry.Tier, 1, 3) == normalizedTier)
                destination.Add(entry);
        }
    }

    public bool TryBuildCanonicalOffer(EmotionType emotion, int tier, List<EmotionAugmentDefinition> destination, out string reason)
    {
        if (destination == null)
        {
            reason = "destination is null";
            return false;
        }
        GetCandidates(emotion, tier, destination);
        if (destination.Count != 3)
        {
            reason = $"{emotion} Tier {Mathf.Clamp(tier, 1, 3)}는 정확히 3장이어야 합니다. Actual={destination.Count}";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    public int GetCandidateCount(EmotionType emotion, int tier)
    {
        if (Entries == null) return 0;
        int normalizedTier = Mathf.Clamp(tier, 1, 3);
        int count = 0;
        for (int i = 0; i < Entries.Count; i++)
        {
            EmotionAugmentDefinition entry = Entries[i];
            if (entry != null && entry.Emotion == emotion && Mathf.Clamp(entry.Tier, 1, 3) == normalizedTier)
                count++;
        }
        return count;
    }
}
