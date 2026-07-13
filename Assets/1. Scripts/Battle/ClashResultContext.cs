using System.Collections.Generic;

public class ClashResultContext
{
    public BattleAction WinnerAction;
    public BattleAction LoserAction;

    public int WinnerClashPower;
    public int LoserClashPower;
    public int Gap;

    public int MomentumShift;
    public int PrestigeGain;

    public bool WinnerWasCritical;
    public bool LoserWasCritical;

    public MomentumState WinnerMomentumStateBefore;
    public MomentumState WinnerMomentumStateAfter;

    // 실제 합인지, 일방 공격 결과인지 구분한다.
    public bool IsClash;

    public List<ClashRollVisualStep> ClashSteps = new();
    public List<int> HitDamages = new();

    public DamageContext DamageContext;
    public DamageResult DamageResult;
    public DamageEventResult DamageEventResult;

    public int FinalHpDamage;
    public int PartHpDamage;
    public int DirectHpDamage;

    public bool WasCritical;
    public bool WasKilled;
    public bool BrokePart;
    public bool WeakenedPart;

    public bool HasTargetPartHpSnapshot;
    public int TargetPartHpBefore;
    public int TargetPartHpAfter;

    public bool HasTargetCharacterHpSnapshot;
    public int TargetCharacterHpBefore;
    public int TargetCharacterHpAfter;
}
