public abstract class Enemy : Character
{
    public abstract BattleAction DecideAction(BattleContext context);
}