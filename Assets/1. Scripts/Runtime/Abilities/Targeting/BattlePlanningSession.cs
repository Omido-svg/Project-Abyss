using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ProjectAbyss.Abilities
{
    public readonly struct R4WeightPreviewTarget
    {
        public readonly long ActorRuntimeId;
        public readonly string PartId;
        public readonly string SlotId;
        public readonly bool IsPrimary;
        public readonly int ConsumedBudget;
        public R4WeightPreviewTarget(long actorId,string partId,string slotId,
            bool primary,int consumed)
        {
            ActorRuntimeId=actorId;PartId=partId??string.Empty;
            SlotId=slotId??string.Empty;IsPrimary=primary;
            ConsumedBudget=consumed;
        }
    }

    /// <summary>
    /// Read-only UI DTO. SelectedSlots consumes budget, distinct physical hits
    /// receive damage. UI must not draw secondary allocations as clashes.
    /// </summary>
    public sealed class R4WeightPlanningPreview
    {
        public long ActionId {get;}
        public int PlanningRevision {get;}
        public bool Success {get;}
        public string Diagnostic {get;}
        public int EffectiveWeight {get;}
        public int UnspentBudget {get;}
        public IReadOnlyList<R4WeightPreviewTarget> SelectedSlots {get;}
        public IReadOnlyList<HitTargetKey> DistinctHitTargets {get;}
        internal R4WeightPlanningPreview(R4LiveSlotSnapshot snapshot)
        {
            ActionId=snapshot.SourceActionId;
            PlanningRevision=snapshot.PlanningRevision;
            Success=snapshot.Selection?.Success==true;
            Diagnostic=string.Join(" | ",snapshot.Diagnostics);
            EffectiveWeight=snapshot.Selection?.Allocation?.EffectiveWeight??0;
            UnspentBudget=snapshot.Selection?.UnspentBudget??0;
            var slots=new List<R4WeightPreviewTarget>();
            var result=snapshot.Selection?.Allocation;
            if(result!=null)
                foreach(var selected in result.SelectedSlots)
                    slots.Add(new R4WeightPreviewTarget(selected.Slot.ActorRuntimeId,
                        selected.Slot.PartId,selected.Slot.SlotId,
                        selected.IsPrimary,selected.ActuallyConsumedWeight));
            SelectedSlots=slots.AsReadOnly();
            DistinctHitTargets=Array.AsReadOnly(
                result?.DistinctHitTargets?.ToArray()??Array.Empty<HitTargetKey>());
        }
    }

    /// <summary>
    /// R4-W: battle-local frozen allocation. UI preview and R5-W opt-in bridge
    /// share this exact immutable snapshot. Other actions changing their aim
    /// intentionally do not recompute a locked allocation. Source skill/target
    /// changes invalidate it; no scene or ActionSlot mutation is performed.
    /// </summary>
    public static class R4BattlePlanningSession
    {
        private sealed class SourceSignature
        {
            public Skill Skill;
            public long OwnerId,TargetId;
            public string TargetPartId,TargetSlotId;
            public int Weight;
            public bool Matches(ActionSlot slot)
            {
                return slot!=null&&ReferenceEquals(Skill,slot.Skill)&&
                    OwnerId==R4LiveSlotQuery.ActorRuntimeId(slot.Owner)&&
                    TargetId==R4LiveSlotQuery.ActorRuntimeId(slot.TargetCharacter)&&
                    TargetPartId==(slot.TargetPart?.PartId??string.Empty)&&
                    TargetSlotId==(slot.TargetSlot?.SlotId??string.Empty)&&
                    Weight==(slot.Skill?.AttackWeight??0);
            }
            public static SourceSignature From(ActionSlot slot)=>new SourceSignature{
                Skill=slot.Skill,
                OwnerId=R4LiveSlotQuery.ActorRuntimeId(slot.Owner),
                TargetId=R4LiveSlotQuery.ActorRuntimeId(slot.TargetCharacter),
                TargetPartId=slot.TargetPart?.PartId??string.Empty,
                TargetSlotId=slot.TargetSlot?.SlotId??string.Empty,
                Weight=slot.Skill?.AttackWeight??0
            };
        }

        private sealed class BattleState
        {
            public readonly R4PlanningAllocationCache Cache=new R4PlanningAllocationCache();
            public readonly Dictionary<long,SourceSignature> Signatures=
                new Dictionary<long,SourceSignature>();
            public int Revision;
            public IR4TargetPlanningRule Rule;
        }

        private static readonly ConditionalWeakTable<BattleContext,BattleState> states=
            new ConditionalWeakTable<BattleContext,BattleState>();

        public static R4WeightPlanningPreview Preview(BattleContext battle,
            ActionSlot source,IReadOnlyList<Character> hostileSide,
            IReadOnlyList<ActionSlot> plans,IR4TargetPlanningRule rules=null)
        {
            if(battle==null||source==null||source.ActionId<=0)
                return null;
            var state=states.GetOrCreateValue(battle);
            
            // A newly supplied policy invalidates only this battle's plans.
            if (rules != null && !ReferenceEquals(rules,state.Rule))
            {
                state.Rule=rules;
                state.Cache.Clear();state.Signatures.Clear();
            }
            var activeRules=rules??state.Rule;
            var signature=SourceSignature.From(source);
            if(state.Signatures.TryGetValue(source.ActionId,out var previous)&&
                !previous.Matches(source))
            {
                state.Cache.Invalidate(source.ActionId,R4PlanMutationKind.SourceTargetChanged);
                state.Signatures.Remove(source.ActionId);
            }
            var snapshot=state.Cache.GetOrCapture(source.ActionId,()=>
            {
                int revision=++state.Revision;
                var captured=R4LiveSlotQuery.Capture(source,hostileSide,plans,
                    revision,Math.Max(1,source.Skill?.AttackWeight??1));
                if(activeRules==null)return captured;
                var ruleContext=new R4TargetPlanningContext(source.ActionId,
                    R4LiveSlotQuery.ActorRuntimeId(source.Owner),
                    R4LiveSlotQuery.ActorRuntimeId(source.TargetCharacter),revision);
                var eligible=captured.Candidates.Where(c=>activeRules.Allows(in ruleContext,c))
                    .ToList();
                var main=captured.Primary;
                if(main!=null&&!activeRules.Allows(in ruleContext,main))
                    main=null;
                var selection=SlotWeightedTargetPlanner.Plan(source.ActionId,
                    revision,Math.Max(1,source.Skill?.AttackWeight??1),main,eligible);
                return new R4LiveSlotSnapshot(source.ActionId,revision,eligible,
                    main,captured.Diagnostics.ToList(),selection);
            });
            if(snapshot.Selection?.Success==true)
                state.Signatures[source.ActionId]=signature;
            return new R4WeightPlanningPreview(snapshot);
        }

        /// <summary>
        /// Execution only reuses exact same source action, skill and hit part.
        /// If a planned source has changed, R5 must capture again or fail closed;
        /// stale cached targets must not be used.
        /// </summary>
        public static bool TryGetFrozen(BattleContext battle,BattleAction action,
            out R4LiveSlotSnapshot snapshot)
        {
            snapshot=null;
            if(battle==null||action?.Slot==null||action.ActionId<=0)
                return false;
            if(!states.TryGetValue(battle,out var state)||
                !state.Signatures.TryGetValue(action.ActionId,out var signature)||
                !signature.Matches(action.Slot)||
                !state.Cache.TryGet(action.ActionId,out snapshot)||
                snapshot.Selection?.Success!=true ||
                snapshot.Primary?.HitTarget.ActorRuntimeId!=
                    R4LiveSlotQuery.ActorRuntimeId(action.Target) ||
                snapshot.Primary.HitTarget.PartId!=(action.TargetPart?.PartId??string.Empty))
            {
                snapshot=null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Register a battle-local eligibility policy (stealth, locked slots,
        /// taunt restrictions). Existing allocations are invalidated; no SO or
        /// Character state is modified. Null removes the previous policy.
        /// </summary>
        public static void RegisterTargetRule(BattleContext battle,
            IR4TargetPlanningRule rule)
        {
            if(battle==null)return;
            var state=states.GetOrCreateValue(battle);
            if(ReferenceEquals(state.Rule,rule))return;
            state.Rule=rule;
            state.Cache.Clear();state.Signatures.Clear();
        }

        
        public static void NotifyMutation(BattleContext battle,long actionId,
            R4PlanMutationKind mutation)
        {
            if(battle==null||!states.TryGetValue(battle,out var state))return;
            if(mutation==R4PlanMutationKind.BattleEnded)
            {
                state.Cache.Clear();state.Signatures.Clear();state.Revision=0;
                state.Rule=null;
                return;
            }
            if(state.Cache.Invalidate(actionId,mutation))
                state.Signatures.Remove(actionId);
        }

        public static void Clear(BattleContext battle)=>
            NotifyMutation(battle,0,R4PlanMutationKind.BattleEnded);
    }
}
