using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MultiRollPenaltyData
{
    public bool Enabled;
    [TextArea(1, 4)] public string PreviewText;
    public MultiRollPenaltyTiming Timing = MultiRollPenaltyTiming.OnActionEnd;
    [Min(0)] public int TriggerAfterRollIndex;
    public List<SkillEffectEntry> EffectEntries = new();
    [HideInInspector] public List<SkillEffectDefinition> Effects = new();

    public bool HasExecutableEffect =>
        Enabled &&
        ((EffectEntries != null && EffectEntries.Exists(entry => entry?.Definition != null)) ||
         (Effects != null && Effects.Exists(effect => effect != null)));

    public void Sanitize()
    {
        TriggerAfterRollIndex = Mathf.Max(0, TriggerAfterRollIndex);
        EffectEntries ??= new List<SkillEffectEntry>();
        Effects ??= new List<SkillEffectDefinition>();
    }
}