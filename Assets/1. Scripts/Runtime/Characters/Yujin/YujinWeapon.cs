using System;

public enum YujinWeaponType
{
    Baeku,
    Jeokseol,
    Nakil
}

[Serializable]
public readonly struct YujinWeaponProfile
{
    public readonly YujinWeaponType Type;
    public readonly int CoinCount;
    public readonly float FrontChance;
    public readonly int CriticalValue;
    public readonly int BaseMarkAmount;

    public YujinWeaponProfile(
        YujinWeaponType type,
        int coinCount,
        float frontChance,
        int criticalValue,
        int baseMarkAmount)
    {
        Type = type;
        CoinCount = coinCount;
        FrontChance = frontChance;
        CriticalValue = criticalValue;
        BaseMarkAmount = baseMarkAmount;
    }
}

public static class YujinWeapons
{
    public static YujinWeaponProfile Get(
        YujinWeaponType type)
    {
        return type switch
        {
            YujinWeaponType.Baeku =>
                new YujinWeaponProfile(
                    type,
                    3,
                    0.40f,
                    12,
                    4),

            YujinWeaponType.Jeokseol =>
                new YujinWeaponProfile(
                    type,
                    2,
                    0.30f,
                    15,
                    6),

            _ =>
                new YujinWeaponProfile(
                    YujinWeaponType.Nakil,
                    1,
                    0.25f,
                    19,
                    8)
        };
    }
}
