using System;
using UnityEngine;

public enum PhysicalDamageType
{
    Cut = 0,      // 절단
    Blunt = 1,    // 타격
    Pierce = 2    // 관통
}

public interface IPhysicalDamageTypeProvider
{
    bool TryResolvePhysicalDamageType(
        Skill skill,
        SkillRollData roll,
        out PhysicalDamageType damageType);
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
        return Resolve(
            action,
            action?.CurrentRollIndex ?? 0);
    }

    public static PhysicalDamageType Resolve(
        BattleAction action,
        int rollIndex)
    {
        SkillRollData roll =
            action?.Skill?.GetRollData(
                Mathf.Max(0, rollIndex));

        // 캐릭터 고유 물리 속성 공급자가 가장 먼저 결정하는 기존 우선순위를 보존한다.
        if (action?.Owner is IPhysicalDamageTypeProvider provider &&
            provider.TryResolvePhysicalDamageType(
                action.Skill,
                roll,
                out PhysicalDamageType providedType))
        {
            return providedType;
        }

        if (roll?.OverridePhysicalType == true)
            return roll.PhysicalType;

        return action?.Skill?.Definition?.PhysicalType ??
               PhysicalDamageType.Cut;
    }

    public static string GetSymbol(PhysicalDamageType type) => type switch
    {
        PhysicalDamageType.Cut => "□",
        PhysicalDamageType.Pierce => "△",
        PhysicalDamageType.Blunt => "○",
        _ => "?"
    };
}