using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R7 batch opt-in bridge for effectful SkillDefinitions.
    /// The original SkillEffectDispatcher remains the only entrypoint.
    /// A scope MUST explicitly select assets and provide trustworthy GUIDs.
    /// For each selected definition it EXCLUSIVELY routes to AbilityRunner.Plan,
    /// then applies via the existing specialist entry exactly once.
    /// There is never a second legacy dispatch and never a parallel modifier.
    /// No ScriptableObject is written. This bridge is a migration stage, not
    /// a claim that all specialist operations have native commands.
    /// </summary>
    public sealed class R7ExclusiveEntryMigrationScope : IDisposable
    {
        private sealed class MatchAny : IAbilityTriggerMatcher
        {
            public bool Matches(in AbilityEventContext context) => true;
        }
        private sealed class Selector : ICombatTargetSelector
        {
            private static readonly CombatTargetRef[] Stub =
                { new CombatTargetRef(0,"","") };
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext ctx) => Stub;
        }
        private sealed class EntryPlan : IEffectOperation
        {
            public void Plan(IReadOnlyAbilityContext context,
                IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
            {
                if(targets!=null && targets.Count>0)
                    sink.Enqueue(new CombatCommandIntent(
                        "R7.LEGACY_SPECIALIST_ADAPTER",targets[0],1));
            }
        }

        public readonly struct Receipt
        {
            public readonly string SkillId;
            public readonly string Guid;
            public readonly long ActionId;
            public readonly int EntryIndex;
            public readonly SkillEffectTiming Timing;
            public readonly SkillEffectResultState State;
            public readonly int Planned;
            public readonly int Applied;
            public Receipt(string id,string guid,long action,int index,
                SkillEffectTiming timing,SkillEffectResultState state,
                int planned,int applied)
            {
                SkillId=id;Guid=guid;ActionId=action;EntryIndex=index;
                Timing=timing;State=state;Planned=planned;Applied=applied;
            }
        }

        private static R7ExclusiveEntryMigrationScope active;
        private readonly Dictionary<SkillDefinition,R7SkillAssetRef> selected =
            new Dictionary<SkillDefinition,R7SkillAssetRef>();
        private readonly Dictionary<(long,int,string),AbilityInstance> instances =
            new Dictionary<(long,int,string),AbilityInstance>();
        private readonly List<Receipt> history=new List<Receipt>();
        private readonly AbilityRunner runner=new AbilityRunner();
        private readonly R7NativeResourceCommandExecutor native =
            new R7NativeResourceCommandExecutor();
        public int NativeAttempts=>native.NativeAttemptCount;
        public int NativeCommitted=>native.NativeCommittedCount;
        private readonly MatchAny matcher=new MatchAny();
        private readonly Selector selector=new Selector();
        private readonly IEffectOperation[] planOperations={new EntryPlan()};
        private readonly R7ExclusiveEntryMigrationScope parent;
        private bool disposed;

        public int SelectedDefinitions=>selected.Count;
        public int Attempts=>history.Count;
        public int Applied=>history.Count(x=>x.Applied>0);
        public IReadOnlyList<Receipt> History=>history.AsReadOnly();
        public static R7ExclusiveEntryMigrationScope Active=>active;

        private R7ExclusiveEntryMigrationScope(
            IEnumerable<R7SkillAssetRef> assets)
        {
            if(active!=null)
                throw new InvalidOperationException("R7 scope already active.");
            var source=assets?.Where(x=>x.Definition!=null).ToArray()
                ?? throw new ArgumentNullException(nameof(assets));
            var collisions=new HashSet<string>(source
                .GroupBy(x=>x.Definition.SkillId,StringComparer.Ordinal)
                .Where(x=>x.Count()>1).Select(x=>x.Key),StringComparer.Ordinal);
            foreach(var asset in source)
            {
                var d=asset.Definition;
                if(string.IsNullOrWhiteSpace(asset.AssetGuid) ||
                   string.IsNullOrWhiteSpace(d.SkillId) ||
                   collisions.Contains(d.SkillId) ||
                   d.Effects?.Any(x=>x!=null)==true ||
                   d.EffectEntries?.Any(x=>x==null || x.Definition==null)==true ||
                   d.EffectEntries?.Any(x=>x?.Definition!=null)!=true ||
                   selected.ContainsKey(d))
                    continue;
                selected.Add(d,asset);
            }
            active=this;
        }
        public static R7ExclusiveEntryMigrationScope Begin(
            IEnumerable<R7SkillAssetRef> assets)=>
            new R7ExclusiveEntryMigrationScope(assets);

        public bool CanRoute(SkillDefinition definition) =>
            !disposed && definition!=null && selected.ContainsKey(definition);

        internal IReadOnlyList<SkillEffectResult> Execute(
            SkillDefinition definition,SkillEffectContext context,
            SkillEffectTiming timing)
        {
            if(!CanRoute(definition))
                throw new InvalidOperationException("Not in active R7 scope.");
            var list=new List<SkillEffectResult>();
            int i=0;
            foreach(var entry in definition.EnumerateEffectEntries())
            {
                if(entry?.Definition==null){i++;continue;}
                var key=(context?.Action?.ActionId??0,i,
                    context?.Action?.Slot?.SlotId??"owner");
                if(!instances.TryGetValue(key,out var instance))
                {
                    var source=selected[definition];
                    long ownerId=context?.Action?.Owner!=null
                        ?R4LiveSlotQuery.ActorRuntimeId(context.Action.Owner)
                        :0;
                    var identity=new AbilityIdentity(definition.SkillId,ownerId,key.Item3,i);
                    instance=new AbilityInstance(
                        new AbilityDefinitionRef(definition.SkillId,source.AssetGuid),identity);
                    instances.Add(key,instance);
                }

                AbilityEventKind kind=MapEvent(timing);
                long actionId=key.Item1;
                var evt=new AbilityEventContext(kind,actionId,actionId,
                    instance.Identity.OwnerActorRuntimeId,
                    context?.RollIndex??-1,
                    context?.Action?.DuelEffectsEligible??false,
                    context?.IsOneSided??false);
                bool nativeEntry =
                    R7NativeResourceCommandExecutor.IsNative(entry.Definition) &&
                    context?.Action!=null && context.Action.ActionId>0;
                int plannedCount=0;
                SkillEffectResult result;
                if(nativeEntry)
                {
                    // Entry.TryApply evaluates original timing, roll filter,
                    // selected target, entry/definition conditions and upgrades.
                    // Only AFTER all gates pass is a typed R5 command planned
                    // and committed by the character's authoritative API.
                    // Legacy Definition.Apply is NEVER invoked in this branch.
                    int capturedIndex=i;
                    result=entry.TryApply(context,timing,(selected,overrides)=>
                        native.Apply(instance,evt,selected,overrides,
                            (TempBalanceSkillProxyEffectDefinition)entry.Definition,
                            capturedIndex,out plannedCount));
                }
                else
                {
                    // Other effect families remain on the exclusive specialist
                    // bridge until an equivalent native operation exists.
                    var planned=runner.Plan(instance,evt,matcher,null,selector,planOperations);
                    plannedCount=planned.Commands.Count;
                    result=new LegacySkillEffectEntryAdapter(entry).Execute(context,timing);
                }
                if(result!=null)
                {
                    list.Add(result);
                    int wasApplied=result.WasApplied?1:0;
                    if(!nativeEntry && wasApplied>0)
                        instance.RuntimeState.Commit(new ActivationKey(
                            actionId,kind,evt.RollUseIndex,evt.CauseId),
                            new AbilityExecutionResult(plannedCount,
                                wasApplied,result.Message));
                    history.Add(new Receipt(definition.SkillId,
                        selected[definition].AssetGuid,actionId,i,timing,
                        result.State,plannedCount,wasApplied));
                }
                i++;
            }
            return list;
        }

        private static AbilityEventKind MapEvent(SkillEffectTiming t)
        {
            switch(t)
            {
                case SkillEffectTiming.OnExecute:return AbilityEventKind.RollUsing;
                case SkillEffectTiming.OnExchangeWin:return AbilityEventKind.ExchangeWon;
                case SkillEffectTiming.OnExchangeLose:return AbilityEventKind.ExchangeLost;
                case SkillEffectTiming.OnClashEnd:return AbilityEventKind.ClashCompleted;
                case SkillEffectTiming.OnPreparationActivated:return AbilityEventKind.PreparationActivated;
                case SkillEffectTiming.OnAttackResolutionStarting:return AbilityEventKind.AttackResolutionStarting;
                default:return AbilityEventKind.AttackResolutionStarting;
            }
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            if(ReferenceEquals(active,this))active=parent;
            instances.Clear();
            native.Clear();
        }
    }
}