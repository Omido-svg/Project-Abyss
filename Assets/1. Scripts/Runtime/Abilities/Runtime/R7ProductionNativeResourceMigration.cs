using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Transitional boundary ONLY: legacy SkillEffectDispatcher calls this
    /// service while native effects are authored in independent SO assets.
    /// This router DOES NOT read legacy SkillEffectEntry effects, conditions,
    /// parameters, upgrades or call legacy Definition.Apply().
    /// The inner runtime engine takes only R7NativeAbilityDefinition data.
    /// </summary>
    public sealed class R7ProductionNativeResourceMigration : IDisposable
    {
        private readonly R7NativeResourceAbilityEngine engine=
            new R7NativeResourceAbilityEngine();
        private bool disposed;
        private static Dictionary<string,R7NativeAbilityDefinition> catalog;

        public bool Enabled {get;set;}=true;
        public int ActualNativeAttempts=>engine.Attempts;
        public int ActualNativeCommits=>engine.Commits;
        public bool IsDisposed=>disposed;
        public static int ApprovedSkillCount=>Catalog.Count;

        private static Dictionary<string,R7NativeAbilityDefinition> Catalog
        {
            get
            {
                if(catalog!=null)return catalog;
                var assets=Resources.LoadAll<R7NativeAbilityDefinition>(
                    "ProjectAbyss/NativeAbilities");
                var result=new Dictionary<string,R7NativeAbilityDefinition>(
                    StringComparer.Ordinal);
                var duplicate=new HashSet<string>(StringComparer.Ordinal);
                foreach(var d in assets)
                {
                    if(d==null||!d.IsValid)continue;
                    if(!result.TryAdd(d.StableId,d))
                        duplicate.Add(d.StableId);
                }
                foreach(var id in duplicate)result.Remove(id);
                catalog=result;
                return catalog;
            }
        }

        public static void ResetCatalogCache()=>catalog=null;

        public bool CanRoute(SkillDefinition oldIdentity)
        {
            if(disposed||!Enabled||oldIdentity==null)return false;
            return Catalog.TryGetValue(oldIdentity.SkillId,out var native) &&
                native!=null && native.IsValid &&
                string.Equals(oldIdentity.name,native.SourceAssetName,
                    StringComparison.Ordinal);
        }

        public IReadOnlyList<SkillEffectResult> Execute(
            SkillDefinition legacyIdentity,SkillEffectContext context,
            SkillEffectTiming timing)
        {
            if(!CanRoute(legacyIdentity))
                throw new InvalidOperationException("Unknown native resource ability.");
            var graph=Catalog[legacyIdentity.SkillId];
            // Legacy effect reference is retained only as result metadata
            // until every UI consumer speaks native AbilityResult.
            var diagnosticDefinition=legacyIdentity.EffectEntries!=null &&
                legacyIdentity.EffectEntries.Count>0
                ?legacyIdentity.EffectEntries[0]?.Definition:null;
            var eventKind=ToEvent(timing);
            if(eventKind!=graph.Trigger ||
               (graph.RestrictToRoll &&
                (context==null||context.RollNumber!=graph.RollNumber)))
                return new[]{SkillEffectResult.NotScheduled(
                    diagnosticDefinition,timing)};
            var action=context?.Action;
            if(action==null||action.ActionId<=0||context.Owner==null)
                return new[]{SkillEffectResult.Failed(
                    diagnosticDefinition,timing,context,
                    "Native ability requires a valid action and owner.")};
            int level=context.BattleContext?.SkillUpgrades?.GetLevel(
                graph.StableId)??0;
            long owner=R4LiveSlotQuery.ActorRuntimeId(context.Owner);
            var evt=new AbilityEventContext(eventKind,action.ActionId,
                action.ActionId,owner,context.RollIndex,
                action.DuelEffectsEligible,context.IsOneSided);
            bool committed=engine.Execute(graph,action,context.Owner,evt,level);
            return new[]{
                committed
                    ?SkillEffectResult.Applied(diagnosticDefinition,timing,context)
                    :SkillEffectResult.Failed(diagnosticDefinition,timing,
                        context,"Native command not committed.")
            };
        }

        private static AbilityEventKind ToEvent(SkillEffectTiming t)
        {
            switch(t)
            {
                case SkillEffectTiming.OnExecute:return AbilityEventKind.RollUsing;
                case SkillEffectTiming.OnExchangeWin:return AbilityEventKind.ExchangeWon;
                case SkillEffectTiming.OnExchangeLose:return AbilityEventKind.ExchangeLost;
                case SkillEffectTiming.OnClashEnd:return AbilityEventKind.ClashCompleted;
                case SkillEffectTiming.OnPreparationActivated:return AbilityEventKind.PreparationActivated;
                default:return AbilityEventKind.AttackResolutionStarting;
            }
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;Enabled=false;engine.Dispose();
        }
    }
}