public sealed class DamageResult
{
    public DamageType DamageType { get; private set; }

    public Character Attacker { get; private set; }
    public Character Target { get; private set; }
    public BodyPart TargetPart { get; private set; }

    public int RequestedDamage { get; private set; }
    public int AppliedDamage { get; private set; }

    public int FinalHpDamage { get; private set; }
    public int PartHpDamage { get; private set; }
    public int DirectHpDamage { get; private set; }

    public int TargetHpBefore { get; private set; }
    public int TargetHpAfter { get; private set; }

    public bool HasTargetPartSnapshot { get; private set; }
    public int TargetPartHpBefore { get; private set; }
    public int TargetPartHpAfter { get; private set; }

    public BodyPartState TargetPartStateBefore { get; private set; }
    public BodyPartState TargetPartStateAfter { get; private set; }

    public bool WasApplied { get; private set; }
    public bool WasCritical { get; private set; }
    public bool WasKilled { get; private set; }
    public bool BrokePart { get; private set; }
    public bool WeakenedPart { get; private set; }
    public bool WasDirectHpDamage { get; private set; }

    public static DamageResult FromContext(
        DamageContext context)
    {
        if (context == null)
            return null;

        return new DamageResult
        {
            DamageType = context.DamageType,

            Attacker = context.Attacker,
            Target = context.Target,
            TargetPart = context.TargetPart,

            RequestedDamage = context.FinalDamage,
            AppliedDamage = context.AppliedDamage,

            FinalHpDamage = context.FinalHpDamage,
            PartHpDamage = context.PartHpDamage,
            DirectHpDamage = context.DirectHpDamage,

            TargetHpBefore = context.TargetHpBefore,
            TargetHpAfter = context.TargetHpAfter,

            HasTargetPartSnapshot =
                context.HasTargetPartSnapshot,

            TargetPartHpBefore =
                context.TargetPartHpBefore,

            TargetPartHpAfter =
                context.TargetPartHpAfter,

            TargetPartStateBefore =
                context.TargetPartStateBefore,

            TargetPartStateAfter =
                context.TargetPartStateAfter,

            WasApplied = context.WasApplied,
            WasCritical = context.WasCritical,
            WasKilled = context.WasKilled,
            BrokePart = context.BrokePart,
            WeakenedPart = context.WeakenedPart,
            WasDirectHpDamage =
                context.WasDirectHPDamage
        };
    }

    public int GetDisplayDamage()
    {
        if (DirectHpDamage > 0)
            return DirectHpDamage;

        if (PartHpDamage > 0)
            return PartHpDamage;

        return FinalHpDamage;
    }
}
