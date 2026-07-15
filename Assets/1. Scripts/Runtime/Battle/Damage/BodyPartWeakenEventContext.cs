public sealed class BodyPartWeakenEventContext
{
    public Character Source { get; }
    public Character Target { get; }
    public BodyPart Part { get; }

    public BattleAction SourceAction { get; }
    public DamageContext DamageContext { get; }
    public DamageResult DamageResult { get; }

    public BodyPartState StateBefore { get; }
    public BodyPartState StateAfter { get; }

    public bool IsDamageDriven { get; }

    public BodyPartWeakenEventContext(
        Character source,
        Character target,
        BodyPart part,
        BattleAction sourceAction,
        DamageContext damageContext,
        DamageResult damageResult,
        BodyPartState stateBefore,
        BodyPartState stateAfter,
        bool isDamageDriven)
    {
        Source = source;
        Target = target;
        Part = part;
        SourceAction = sourceAction;
        DamageContext = damageContext;
        DamageResult = damageResult;
        StateBefore = stateBefore;
        StateAfter = stateAfter;
        IsDamageDriven = isDamageDriven;
    }

    public static BodyPartWeakenEventContext FromDamage(
        DamageContext context)
    {
        if (context == null ||
            !context.WeakenedPart ||
            context.Target == null ||
            context.TargetPart == null)
        {
            return null;
        }

        return new BodyPartWeakenEventContext(
            context.Attacker,
            context.Target,
            context.TargetPart,
            context.Action,
            context,
            context.Result,
            context.TargetPartStateBefore,
            context.TargetPartStateAfter,
            true);
    }

    public static BodyPartWeakenEventContext External(
        Character source,
        Character target,
        BodyPart part,
        BattleAction sourceAction = null,
        BodyPartState stateBefore = BodyPartState.Normal)
    {
        if (target == null || part == null)
            return null;

        return new BodyPartWeakenEventContext(
            source,
            target,
            part,
            sourceAction,
            null,
            null,
            stateBefore,
            part.State,
            false);
    }
}
