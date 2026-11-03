using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class R7NativeEmotionCommonGraph20261103 : R7NativeEmotionGraph20261103
{
    public R7NativeEmotionCommonKind20261103 Kind;
    public StatusEffectId PrimaryStatus = StatusEffectId.Strength;
    [Min(0)] public int PrimaryValue = 1;
    public int PrimaryDuration = StatusEffect.InfiniteDuration;
    public bool HasSecondaryStatus;
    public StatusEffectId SecondaryStatus = StatusEffectId.Swift;
    [Min(0)] public int SecondaryValue = 1;
    public int SecondaryDuration = StatusEffect.InfiniteDuration;
    [Min(0)] public int GoldThreshold = 500;
    public bool BlockPrestige;
    [Min(1)] public int Turns = 3;
    [Min(0)] public int Amount = 1;

    public override void Apply(EmotionAugmentRuntimeContext context) { }

    public override CombatMechanic CreateMechanic(
        EmotionAugmentRuntimeContext context) =>
        new R7NativeEmotionCommonMechanic20261103(this);
}

