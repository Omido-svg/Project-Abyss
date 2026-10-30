using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    // R3: distinct evaluation boundaries. A post-damage predicate must not be
    // evaluated before the named DamageResult has been committed.
    public enum CombatConditionPhase
    {
        PreTarget = 0,
        CandidateTarget = 1,
        PostDamageResult = 2
    }

    public enum CombatConditionNodeKind { All = 0, Any = 1, Not = 2, Predicate = 3 }

    /// <summary>
    /// Immutable runtime condition specification. This is NOT a replacement
    /// for legacy serialized SkillEffectCondition enum/fields.
    /// A future R8 authoring editor may build these trees from ScriptableObjects.
    /// </summary>
    public sealed class CombatConditionNode : ICombatCondition
    {
        private readonly IReadOnlyList<CombatConditionNode> children;
        private readonly ICombatCondition predicate;
        public CombatConditionNodeKind Kind { get; }
        public IReadOnlyList<CombatConditionNode> Children => children;

        private CombatConditionNode(CombatConditionNodeKind kind,
            ICombatCondition predicate, CombatConditionNode[] nodes)
        {
            Kind = kind;
            this.predicate = predicate;
            children = Array.AsReadOnly(nodes ?? Array.Empty<CombatConditionNode>());
        }

        public static CombatConditionNode All(params CombatConditionNode[] children) =>
            new CombatConditionNode(CombatConditionNodeKind.All, null, children == null
                ? Array.Empty<CombatConditionNode>() : (CombatConditionNode[])children.Clone());

        public static CombatConditionNode Any(params CombatConditionNode[] children) =>
            new CombatConditionNode(CombatConditionNodeKind.Any, null, children == null
                ? Array.Empty<CombatConditionNode>() : (CombatConditionNode[])children.Clone());

        public static CombatConditionNode Not(CombatConditionNode child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            return new CombatConditionNode(CombatConditionNodeKind.Not, null, new[] { child });
        }

        public static CombatConditionNode Leaf(ICombatCondition predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            return new CombatConditionNode(CombatConditionNodeKind.Predicate, predicate, null);
        }

        public bool Evaluate(IReadOnlyAbilityContext context, CombatTargetRef candidate) =>
            CombatConditionEvaluator.Evaluate(this, context, candidate);
        internal ICombatCondition Predicate => predicate;
    }

    public static class CombatConditionEvaluator
    {
        /// <summary>Null root means 'no condition' and is true.</summary>
        public static bool Evaluate(CombatConditionNode node,
            IReadOnlyAbilityContext context, CombatTargetRef candidate)
        {
            if (node == null)
                return true;
            switch (node.Kind)
            {
                case CombatConditionNodeKind.All:
                    foreach (var child in node.Children)
                        if (child == null || !Evaluate(child, context, candidate))
                            return false;
                    return true;
                case CombatConditionNodeKind.Any:
                    foreach (var child in node.Children)
                        if (child != null && Evaluate(child, context, candidate))
                            return true;
                    return false; // empty Any is false, not an unconditional pass
                case CombatConditionNodeKind.Not:
                    return node.Children.Count == 1 &&
                           node.Children[0] != null &&
                           !Evaluate(node.Children[0], context, candidate);
                case CombatConditionNodeKind.Predicate:
                    return node.Predicate != null &&
                           node.Predicate.Evaluate(context, candidate);
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Read-only projected predicates intentionally do not get mutable
    /// Character, BodyPart, RNG or an operation sink.
    /// </summary>
    public sealed class CombatActorIdCondition : ICombatCondition
    {
        private readonly long expectedActorId;
        private readonly bool owner;
        public CombatActorIdCondition(long actorId, bool compareOwner)
        {
            expectedActorId = actorId;
            owner = compareOwner;
        }
        public bool Evaluate(IReadOnlyAbilityContext context, CombatTargetRef candidate)
        {
            return context != null && expectedActorId > 0 &&
                   (owner ? context.Event.OwnerActorRuntimeId : candidate.ActorRuntimeId)
                       == expectedActorId;
        }
    }

    /// <summary>Can be used for explicit PreTarget and CandidateTarget gates.</summary>
    public sealed class CombatEventKindCondition : ICombatCondition
    {
        private readonly AbilityEventKind expected;
        public CombatEventKindCondition(AbilityEventKind expected) { this.expected = expected; }
        public bool Evaluate(IReadOnlyAbilityContext context, CombatTargetRef candidate) =>
            context != null && context.Event.Kind == expected;
    }

    /// <summary>Legacy list conditions retain their existing AND semantics.</summary>
    public sealed class CombatConditionListAdapter : ICombatCondition
    {
        private readonly ICombatCondition[] conditions;
        public CombatConditionListAdapter(IEnumerable<ICombatCondition> items)
        {
            conditions = items == null ? Array.Empty<ICombatCondition>() :
                new List<ICombatCondition>(items).ToArray();
        }
        public bool Evaluate(IReadOnlyAbilityContext context, CombatTargetRef candidate)
        {
            foreach (var c in conditions)
                if (c != null && !c.Evaluate(context, candidate))
                    return false;
            return true;
        }
    }
}
