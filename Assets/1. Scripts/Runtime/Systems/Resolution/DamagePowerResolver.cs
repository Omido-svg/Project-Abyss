using UnityEngine;

public readonly struct DamagePowerResolution
{
    public bool HasDamage { get; }
    public bool WasDefenseResolution { get; }
    public int PrimaryPower { get; }
    public bool ApplyPrimaryMomentum { get; }
    public int SecondaryPower { get; }
    public bool ApplySecondaryMomentum { get; }

    public DamagePowerResolution(bool hasDamage, bool wasDefenseResolution, int primaryPower, bool applyPrimaryMomentum, int secondaryPower, bool applySecondaryMomentum)
    {
        HasDamage = hasDamage;
        WasDefenseResolution = wasDefenseResolution;
        PrimaryPower = Mathf.Max(0, primaryPower);
        ApplyPrimaryMomentum = applyPrimaryMomentum;
        SecondaryPower = Mathf.Max(0, secondaryPower);
        ApplySecondaryMomentum = applySecondaryMomentum;
    }

    public static DamagePowerResolution None(bool wasDefenseResolution = false) =>
        new DamagePowerResolution(false, wasDefenseResolution, 0, false, 0, false);
}

/// <summary>
/// 0915 정본: HP 피해 여부는 roll별 CombatRollType이 아니라 SkillColor가 결정한다.
/// RED는 HP+흐트러짐, BLUE는 HP 없이 흐트러짐 + 자기 흐트러짐 회복.
/// </summary>
public static class DamagePowerResolver
{
    public static DamagePowerResolution ResolvePaired(BattleAction winner, BattleAction loser)
    {
        if (winner == null || loser == null)
            return DamagePowerResolution.None();

        if (winner.Skill?.IsBlue == true)
            return DamagePowerResolution.None(wasDefenseResolution: true);

        int purePower = winner.GetDamagePower();
        return new DamagePowerResolution(
            hasDamage: true,
            wasDefenseResolution: loser.Skill?.IsBlue == true,
            primaryPower: purePower,
            applyPrimaryMomentum: true,
            secondaryPower: purePower,
            applySecondaryMomentum: true);
    }

    public static DamagePowerResolution ResolveOneSided(BattleAction action)
    {
        if (action == null)
            return DamagePowerResolution.None();

        if (action.Skill?.IsBlue == true)
            return DamagePowerResolution.None(wasDefenseResolution: true);

        int purePower = action.GetDamagePower();
        return new DamagePowerResolution(true, false, purePower, true, purePower, true);
    }
}
