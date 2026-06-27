public class BattleLogBuilder
{
    private readonly BattleLogEntry entry;

    public BattleLogBuilder(BattleAction action)
    {
        entry = new BattleLogEntry();
        entry.Action = action;
    }

    //--------------------------------

    public BattleLogBuilder SetType(BattleLogType type)
    {
        entry.Type = type;
        return this;
    }

    //--------------------------------

    public BattleLogBuilder SetClash(
        int myPower,
        int enemyPower)
    {
        entry.Type = BattleLogType.Clash;

        entry.MyPower = myPower;
        entry.EnemyPower = enemyPower;

        entry.IsWinner = myPower > enemyPower;

        return this;
    }

    //--------------------------------

    public BattleLogBuilder SetDamage(
        int damage,
        int beforeHP,
        int afterHP)
    {
        entry.Damage = damage;

        entry.TargetHPBefore = beforeHP;
        entry.TargetHPAfter = afterHP;

        return this;
    }

    //--------------------------------

    public BattleLogBuilder SetPrestige(int prestige)
    {
        entry.PrestigeGain = prestige;
        return this;
    }

    //--------------------------------

    public BattleLogBuilder SetBroken(bool broken)
    {
        entry.TargetPartBroken = broken;
        return this;
    }

    //--------------------------------

    public BattleLogBuilder SetDead(bool dead)
    {
        entry.TargetDead = dead;
        return this;
    }

    //--------------------------------

    public BattleLogBuilder SetMessage(string message)
    {
        entry.Message = message;
        return this;
    }

    //--------------------------------

    public BattleLogEntry Build()
    {
        return entry;
    }
}