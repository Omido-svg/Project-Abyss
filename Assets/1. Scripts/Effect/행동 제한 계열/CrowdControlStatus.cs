public abstract class CrowdControlStatus : StatusEffect
{
    protected CrowdControlStatus(int duration)
    {
        Duration = duration;
    }

    public override void OnTurnEnd()
    {
        DecreaseDuration();

        if (IsExpired())
            RemoveStatus();
    }
}