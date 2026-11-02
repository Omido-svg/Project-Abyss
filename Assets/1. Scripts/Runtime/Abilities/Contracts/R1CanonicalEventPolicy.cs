using ProjectAbyss.Abilities;

/// <summary>
/// R1 canonical 1029 event semantics. This class decides whether an actual
/// opposed exchange may publish a card win/loss effect. Serialized legacy
/// SkillEffectTiming values are intentionally untouched.
/// </summary>
public static class R1CanonicalEventPolicy
{
    /// <summary>
    /// A card exchange outcome is NOT a hit result. A single-sided attack,
    /// simulated result or interrupted action can never publish an
    /// ExchangeWon/ExchangeLost card effect.
    /// </summary>
    public static bool AllowsExchangeOutcome(
        BattleAction action, BattleAction opponent,
        bool isClash, bool isOneSided)
    {
        if (action == null || opponent == null || !isClash || isOneSided)
            return false;
        if (action.ActionType != ActionType.Duel)
            return true;
        return action.DuelEffectsEligible &&
               opponent.ActionType == ActionType.Duel;
    }

    /// <summary>
    /// Only these six triggers can be directly authored on a card; damage,
    /// kill, status and reactive checkpoints remain internal result events.
    /// </summary>
    public static bool IsCanonicalCardTrigger(SkillEffectTiming timing) =>
        timing == SkillEffectTiming.OnAttackResolutionStarting ||
        timing == SkillEffectTiming.OnExecute ||
        timing == SkillEffectTiming.OnExchangeWin ||
        timing == SkillEffectTiming.OnExchangeLose ||
        timing == SkillEffectTiming.OnClashEnd ||
        timing == SkillEffectTiming.OnPreparationActivated;

    /// <summary>
    /// Scheduler checkpoints are not card authoring triggers, and never
    /// represent a half-committed damage result.
    /// </summary>
    public static bool IsPostCommitCheckpoint(ReactionTimingCheckpoint checkpoint) =>
        checkpoint == ReactionTimingCheckpoint.AfterExchangeSettled ||
        checkpoint == ReactionTimingCheckpoint.AfterClashCompleted ||
        checkpoint == ReactionTimingCheckpoint.AfterActionCompleted ||
        checkpoint == ReactionTimingCheckpoint.AfterDamageResultCommitted;
}
