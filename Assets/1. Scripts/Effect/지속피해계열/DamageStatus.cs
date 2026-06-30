// 지속 피해 계열(화상, 출혈, 독)
public abstract class DamageStatus : StatusEffect
{
    protected int Damage;

    protected DamageStatus(
        int damage,
        int duration)
    {
        Damage = damage;
        Duration = duration;
    }

    public override void OnTurnEnd()
    {
        if (owner == null)
            return;

        owner.TakeTrueDamage(Damage, this);

        DecreaseDuration();

        if (IsExpired())
            RemoveStatus();
    }
}