using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Exact original source preserved, but canonical N/power/probability is unset.
/// A pending card never runs a TEMP_BALANCE proxy as a silent substitute.
/// </summary>
public sealed class NativeEmotionPendingGraph : NativeEmotionGraph
{
    public int LegacyEffectEntries;

    public override bool IsValid =>
        base.IsValid &&
        State == NativeEmotionGraphState.CanonicalValuesPending;

    public override void Apply(EmotionAugmentRuntimeContext context)
    {
        Debug.LogWarning("[R7-10 Pending] "+SourceDisplayName+
            " has unresolved 1029 effects. No TEMP_BALANCE execution. "+
            PendingReason);
    }
}
