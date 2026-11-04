using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Production router is an identity boundary only. All effect trigger,
    /// amount, levels and operation come from native SO; old entry/definition
    /// is never invoked or consulted for gameplay.
    /// </summary>
    public sealed class R7ProductionNativeUniqueGaugeMigration : IDisposable
    {
        private readonly NativeUniqueGaugeEngine engine=
            new NativeUniqueGaugeEngine();
        private static Dictionary<string,R7NativeUniqueGaugeDefinition> catalog;
        private bool disposed;
        public bool Enabled {get;set;}=true;
        public int Attempts=>engine.Attempts;
        public int Commits=>engine.Commits;
        public static int ApprovedSkillCount=>Catalog.Count;
        private static Dictionary<string,R7NativeUniqueGaugeDefinition> Catalog
        {
            get
            {
                if(catalog!=null)return catalog;
                var result=new Dictionary<string,R7NativeUniqueGaugeDefinition>(
                    StringComparer.Ordinal);
                var duplicates=new HashSet<string>(StringComparer.Ordinal);
                foreach(var graph in Resources.LoadAll<R7NativeUniqueGaugeDefinition>(
                    "ProjectAbyss/NativeUniqueGauges"))
                {
                    if(graph==null||!graph.IsValid)continue;
                    if(!result.TryAdd(graph.StableId,graph))
                        duplicates.Add(graph.StableId);
                }
                foreach(var id in duplicates)result.Remove(id);
                catalog=result;
                return catalog;
            }
        }
        public static void ResetCatalogCache()=>catalog=null;
        public bool CanRoute(SkillDefinition identity)=>
            !disposed&&Enabled&&identity!=null&&
            Catalog.TryGetValue(identity.SkillId,out var graph)&&
            string.Equals(identity.name,graph.SourceAssetName,
                StringComparison.Ordinal);

        public IReadOnlyList<SkillEffectResult> Execute(
            SkillDefinition identity,SkillEffectContext context,
            SkillEffectTiming timing)
        {
            if(!CanRoute(identity))
                throw new InvalidOperationException("Native unique gauge identity not approved");
            var graph=Catalog[identity.SkillId];
            SkillEffectDefinition legacyMetadata=identity.EffectEntries?.Count>0
                ?identity.EffectEntries[0]?.Definition:null;
            var evtKind=timing==SkillEffectTiming.OnExchangeWin
                ?AbilityEventKind.ExchangeWon:
                timing==SkillEffectTiming.OnExecute
                ?AbilityEventKind.RollUsing:
                AbilityEventKind.AttackResolutionStarting;
            if(evtKind!=graph.Trigger ||
               graph.RestrictToRoll && (context==null||
                   context.RollNumber!=graph.RollNumber))
                return new[]{SkillEffectResult.NotScheduled(legacyMetadata,timing)};
            var action=context?.Action;
            if(action==null||action.ActionId<=0||context.Owner==null)
                return new[]{SkillEffectResult.Failed(legacyMetadata,timing,context,
                    "Native unique gauge requires valid action/owner")};
            int level=context.BattleContext?.SkillUpgrades?.GetLevel(
                graph.StableId)??0;
            var evt=new AbilityEventContext(evtKind,action.ActionId,action.ActionId,
                R4LiveSlotQuery.ActorRuntimeId(context.Owner),
                context.RollIndex,action.DuelEffectsEligible,context.IsOneSided);
            bool applied=engine.Execute(graph,action,context.Owner,evt,level);
            return applied
                ?new[]{SkillEffectResult.Applied(legacyMetadata,timing,context)}
                :new[]{SkillEffectResult.Failed(legacyMetadata,timing,context,
                    "Native unique gauge command not committed")};
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;Enabled=false;engine.Dispose();
        }
    }
}
