public class DamageManager
{
    BattleContext _battleContext;
    public DamageManager(BattleContext _battleContext)
    {
        this._battleContext = _battleContext;
    }
    
    /// 일반 공격(기습, 평타)
    public void ApplyDamage(BattleAction action)
    {
        Character attacker = action.Owner;
        Character target = action.Target;

        float damage = CalculateDamage(attacker, target, action.Skill);

        target.TakeDamage((int)damage);
    }

    /// 합 승리 후 공격
    public void ApplyDamage(BattleAction winner, BattleAction loser)
    {
        Character attacker = winner.Owner;
        Character target = loser.Owner;

        float damage = CalculateDamage(attacker, target, winner.Skill);

        target.TakeDamage((int)damage);
    }

    //------------------------------------------------

    private float CalculateDamage(
        Character attacker,
        Character defender,
        Skill skill)
    {
        float damage = 0;

        damage += attacker.BaseStatus.attackLevel;

        damage += skill.BasePower;

        damage -= defender.BaseStatus.defenseLevel;

        damage *= attacker.BaseStatus.damageMultiplier;

        if (damage < 1)
            damage = 1;

        return damage;
    }
}