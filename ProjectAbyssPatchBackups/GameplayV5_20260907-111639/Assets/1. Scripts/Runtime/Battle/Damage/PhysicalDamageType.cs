using System;
using UnityEngine;

public enum PhysicalDamageType
{
    Cut = 0,      // 절단
    Blunt = 1,    // 둔격
    Pierce = 2    // 관통
}

[Serializable]
public sealed class PhysicalResistanceProfile
{
    [Min(0f)] public float CutMultiplier = 1f;
    [Min(0f)] public float BluntMultiplier = 1f;
    [Min(0f)] public float PierceMultiplier = 1f;

    public float GetMultiplier(PhysicalDamageType type)
    {
        return Mathf.Max(0f, type switch
        {
            PhysicalDamageType.Cut => CutMultiplier,
            PhysicalDamageType.Blunt => BluntMultiplier,
            PhysicalDamageType.Pierce => PierceMultiplier,
            _ => 1f
        });
    }

    public void Sanitize()
    {
        CutMultiplier = Mathf.Max(0f, CutMultiplier);
        BluntMultiplier = Mathf.Max(0f, BluntMultiplier);
        PierceMultiplier = Mathf.Max(0f, PierceMultiplier);
    }
}

public static class PhysicalDamageResolver
{
    public static PhysicalDamageType Resolve(BattleAction action)
    {
        if (action?.Owner is Yujin yujin)
            return yujin.ResolveWeaponPhysicalType();

        return action?.Skill?.Definition?.PhysicalType ??
               PhysicalDamageType.Cut;
    }
}
