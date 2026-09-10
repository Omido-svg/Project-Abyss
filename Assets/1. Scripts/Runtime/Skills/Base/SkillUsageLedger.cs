using System;
using System.Collections.Generic;

/// <summary>
/// Character 소유 단위의 스킬 사용 횟수 ledger.
/// 턴 reset은 Character.PrepareTurnStart에서 owner당 한 번만 수행하고,
/// 런타임 종료 시 owner 참조를 명시적으로 제거한다.
/// </summary>
internal static class SkillUsageLedger
{
    private sealed class UsageKey : IEquatable<UsageKey>
    {
        public Character Owner;
        public object Identity;

        public bool Equals(UsageKey other) =>
            other != null &&
            Owner == other.Owner &&
            ReferenceEquals(Identity, other.Identity);

        public override bool Equals(object obj) =>
            obj is UsageKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int ownerHash = Owner == null
                    ? 0
                    : Owner.GetHashCode();

                int identityHash = Identity == null
                    ? 0
                    : Identity.GetHashCode();

                return (ownerHash * 397) ^ identityHash;
            }
        }
    }

    private static readonly Dictionary<UsageKey, int>
        useCounts = new();

    private static readonly Dictionary<Character, int>
        preparedTurns = new();

    public static int GetUseCount(
        Character owner,
        object identity)
    {
        if (owner == null || identity == null)
            return 0;

        UsageKey lookup = new UsageKey
        {
            Owner = owner,
            Identity = identity
        };

        return useCounts.TryGetValue(
            lookup,
            out int value)
            ? value
            : 0;
    }

    public static void Increment(
        Character owner,
        object identity)
    {
        if (owner == null || identity == null)
            return;

        UsageKey key = new UsageKey
        {
            Owner = owner,
            Identity = identity
        };

        useCounts.TryGetValue(
            key,
            out int value);

        useCounts[key] = value + 1;
    }

    public static void BeginTurn(
        Character owner,
        int turn)
    {
        if (owner == null)
            return;

        int safeTurn = Math.Max(1, turn);

        if (preparedTurns.TryGetValue(
                owner,
                out int preparedTurn) &&
            preparedTurn == safeTurn)
        {
            return;
        }

        preparedTurns[owner] = safeTurn;
        ResetCounts(owner);
    }

    public static void ClearOwner(Character owner)
    {
        if (owner == null)
            return;

        ResetCounts(owner);
        preparedTurns.Remove(owner);
    }

    private static void ResetCounts(Character owner)
    {
        List<UsageKey> removeTargets = new();

        foreach (UsageKey key in useCounts.Keys)
        {
            if (key.Owner == owner)
                removeTargets.Add(key);
        }

        foreach (UsageKey key in removeTargets)
            useCounts.Remove(key);
    }
}
