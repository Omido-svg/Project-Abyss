public sealed class KillEventContext
{
    public Character Killer { get; }
    public Character Victim { get; }

    public BattleAction SourceAction { get; }
    public DamageContext DamageContext { get; }
    public DamageResult DamageResult { get; }

    public DamageType DamageType { get; }
    public bool IsDamageDriven { get; }

    public bool IsSelfKill =>
        Killer != null &&
        Killer == Victim;

    public bool HasKiller =>
        Killer != null &&
        Victim != null &&
        Killer != Victim;

    public KillEventContext(
        Character killer,
        Character victim,
        BattleAction sourceAction,
        DamageContext damageContext,
        DamageResult damageResult,
        DamageType damageType,
        bool isDamageDriven)
    {
        Killer = killer;
        Victim = victim;
        SourceAction = sourceAction;
        DamageContext = damageContext;
        DamageResult = damageResult;
        DamageType = damageType;
        IsDamageDriven = isDamageDriven;
    }

    public static KillEventContext FromDamage(
        DamageContext context)
    {
        if (context == null ||
            !context.WasKilled ||
            context.Target == null)
        {
            return null;
        }

        return new KillEventContext(
            context.Attacker,
            context.Target,
            context.Action,
            context,
            context.Result,
            context.DamageType,
            true);
    }

    public static KillEventContext External(
        Character killer,
        Character victim,
        BattleAction sourceAction = null)
    {
        if (victim == null)
            return null;

        return new KillEventContext(
            killer,
            victim,
            sourceAction,
            null,
            null,
            DamageType.Execution,
            false);
    }
}
