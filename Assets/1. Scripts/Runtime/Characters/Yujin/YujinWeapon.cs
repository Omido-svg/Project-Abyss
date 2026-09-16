using System;

public enum YujinWeaponType
{
    Baeku,
    Jeokseol,
    Nakil
}

public enum YujinMarkIgnitionType
{
    MomentumPush = 0,
    Seal = 1,
    Weaken = 2
}

/// <summary>
/// 0916 Y-01: 유진 무기 계약의 단일 Source of Truth.
/// 코인 수/앞면 확률/크리값/타격타입/처형/표식 발화/표식 부여량을
/// 다른 스킬 또는 Character inspector에서 중복 보관하지 않는다.
/// </summary>
[Serializable]
public readonly struct YujinWeaponProfile
{
    public readonly YujinWeaponType Type;
    public readonly int CoinCount;
    public readonly float FrontChance;
    public readonly int CriticalValue;
    public readonly PhysicalDamageType PhysicalType;
    public readonly bool CanExecute;
    public readonly YujinMarkIgnitionType MarkIgnition;
    public readonly int NormalMarkAmount;
    public readonly int DuelMarkAmount;
    public readonly int AdditionalStaggerDamagePerHit;

    // Legacy call sites can read this without owning a second value.
    public int BaseMarkAmount => NormalMarkAmount;

    public YujinWeaponProfile(
        YujinWeaponType type,
        int coinCount,
        float frontChance,
        int criticalValue,
        PhysicalDamageType physicalType,
        bool canExecute,
        YujinMarkIgnitionType markIgnition,
        int normalMarkAmount,
        int duelMarkAmount,
        int additionalStaggerDamagePerHit = 0)
    {
        Type = type;
        CoinCount = coinCount;
        FrontChance = frontChance;
        CriticalValue = criticalValue;
        PhysicalType = physicalType;
        CanExecute = canExecute;
        MarkIgnition = markIgnition;
        NormalMarkAmount = normalMarkAmount;
        DuelMarkAmount = duelMarkAmount;
        AdditionalStaggerDamagePerHit = additionalStaggerDamagePerHit;
    }
}

public static class YujinWeapons
{
    public const int FixedBasePower = 11;
    public const int BackContribution = 2;
    public const int MarkIgnitionThreshold = 44;

    public static YujinWeaponProfile Get(
        YujinWeaponType type)
    {
        return type switch
        {
            YujinWeaponType.Baeku =>
                new YujinWeaponProfile(
                    type,
                    3,
                    0.50f,
                    7,
                    PhysicalDamageType.Pierce,
                    false,
                    YujinMarkIgnitionType.MomentumPush,
                    6,
                    12,
                    20),

            YujinWeaponType.Jeokseol =>
                new YujinWeaponProfile(
                    type,
                    2,
                    0.50f,
                    8,
                    PhysicalDamageType.Blunt,
                    false,
                    YujinMarkIgnitionType.Seal,
                    12,
                    24),

            _ =>
                new YujinWeaponProfile(
                    YujinWeaponType.Nakil,
                    1,
                    0.20f,
                    24,
                    PhysicalDamageType.Cut,
                    true,
                    YujinMarkIgnitionType.Weaken,
                    8,
                    16)
        };
    }

    public static int ResolveCoinPower(
        YujinWeaponType weapon,
        bool front,
        int energyCost)
    {
        YujinWeaponProfile profile = Get(weapon);
        int energyBonus = Math.Max(0, energyCost) * PowerFormulaService.EnergyPowerPerPoint;

        if (!front)
            return FixedBasePower + BackContribution + energyBonus;

        if (!PowerFormulaService.TryGetCurveValue(profile.CoinCount, out int curve))
            curve = FixedBasePower;

        return curve + profile.CriticalValue + energyBonus;
    }
}
