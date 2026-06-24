public class SlotResolver : RandomResolver
{
    public override int MinValue => -2;

    public override int MaxValue => 6;

    public override int Roll()
    {
        int a = UnityEngine.Random.Range(1, 10);
        int b = UnityEngine.Random.Range(1, 10);

        return a * b;
    }
}