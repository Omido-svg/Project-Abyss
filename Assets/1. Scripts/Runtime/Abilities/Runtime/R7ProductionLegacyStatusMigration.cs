using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Existing SkillEffectDispatcher integration boundary for the seven
    /// natively-authored mixed/bleeding cards. Never runs legacy effects,
    /// TryApply, overrides, or SkillId-specific code routing.
    /// SkillEffectDefinition is read ONLY as legacy UI result metadata.
    /// </summary>
    public sealed class R7ProductionLegacyStatusMigration : IDisposable
    {
        private static Dictionary<string,R7NativeMixedAbilityDefinition> catalog;
        private readonly R7NativeMixedAbilityEngine engine;
        private bool disposed;
        public bool Enabled {get;set;}=true;
        public int NativeDamageAttempts => engine.DamageAttempts;
        public int NativeDamageCommits => engine.DamageCommits;
        public int NativeAttempts => engine.StatusAttempts;
        public int NativeCommits => engine.StatusCommits;
        public static int ApprovedSkillCount => Catalog.Count;

        public R7ProductionLegacyStatusMigration(BattleContext battle)
        {
            engine=new R7NativeMixedAbilityEngine(battle);
        }
        private static Dictionary<string,R7NativeMixedAbilityDefinition> Catalog
        {
            get
            {
                if(catalog!=null)return catalog;
                var result=new Dictionary<string,R7NativeMixedAbilityDefinition>(
                    StringComparer.Ordinal);
                var duplicates=new HashSet<string>(StringComparer.Ordinal);
                foreach(var graph in Resources.LoadAll<R7NativeMixedAbilityDefinition>(
                    "ProjectAbyss/NativeMixed"))
                {
                    if(graph==null || !graph.IsValid || graph.SchemaVersion!=2)
                        continue;
                    if(!result.TryAdd(graph.StableId,graph))
                        duplicates.Add(graph.StableId);
                }
                foreach(var duplicate in duplicates)result.Remove(duplicate);
                catalog=result;
                return catalog;
            }
        }
        public static void ResetCatalogCache() {catalog=null;}
        public bool CanRoute(SkillDefinition oldIdentity)
        {
            return !disposed && Enabled && oldIdentity!=null &&
                Catalog.TryGetValue(oldIdentity.SkillId,out var graph) &&
                graph!=null && graph.IsValid && graph.SchemaVersion==2 &&
                string.Equals(graph.SourceAssetName,oldIdentity.name,
                    StringComparison.Ordinal);
        }
        public IReadOnlyList<SkillEffectResult> Execute(
            SkillDefinition oldIdentity,SkillEffectContext context,
            SkillEffectTiming timing)
        {
            if(!CanRoute(oldIdentity))
                throw new InvalidOperationException(
                    "Native mixed ability asset not available.");
            var graph=Catalog[oldIdentity.SkillId];
            var results=new List<SkillEffectResult>(graph.Operations.Length);
            for(int i=0;i<graph.Operations.Length;i++)
            {
                var operation=graph.Operations[i];
                // Compatibility metadata only. Does not affect execution.
                SkillEffectDefinition diagnostic=
                    oldIdentity.EffectEntries!=null &&
                    i<oldIdentity.EffectEntries.Count
                        ?oldIdentity.EffectEntries[i]?.Definition:null;
                if(timing!=SkillEffectTiming.OnExecute ||
                   operation.Trigger!=AbilityEventKind.RollUsing)
                {
                    results.Add(SkillEffectResult.NotScheduled(diagnostic,timing));
                    continue;
                }
                if(context?.Action==null||context.Action.ActionId<=0||
                   context.Owner==null)
                {
                    results.Add(SkillEffectResult.Failed(diagnostic,timing,context,
                        "Native mixed ability requires an action and owner."));
                    continue;
                }
                bool committed=engine.Execute(graph,i,context);
                results.Add(committed
                    ?SkillEffectResult.Applied(diagnostic,timing,context)
                    :SkillEffectResult.Failed(diagnostic,timing,context,
                        "Native mixed command was not committed."));
            }
            return results;
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            Enabled=false;
            engine.Dispose();
        }
    }
}