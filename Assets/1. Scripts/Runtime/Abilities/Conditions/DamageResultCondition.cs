using System;
namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Immutable evidence derived from one committed DamageResult.
    /// This payload carries its OWN cause, never inferred from current actor HP.
    /// Producers must supply the real origin IDs during R5 integration.
    /// </summary>
    public readonly struct AppliedDamageEvidence
    {
        public readonly long CauseEventId;
        public readonly long OriginRollUseId;
        public readonly long OriginHitId;
        public readonly bool IsAdditionalHit;
        public readonly int AppliedHpDamage;
        public readonly bool WeakenedPart;
        public readonly bool BrokePart;
        public readonly bool KilledActor;
        public AppliedDamageEvidence(long cause, long rollUseId, long hitId, bool additional,
            int appliedHp, bool weakened, bool broken, bool killed)
        {
            CauseEventId = cause;
            OriginRollUseId = rollUseId;
            OriginHitId = hitId;
            IsAdditionalHit = additional;
            AppliedHpDamage = Math.Max(0, appliedHp);
            WeakenedPart = weakened;
            BrokePart = broken;
            KilledActor = killed;
        }
    }

    public interface IReadOnlyDamageAbilityContext : IReadOnlyAbilityContext
    {
        bool HasCommittedDamageResult { get; }
        AppliedDamageEvidence DamageEvidence { get; }
    }

    public enum DamageEvidenceTest
    {
        AppliedHpAtLeast,
        WeakenedPart,
        BrokePart,
        KilledActor
    }

    /// <summary>
    /// Same-origin check prevents a secondary hit's result from waking
    /// the original card's post-damage condition. No gameplay state mutation.
    /// </summary>
    public sealed class CombatDamageResultCondition : ICombatCondition
    {
        public DamageEvidenceTest Test { get; }
        public int Threshold { get; }
        public bool RequireOriginalHit { get; }
        public CombatDamageResultCondition(DamageEvidenceTest test,
            int threshold = 1, bool originalHitOnly = true)
        {
            Test = test; Threshold = Math.Max(0, threshold);
            RequireOriginalHit = originalHitOnly;
        }

        public bool Evaluate(IReadOnlyAbilityContext context, CombatTargetRef candidate)
        {
            if (!(context is IReadOnlyDamageAbilityContext damage) ||
                !damage.HasCommittedDamageResult ||
                context.Event.Kind != AbilityEventKind.DamageSettled)
                return false;

            var d = damage.DamageEvidence;
            if (d.CauseEventId == 0 || d.CauseEventId != context.Event.CauseId ||
                d.OriginRollUseId == 0 ||
                (RequireOriginalHit && d.IsAdditionalHit))
                return false;

            switch (Test)
            {
                case DamageEvidenceTest.AppliedHpAtLeast:
                    return d.AppliedHpDamage >= Threshold;
                case DamageEvidenceTest.WeakenedPart:
                    return d.WeakenedPart;
                case DamageEvidenceTest.BrokePart:
                    return d.BrokePart;
                case DamageEvidenceTest.KilledActor:
                    return d.KilledActor;
                default:
                    return false;
            }
        }
    }
}
