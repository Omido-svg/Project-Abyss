using System.Collections.Generic;

public class ClashManager
{
    private readonly DamageManager damageManager;
    BattleContext _battleContext; // 아직 사용은 안하는데 이벤트 발생용으로 필요할수도

    public ClashManager(BattleContext _battleContext, DamageManager damageManager)
    {
        this._battleContext = _battleContext;
        this.damageManager = damageManager;
    }

    public void Resolve(Queue<BattleAction> actions)
    {
        foreach (BattleAction action in actions)
        {
            if (action.IsResolved)
                continue;

            // 합이 존재
            if (action.ClashTarget != null)
            {
                ResolveClash(action, action.ClashTarget);

                action.IsResolved = true;
                action.ClashTarget.IsResolved = true;
            }
            else
            {
                ResolveOneSide(action);

                action.IsResolved = true;
            }
        }
    }

    //------------------------------------------------

    private void ResolveClash(BattleAction attacker, BattleAction defender)
    {
        int attackerPower = attacker.Skill.Roll(attacker.Owner);
        int defenderPower = defender.Skill.Roll(defender.Owner);

        if (attackerPower > defenderPower)
        {
            damageManager.ApplyDamage(attacker, defender);
        }
        else if (attackerPower < defenderPower)
        {
            damageManager.ApplyDamage(defender, attacker);
        }
        else
        {
            // 무승부
        }
    }

    //------------------------------------------------

    private void ResolveOneSide(BattleAction action)
    {
        damageManager.ApplyDamage(action, null);
    }
}