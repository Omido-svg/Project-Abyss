using System;
using UnityEngine;

[Serializable]
public class BattleVfxCue
{
    [Header("Timing")]
    public BattleVfxTiming Timing = BattleVfxTiming.OnHitFrame;

    [Header("VFX")]
    public BattleVfxDefinition Vfx;

    [Header("Anchor")]
    public BattleVfxAnchorType AnchorType = BattleVfxAnchorType.TargetBodyPart;

    [Header("Hit Filter")]
    public bool UseHitIndexFilter = false;
    public int HitIndex = 0;

    [Header("Delay")]
    public float Delay = 0f;
}