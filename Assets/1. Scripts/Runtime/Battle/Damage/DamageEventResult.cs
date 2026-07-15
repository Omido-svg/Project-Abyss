public sealed class DamageEventResult
{
    public DamageContext Context { get; }
    public DamageResult DamageResult { get; }

    public KillEventContext Kill { get; }
    public BodyPartBreakEventContext Break { get; }
    public BodyPartWeakenEventContext Weaken { get; }

    public bool HasKill => Kill != null;
    public bool HasBreak => Break != null;
    public bool HasWeaken => Weaken != null;

    private DamageEventResult(
        DamageContext context,
        DamageResult damageResult,
        KillEventContext kill,
        BodyPartBreakEventContext breakContext,
        BodyPartWeakenEventContext weaken)
    {
        Context = context;
        DamageResult = damageResult;
        Kill = kill;
        Break = breakContext;
        Weaken = weaken;
    }

    public static DamageEventResult FromContext(
        DamageContext context)
    {
        if (context == null)
            return null;

        return new DamageEventResult(
            context,
            context.Result,
            KillEventContext.FromDamage(context),
            BodyPartBreakEventContext.FromDamage(context),
            BodyPartWeakenEventContext.FromDamage(context));
    }
}
