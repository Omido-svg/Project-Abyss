/// <summary>
/// Optional capability for mechanics that can replace the next Chinchiro outcome.
/// Common roll code depends on this contract instead of a concrete character mechanic.
/// </summary>
public interface IChinchiroOutcomeOverride
{
    bool TryGetForcedChinchiro(out ChinchiroCombination combination);
}

public static class ChinchiroOutcomeOverrideResolver
{
    public static bool TryResolve(Character owner, out IChinchiroOutcomeOverride outcomeOverride)
    {
        outcomeOverride = null;
        if (owner?.Mechanics == null)
            return false;

        System.Collections.Generic.IReadOnlyList<CombatMechanic> mechanics = owner.Mechanics;
        for (int i = 0; i < mechanics.Count; i++)
        {
            if (mechanics[i] is IChinchiroOutcomeOverride candidate)
            {
                outcomeOverride = candidate;
                return true;
            }
        }

        return false;
    }
}
