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
}