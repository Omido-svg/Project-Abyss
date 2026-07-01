using System.Collections.Generic;

public interface IBattleRule
{
    int ModifyStaggerDamage(
        Character source,
        Character target,
        int amount);
}

public class BattleContext
{
    public Character Player;

    public List<Character> Enemies = new();

    public BattleEvent _battleEvent = new();
    
    public BattleManager battleManager;
    public readonly List<IBattleRule> BattleRules = new();


    public List<Character> AllCharacters
    {
        get
        {
            List<Character> result = new();

            if (Player != null)
                result.Add(Player);

            result.AddRange(Enemies);

            return result;
        }
    }
}