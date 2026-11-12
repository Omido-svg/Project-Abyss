using System;
using UnityEngine;

/// <summary>1112 §18.1/18.4. Zero = undecided, never an invented balance value.</summary>
[Serializable]
public sealed class YujinMarkRuleSettings
{
    [Min(0)] public int BaekuHitMomentum;
    [Min(0)] public int BaekuHitStagger;
    [Min(0)] public int BaekuIgnitionStagger;
    [Min(0)] public int NakilIgnitionDamage;
    [Min(0)] public int TrapLifetimeTurns;
    [Min(0)] public int ExplosionDamagePerTrap;
    [Min(0)] public int ResonanceWeakness;
    [Min(0)] public int ResonanceNakilExtraDamage;
    [Min(0)] public int ContagionMarkAmount;
}
