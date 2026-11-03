using UnityEngine;

/// <summary>
/// R7-7 Hifumi-only deterministic gameplay policy. Direct domain logic;
/// no SkillEffectDefinition adapter, no global status ID and no asset overwrite.
/// Numerical balance still marked provisional where 1029 canon says "미정".
/// </summary>
public static class R7HifumiNativeRules20261103
{
    public const int BoneCap=500;
    public const int CatastropheDirectBone=60;

    // Blood Price only augments actual HP-damage hit accrual.
    // Direct bone grants, such as catastrophe's +60, never call this.
    public static int BoneFromReceivedHit(int finalHpDamage,int bloodPrice)
    {
        return finalHpDamage<=0?0:
            Mathf.Max(0,finalHpDamage)+Mathf.Max(0,bloodPrice);
    }

    // Current + future temporary bone is a payment/wager pool, never
    // a substitute for permanent BoneBand calculation.
    public static int SpendableBone(int actualBone,int temporaryAdvance) =>
        Mathf.Max(0,actualBone)+Mathf.Max(0,temporaryAdvance);

    public static int GamblingPowerFromBoneBurned(int burned) =>
        Mathf.Max(0,burned)/100*6;

    // Every bloom Goldan counter roll gets execution authorization.
    // Outside bloom: break only an already-weakened part.
    public static PartBreakMode GoldanBreakMode(bool bloom) =>
        bloom?PartBreakMode.IgnoreWeakenedPrerequisite:
            PartBreakMode.WeakenedOnly;
}