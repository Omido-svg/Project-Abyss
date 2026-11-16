using ProjectAbyss.Abilities;

public enum BoneGrantReason { DirectCard, ReceivedHit, Catastrophe }
public readonly struct BoneGrantResult
{
    public readonly int Requested, Applied, Before, After;
    public readonly BoneGrantReason Reason;
    public readonly EffectCause Cause;
    public BoneGrantResult(int requested,int before,int after,BoneGrantReason reason,EffectCause cause)
    {
        Requested=requested;Before=before;After=after;Applied=after-before;Reason=reason;Cause=cause;
    }
}
