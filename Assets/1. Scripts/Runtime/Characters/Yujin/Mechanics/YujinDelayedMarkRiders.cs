public static class YujinDelayedTriggerKeys
{
    public const string MarkIgnited = "yujin.mark.ignited";
}

public enum YujinMarkRiderType
{
    Trap = 0,
    Deadline = 1
}

public sealed class YujinMarkRiderPayload
{
    public YujinMarkRiderType Type;
    public int Value;

    public YujinMarkRiderPayload(
        YujinMarkRiderType type,
        int value)
    {
        Type = type;
        Value = value;
    }
}
