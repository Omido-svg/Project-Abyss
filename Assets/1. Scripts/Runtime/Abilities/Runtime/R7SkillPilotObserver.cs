using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    /// <summary>Asset identity reference provided by a validated catalog.
    /// UnityEditor.AssetDatabase is intentionally NOT used in runtime code.</summary>
    public readonly struct R7SkillAssetRef
    {
        public readonly SkillDefinition Definition;
        public readonly string AssetGuid;
        public R7SkillAssetRef(SkillDefinition definition,string guid)
        {
            Definition=definition;AssetGuid=guid??string.Empty;
        }
    }

    public readonly struct R7PilotObservedAction
    {
        public readonly long ActionId;
        public readonly string SkillId;
        public readonly string SourceAssetGuid;
        public readonly long OwnerRuntimeId;
        public readonly string SlotId;
        public readonly int PlannedCommands;
        public R7PilotObservedAction(long actionId,string skillId,
            string guid,long owner,string slot,int planned)
        {
            ActionId=actionId;SkillId=skillId;SourceAssetGuid=guid;
            OwnerRuntimeId=owner;SlotId=slot;PlannedCommands=planned;
        }
    }

    /// <summary>
    /// R7 first opt-in vertical slice: asset ID → AbilityIdentity →
    /// AbilityRunner on real ActionStart. This is READ-ONLY for no-effect
    /// NormalAttack candidates. Rolls, resource costs, mechanics and damage
    /// continue through the original ClashManager.
    /// A non-empty effect list is explicitly rejected: no double execution.
    /// </summary>
    public sealed class R7SkillPilotObserver : IDisposable
    {
        private sealed class AnyAttackStart : IAbilityTriggerMatcher
        {
            public bool Matches(in AbilityEventContext c)=>
                c.Kind==AbilityEventKind.AttackResolutionStarting;
        }
        private sealed class EmptyTargetSelector : ICombatTargetSelector
        {
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext _) =>
                Array.Empty<CombatTargetRef>();
        }

        private readonly BattleEvent battleEvent;
        private readonly Dictionary<SkillDefinition,R7SkillAssetRef> safe =
            new Dictionary<SkillDefinition,R7SkillAssetRef>();
        private readonly HashSet<string> ambiguousIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<long,AbilityInstance> actual =
            new Dictionary<long,AbilityInstance>();
        private readonly List<R7PilotObservedAction> log =
            new List<R7PilotObservedAction>();
        private readonly AbilityRunner runner = new AbilityRunner();
        private readonly AnyAttackStart matcher = new AnyAttackStart();
        private readonly EmptyTargetSelector selector=new EmptyTargetSelector();
        private bool disposed;

        public int EligibleAssetCount=>safe.Count;
        public int AmbiguousIdCount=>ambiguousIds.Count;
        public IReadOnlyList<R7PilotObservedAction> Observations=>log.AsReadOnly();
        public bool IsDisposed=>disposed;

        public R7SkillPilotObserver(BattleEvent events,IReadOnlyList<R7SkillAssetRef> source)
        {
            battleEvent=events??throw new ArgumentNullException(nameof(events));
            if(source==null)throw new ArgumentNullException(nameof(source));
            var counts=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var item in source)
            {
                if(item.Definition==null)continue;
                string id=item.Definition.SkillId;
                counts[id]=counts.TryGetValue(id,out int current)?current+1:1;
            }
            foreach(var pair in counts)
                if(pair.Value>1)ambiguousIds.Add(pair.Key);
            foreach(var item in source)
            {
                if(item.Definition==null || string.IsNullOrWhiteSpace(item.AssetGuid)||
                   ambiguousIds.Contains(item.Definition.SkillId)||
                   !IsMetadataOnlyNormal(item.Definition))
                    continue;
                if(!safe.ContainsKey(item.Definition))
                    safe.Add(item.Definition,item);
            }
            battleEvent.OnActionStart += OnActionStart;
        }

        public static bool IsMetadataOnlyNormal(SkillDefinition d)
        {
            if(d==null || d.ActionType!=ActionType.NormalAttack ||
                string.IsNullOrWhiteSpace(d.SkillId) ||
                d.SkillId.StartsWith("hifumi.",StringComparison.Ordinal) ||
                d.EffectEntries?.Any(x=>x?.Definition!=null)==true ||
                d.Effects?.Any(x=>x!=null)==true ||
                d.Rulebreaker?.Enabled==true ||
                d.OverrideResourceRules || d.ResolvePrestigeInCombat)
                return false;
            if(d.Rolls!=null)
                foreach(var r in d.Rolls)
                {
                    if(r==null)continue;
                    if(r.EffectEntries?.Any(x=>x?.Definition!=null)==true ||
                       r.OnWinEffectEntries?.Any(x=>x?.Definition!=null)==true ||
                       r.OnLoseEffectEntries?.Any(x=>x?.Definition!=null)==true ||
                       r.OnWinEffects?.Any(x=>x!=null)==true ||
                       r.OnLoseEffects?.Any(x=>x!=null)==true)
                        return false;
                }
            var p=d.MultiRollPenalty;
            if(p!=null && (p.EffectEntries?.Any(x=>x?.Definition!=null)==true ||
                           p.Effects?.Any(x=>x!=null)==true))
                return false;
            return true;
        }

        private void OnActionStart(BattleAction action)
        {
            if(disposed || action?.Skill?.Definition==null ||
               action.ActionId<=0 || action.Owner==null ||
               !safe.TryGetValue(action.Skill.Definition,out var asset) ||
               actual.ContainsKey(action.ActionId))
                return;
            long actorId=R4LiveSlotQuery.ActorRuntimeId(action.Owner);
            if(actorId<=0 || action.Slot==null)return;

            string slotId=action.Slot.SlotId??string.Empty;
            var identity=new AbilityIdentity(asset.Definition.SkillId,
                actorId,slotId,0);
            var instance=new AbilityInstance(
                new AbilityDefinitionRef(asset.Definition.SkillId,asset.AssetGuid),
                identity);
            var ctx=new AbilityEventContext(
                AbilityEventKind.AttackResolutionStarting,
                action.ActionId,action.ActionId,actorId,-1,
                action.DuelEffectsEligible,false);
            var planned=runner.Plan(instance,ctx,matcher,null,selector,
                Array.Empty<IEffectOperation>());
            // This pilot must never claim an applied effect or enqueue a
            // synthetic damage request; original Skill is the only executor.
            if(planned.Commands.Count!=0 || planned.Result.AppliedCommands!=0)
                throw new InvalidOperationException(
                    "R7 metadata pilot attempted to write gameplay.");
            actual.Add(action.ActionId,instance);
            log.Add(new R7PilotObservedAction(action.ActionId,
                identity.DefinitionId,asset.AssetGuid,actorId,slotId,
                planned.Commands.Count));
        }

        public bool TryGet(long actionId,out AbilityInstance value)=>
            actual.TryGetValue(actionId,out value);

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            battleEvent.OnActionStart -= OnActionStart;
            actual.Clear();
        }
    }
}