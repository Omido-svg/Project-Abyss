public class SlotResolver : RandomResolver
{
    public SlotResolver(Character owner) : base(owner) {}
    
    public override int Roll()
    {
        int a = UnityEngine.Random.Range(1, 10);
        int b = UnityEngine.Random.Range(1, 10);

        return a * b;
    }

    public override string GetResultText()
    {
        return "슬롯";
    }
}