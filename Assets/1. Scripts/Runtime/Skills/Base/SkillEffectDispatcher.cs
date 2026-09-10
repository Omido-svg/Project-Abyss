using System;
using System.Collections.Generic;

/// <summary>
/// SkillDefinition의 효과 context 생성, 재진입/중복 dispatch 방지,
/// EffectEntry 실행을 담당한다. Skill은 façade와 타이밍 진입점만 유지한다.
/// </summary>
internal sealed class SkillEffectDispatcher
{
    private readonly HashSet<DispatchKey> activeDispatches = new();

    public IReadOnlyList<SkillEffectResult> Execute(
        SkillDefinition definition,
        BattleAction action,
        SkillEffectTiming timing,
        int useCountThisTurn,
        BattleAction opponentAction = null,
        DamageContext damageContext = null,
        KillEventContext killContext = null,
        ClashExchangeResult exchangeResult = null,
        int rollIndex = -1,
        RollResult rollResult = null,
        bool isClash = false,
        bool isOneSided = false,
        bool rollSucceeded = false)
    {
        if (definition == null ||
            action == null ||
            !HasExecutableEffects(definition))
        {
            return Array.Empty<SkillEffectResult>();
        }

        DispatchKey key =
            new DispatchKey(
                action.ActionId,
                timing);

        if (!activeDispatches.Add(key))
            return Array.Empty<SkillEffectResult>();

        try
        {
            SkillEffectContext context =
                new SkillEffectContext(
                    action,
                    definition,
                    timing,
                    opponentAction,
                    damageContext,
                    killContext,
                    exchangeResult,
                    useCountThisTurn,
                    rollIndex,
                    rollResult,
                    isClash,
                    isOneSided,
                    rollSucceeded);

            return ExecuteEntries(
                definition,
                context,
                timing);
        }
        finally
        {
            activeDispatches.Remove(key);
        }
    }

    public IReadOnlyList<SkillEffectResult> ExecuteOwner(
        Character owner,
        SkillDefinition definition,
        SkillEffectTiming timing,
        int useCountThisTurn)
    {
        if (definition == null ||
            owner == null ||
            !HasExecutableEffects(definition))
        {
            return Array.Empty<SkillEffectResult>();
        }

        DispatchKey key =
            new DispatchKey(0, timing);

        if (!activeDispatches.Add(key))
            return Array.Empty<SkillEffectResult>();

        try
        {
            SkillEffectContext context =
                new SkillEffectContext(
                    owner,
                    definition,
                    timing,
                    useCountThisTurn);

            return ExecuteEntries(
                definition,
                context,
                timing);
        }
        finally
        {
            activeDispatches.Remove(key);
        }
    }

    public void Clear()
    {
        activeDispatches.Clear();
    }

    private static bool HasExecutableEffects(
        SkillDefinition definition)
    {
        return definition != null &&
               (definition.HasEffectEntries ||
                (definition.Effects != null &&
                 definition.Effects.Count > 0));
    }

    private static IReadOnlyList<SkillEffectResult>
        ExecuteEntries(
            SkillDefinition definition,
            SkillEffectContext context,
            SkillEffectTiming timing)
    {
        List<SkillEffectResult> results = new();

        foreach (SkillEffectEntry entry
                 in definition.EnumerateEffectEntries())
        {
            if (entry?.Definition == null)
                continue;

            SkillEffectResult result =
                entry.TryApply(
                    context,
                    timing);

            if (result != null)
                results.Add(result);
        }

        return results;
    }

    private readonly struct DispatchKey :
        IEquatable<DispatchKey>
    {
        private readonly long actionId;
        private readonly SkillEffectTiming timing;

        public DispatchKey(
            long actionId,
            SkillEffectTiming timing)
        {
            this.actionId = actionId;
            this.timing = timing;
        }

        public bool Equals(DispatchKey other) =>
            actionId == other.actionId &&
            timing == other.timing;

        public override bool Equals(object obj) =>
            obj is DispatchKey other &&
            Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (actionId.GetHashCode() * 397) ^
                       (int)timing;
            }
        }
    }
}
