public class BrokenHead : BrokenPartStatus
{
    public BrokenHead()
        : base(PartType.HEAD)
    {
        Name = "Broken Head";
    }

    public override int ModifyRoll(
        BattleAction action,
        int roll)
    {
        return roll - 1;
    }
}