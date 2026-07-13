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

    // 실제 합이었는지 여부
    public bool IsClash;

    // 합 수치 연출용
    // WinnerAction 기준 값 / LoserAction 기준 값
    public List<ClashRollVisualStep> ClashSteps = new();

    // 실제 데미지 연출용
    public List<int> HitDamages = new();

    public bool HasTargetPartHpSnapshot;
    public int TargetPartHpBefore;
    public int TargetPartHpAfter;
}
