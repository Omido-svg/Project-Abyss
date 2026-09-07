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
/// Gameplay v5: CombatRollType.Stagger(legacy Defense=1)는 HP 피해를 주지 않는다.
/// Attack이 교환에서 이기면 상대 굴림 종류와 무관하게 자신의 순수 피해 굴림값으로 HP 피해를 준다.
/// 흐트러짐 피해는 StaggerGaugeMechanic이 교환 이벤트에서 별도로 처리한다.
/// </summary>
public static class DamagePowerResolver
{
    public static DamagePowerResolution ResolvePaired(BattleAction winner, BattleAction loser)
    {
        if (winner == null || loser == null)
            return DamagePowerResolution.None();

        if (winner.CurrentRollType == CombatRollType.Stagger)
            return DamagePowerResolution.None(wasDefenseResolution: true);

        int purePower = winner.GetDamagePower();
        return new DamagePowerResolution(
            hasDamage: true,
            wasDefenseResolution: loser.CurrentRollType == CombatRollType.Stagger,
            primaryPower: purePower,
            applyPrimaryMomentum: true,
            secondaryPower: purePower,
            applySecondaryMomentum: true);
    }

    public static DamagePowerResolution ResolveOneSided(BattleAction action)
    {
        if (action == null)
            return DamagePowerResolution.None();

        if (action.CurrentRollType == CombatRollType.Stagger)
            return DamagePowerResolution.None(wasDefenseResolution: true);

        int purePower = action.GetDamagePower();
        return new DamagePowerResolution(true, false, purePower, true, purePower, true);
    }
}
