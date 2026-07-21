using System.Collections.Generic;

public class ClashResultContext
{
    public BattleAction FirstAction;
    public BattleAction SecondAction;

    public BattleAction WinnerAction;
    public BattleAction LoserAction;

    public int WinnerClashPower;
    public int LoserClashPower;
    public int Gap;

    public int MomentumAtStart;
    public int MomentumAfterResolution;
    public int MomentumShift;
    public int PrestigeGain;

    public bool WinnerWasCritical;
    public bool LoserWasCritical;

    public MomentumState WinnerMomentumStateBefore;
    public MomentumState WinnerMomentumStateAfter;

    public bool IsClash;
    public bool IsDraw;

    public int FirstExchangeWins;
    public int SecondExchangeWins;
    public int PairedExchangeCount;
    public int OneSidedHitCount;

    public List<ClashExchangeResult> Exchanges = new();
    public List<ClashRollVisualStep> ClashSteps = new();
    public List<int> HitDamages = new();
    public List<DamageContext> DamageContexts = new();

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

    public int TotalDamage
    {
        get
        {
            int total = 0;

            foreach (int damage in HitDamages)
                total += damage;

            return total;
        }
    }
}