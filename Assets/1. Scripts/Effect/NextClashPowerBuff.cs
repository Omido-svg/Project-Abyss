public class NextClashPowerBuff : StatusEffect
{
    private readonly int bonus;

    public NextClashPowerBuff(int bonus)
    {
        this.bonus = bonus;
    }

    public override int ModifyRoll(
        BattleAction action,
        int roll)
    {
        // 합에서만 적용
        if (!action.IsDuel)
            return roll;

        RemoveStatus();

        return roll + bonus;
    }
}