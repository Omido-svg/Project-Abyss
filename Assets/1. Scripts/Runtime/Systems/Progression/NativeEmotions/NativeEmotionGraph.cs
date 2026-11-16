using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R7-10 immutable 63-entry source manifest for a battle-scoped emotion
/// augment graph. Every graph binds one EXISTING 1029 EmotionAugmentDefinition
/// asset by its stable ID, exact source GUID, Emotion/Tier and display name.
/// No legacy EffectDefinition instance participates in native execution.
/// </summary>
public enum NativeEmotionGraphState
{
    NativeReady = 0,
    CanonicalValuesPending = 1
}

public abstract class NativeEmotionGraph : ScriptableObject
{
    public const int CurrentVersion = 1;

    public int SchemaVersion = CurrentVersion;
    public string StableId;
    public string SourceGuid;
    public string SourceAssetName;
    public string SourceDisplayName;
    public EmotionType Emotion;
    [Range(1,3)] public int Tier = 1;
    public string CanonicalDesignStatus;
    public NativeEmotionGraphState State;
    [TextArea] public string PendingReason;

    public virtual bool IsValid =>
        SchemaVersion == CurrentVersion &&
        !string.IsNullOrWhiteSpace(StableId) &&
        !string.IsNullOrWhiteSpace(SourceGuid) &&
        SourceGuid.Length == 32 &&
        !string.IsNullOrWhiteSpace(SourceAssetName) &&
        Tier >= 1 && Tier <= 3;

    public bool Matches(EmotionAugmentDefinition augment)
    {
        return IsValid && augment != null &&
            augment.AugmentId == StableId &&
            augment.name == SourceAssetName &&
            augment.Emotion == Emotion && augment.Tier == Tier &&
            augment.DisplayName == SourceDisplayName;
    }

    public abstract void Apply(EmotionAugmentRuntimeContext context);

    public virtual CombatMechanic CreateMechanic(
        EmotionAugmentRuntimeContext context) => null;
}

