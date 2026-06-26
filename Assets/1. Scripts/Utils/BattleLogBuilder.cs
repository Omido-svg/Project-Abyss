public class BattleLogBuilder
{
    private BattleLogEntry entry = new();

    public BattleLogBuilder(BattleAction action)
    {
        entry.Action = action;
    }

    public BattleLogBuilder SetClash(int myPower, int enemyPower)
    {
        entry.ResultType = BattleLogType.Clash;

        entry.MyPower = myPower;
        entry.EnemyPower = enemyPower;

        entry.IsWinner = myPower > enemyPower;

        return this;
    }

    public BattleLogBuilder SetDamage(int dmg)
    {
        entry.Damage = dmg;
        return this;
    }

    public BattleLogBuilder SetPrestige(int p)
    {
        entry.PrestigeGain = p;
        return this;
    }

    public BattleLogEntry Build(BattleLogger logger)
    {
        logger.Add(entry);   // 👈 여기서 통합
        return entry;
    }
}