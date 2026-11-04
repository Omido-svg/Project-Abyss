using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class NativeEmotion33Graph : NativeEmotionGraph
{
    public NativeEmotion33Kind Kind;
    public int Amount = 1;
    public int SecondaryAmount;
    public int Duration = 1;
    public int MaximumTotal;
    [Min(0f)] public float Ratio;
    [Min(0f)] public float Multiplier = 1f;

    public override void Apply(EmotionAugmentRuntimeContext context)
    {
        // Side effects are owned by the independently ported native mechanic
        // after it has been registered for THIS battle.
    }

    public override CombatMechanic CreateMechanic(
        EmotionAugmentRuntimeContext context) =>
        new NativeEmotion33Mechanic(this);
}

