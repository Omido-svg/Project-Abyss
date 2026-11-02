using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R7-3 direct-native, four source-specific single operations.
    /// Never reads original effect values, timings, conditions or upgrades.
    /// </summary>
    public sealed class R7ProductionSingleEffectMigration20261102 : IDisposable
    {
        private static Dictionary<string,R7NativeSingleEffectDefinition20261102> catalog;
        private readonly R7NativeSingleEffectEngine20261102 engine;
        private bool disposed;
        public bool Enabled {get;set;}=true;
        public int Attempts=>engine.Attempts;
        public int Commits=>engine.Commits;

        public R7ProductionSingleEffectMigration20261102(BattleContext battle)
        {engine=new R7NativeSingleEffectEngine20261102(battle);}

        private static Dictionary<string,R7NativeSingleEffectDefinition20261102> Catalog
        {
            get
            {
                if(catalog!=null)return catalog;
                var result=new Dictionary<string,R7NativeSingleEffectDefinition20261102>(
                    StringComparer.Ordinal);
                var bad=new HashSet<string>(StringComparer.Ordinal);
                foreach(var x in Resources.LoadAll<R7NativeSingleEffectDefinition20261102>(
                    "ProjectAbyss/NativeSingles"))
                {
                    if(x==null||!x.IsValid)continue;
                    if(!result.TryAdd(x.StableId,x))bad.Add(x.StableId);
                }
                foreach(var id in bad)result.Remove(id);
                catalog=result;
                return result;
            }
        }
        public static int ApprovedSkillCount=>Catalog.Count;
        public static void ResetCatalogCache()=>catalog=null;

        public bool CanRoute(SkillDefinition definition)
        {
            return !disposed&&Enabled&&definition!=null&&
                definition.EffectEntries?.Count==1&&
                Catalog.TryGetValue(definition.SkillId,out var graph)&&
                graph!=null&&graph.IsValid&&
                string.Equals(graph.SourceAssetName,definition.name,StringComparison.Ordinal);
        }

        public IReadOnlyList<SkillEffectResult> Execute(SkillDefinition definition,
            SkillEffectContext context,SkillEffectTiming timing)
        {
            if(!CanRoute(definition))
                throw new InvalidOperationException("R7-3 source is not approved.");
            var graph=Catalog[definition.SkillId];
            var diagnostic=definition.EffectEntries[0]?.Definition;
            AbilityEventKind kind=timing==SkillEffectTiming.OnExchangeWin
                ?AbilityEventKind.ExchangeWon
                :timing==SkillEffectTiming.OnExecute
                    ?AbilityEventKind.RollUsing
                    :AbilityEventKind.AttackResolutionStarting;
            if(kind!=graph.Trigger||graph.RestrictToRoll&&
                (context==null||context.RollNumber!=graph.RollNumber))
                return new[]{SkillEffectResult.NotScheduled(diagnostic,timing)};
            var action=context?.Action;
            if(action?.Slot==null||action.ActionId<=0||context.Owner==null)
                return new[]{SkillEffectResult.Failed(diagnostic,timing,context,
                    "R7-3 requires a valid action, slot and owner.")};
            var evt=new AbilityEventContext(kind,action.ActionId,action.ActionId,
                R4LiveSlotQuery.ActorRuntimeId(context.Owner),
                context.RollIndex,action.DuelEffectsEligible,context.IsOneSided);
            bool applied=engine.Execute(graph,context,evt);
            return new[]{applied
                ?SkillEffectResult.Applied(diagnostic,timing,context)
                :SkillEffectResult.Failed(diagnostic,timing,context,
                    "R7-3 typed command was not committed.")};
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;Enabled=false;
            engine.Dispose();
        }
    }
}