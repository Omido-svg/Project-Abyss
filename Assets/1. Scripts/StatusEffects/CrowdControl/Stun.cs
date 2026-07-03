public class Stun : CrowdControlStatus
{
    public Stun() : base(1)
    {
        Name = "Stun";
    }

    public override bool CanAct()
    {
        return false;
    }
}