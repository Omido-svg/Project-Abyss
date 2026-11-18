using UnityEngine;

/// <summary>
/// R7-7 Hifumi-only deterministic gameplay policy. Direct domain logic;
/// no SkillEffectDefinition adapter, no global status ID and no asset overwrite.
/// Numerical balance still marked provisional where 1029 canon says "미정".
/// </summary>
public static class HifumiNativeRules
{
    public const int BoneCap=500;
    public const int CatastropheDirectBone=60;

    /// <summary>
    /// 히후미 자해(동귀어진 자기 피해 · 대실패 자해)는 전체 체력을 1 아래로 내리지 않는다(11-18 사용자).
    /// 실제로 깎을 수 있는 양(현재 전체 체력 − 1)까지로 줄인 값을 돌려준다.
    /// </summary>
    public static int SelfDamageAboveFloor(Character owner,int damage)
    {
        if(owner?.RuntimeStatus==null)return Mathf.Max(0,damage);
        return Mathf.Clamp(damage,0,Mathf.Max(0,owner.CurrentHP-1));
    }

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