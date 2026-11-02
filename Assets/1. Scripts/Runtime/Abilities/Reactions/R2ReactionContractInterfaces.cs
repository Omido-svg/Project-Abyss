namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R2-I contract: reaction is an independent action draft, not an
    /// immediate damage command. R6 owns queueing, frame suspension,
    /// cost handling, execution and playback.
    /// </summary>
    public interface IR2TriggeredActionFactory
    {
        bool TryCreate(ReactionRequest request, long newActionId,
            Character owner, Character target, SkillDefinition definition,
            BodyPart ownerPart, BodyPart targetPart,
            out R2TriggeredActionFrame frame, out string error);
    }

    public interface IR2ReactionTimingPolicy
    {
        bool CanConstructAt(in ReactionRequest request, out string reason);
    }

    public sealed class R2CanonicalReactionTimingPolicy : IR2ReactionTimingPolicy
    {
        public bool CanConstructAt(in ReactionRequest request, out string reason)
        {
            reason = null;
            if (request.Checkpoint == ReactionTimingCheckpoint.AfterExchangeSettled ||
                request.Checkpoint == ReactionTimingCheckpoint.AfterClashCompleted ||
                request.Checkpoint == ReactionTimingCheckpoint.AfterActionCompleted)
                return true;
            reason = "Checkpoint is declared but not enabled: damage result cannot be interrupted mid-commit.";
            return false;
        }
    }
}
