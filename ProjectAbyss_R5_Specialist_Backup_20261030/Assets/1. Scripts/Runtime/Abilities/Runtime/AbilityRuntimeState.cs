using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    public enum AbilityLifetimeBoundary
    {
        Turn, Battle, Run
    }

    public readonly struct ActivationKey : IEquatable<ActivationKey>
    {
        public readonly long ActionId;
        public readonly AbilityEventKind EventKind;
        public readonly int RollUseIndex;
        public ActivationKey(long actionId, AbilityEventKind eventKind, int rollUseIndex)
        {
            ActionId = actionId;
            EventKind = eventKind;
            RollUseIndex = rollUseIndex;
        }
        public bool Equals(ActivationKey other) =>
            ActionId == other.ActionId &&
            EventKind == other.EventKind &&
            RollUseIndex == other.RollUseIndex;
        public override bool Equals(object obj) => obj is ActivationKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ActionId, (int)EventKind, RollUseIndex);
    }

    public readonly struct AbilityExecutionResult
    {
        public readonly int PlannedCommands;
        public readonly int AppliedCommands;
        public readonly string FailureReason;
        public bool HasApplied => AppliedCommands > 0;
        public AbilityExecutionResult(int planned, int applied, string failureReason)
        {
            PlannedCommands = Math.Max(0, planned);
            AppliedCommands = Math.Max(0, applied);
            FailureReason = failureReason ?? string.Empty;
        }
    }

    public interface IAbilityRuntimeState
    {
        bool CanActivate(ActivationKey key);
        void Commit(ActivationKey key, AbilityExecutionResult result);
        void ResetAt(AbilityLifetimeBoundary boundary);
        int SuccessfulActivationCount { get; }
    }

    /// <summary>
    /// Battle-local activation ledger; only committed effects count.
    /// No mutable fields are written into a shared ScriptableObject.
    /// </summary>
    public sealed class AbilityRuntimeState : IAbilityRuntimeState
    {
        private readonly HashSet<ActivationKey> committed = new HashSet<ActivationKey>();
        public int SuccessfulActivationCount => committed.Count;
        public bool CanActivate(ActivationKey key) => !committed.Contains(key);
        public void Commit(ActivationKey key, AbilityExecutionResult result)
        {
            if (result.HasApplied) committed.Add(key);
        }
        public void ResetAt(AbilityLifetimeBoundary boundary)
        {
            if (boundary == AbilityLifetimeBoundary.Battle || boundary == AbilityLifetimeBoundary.Run)
                committed.Clear();
        }
    }

    public sealed class AbilityInstance
    {
        public AbilityDefinitionRef Definition { get; }
        public AbilityIdentity Identity { get; }
        public AbilityRuntimeState RuntimeState { get; }
        public AbilityInstance(AbilityDefinitionRef definition, AbilityIdentity identity)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Identity = identity;
            RuntimeState = new AbilityRuntimeState();
        }
    }
}
