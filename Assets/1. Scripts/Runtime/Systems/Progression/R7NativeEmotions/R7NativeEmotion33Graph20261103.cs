using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class R7NativeEmotion33Graph20261103 : R7NativeEmotionGraph20261103
{
    public R7NativeEmotion33Kind20261103 Kind;
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
        new R7NativeEmotion33Mechanic20261103(this);
}

