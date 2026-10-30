using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Temporary legacy Skill dispatcher boundary. Effect authoring and
    /// execution use independent native status SO + native runner, with no
    /// TempBalanceSkillProxyEffectDefinition/SkillEffectEntry interpretation.
    /// </summary>
    public sealed class R7ProductionNativeBleedingMigration : IDisposable
    {
        private static Dictionary<string,R7NativeStatusAbilityDefinition> catalog;
        private readonly R7NativeStatusAbilityEngine engine;
        private bool disposed;
        public bool Enabled {get;set;}=true;
        public int NativeAttempts=>engine.Attempts;
        public int NativeCommits=>engine.Commits;
        public static int ApprovedSkillCount=>Catalog.Count;
        public R7ProductionNativeBleedingMigration(BattleContext battle)
        {
            engine=new R7NativeStatusAbilityEngine(battle);
        }

        private static Dictionary<string,R7NativeStatusAbilityDefinition> Catalog
        {
            get
            {
                if(catalog!=null)return catalog;
                var result=new Dictionary<string,R7NativeStatusAbilityDefinition>(
                    StringComparer.Ordinal);
                var duplicates=new HashSet<string>(StringComparer.Ordinal);
                foreach(var graph in Resources.LoadAll<R7NativeStatusAbilityDefinition>(
                    "ProjectAbyss/NativeStatuses"))
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

        public bool CanRoute(SkillDefinition legacyIdentity)
        {
            return !disposed&&Enabled&&legacyIdentity!=null&&
                Catalog.TryGetValue(legacyIdentity.SkillId,out var graph)&&
                graph!=null&&graph.IsValid&&
                graph.SourceAssetName==legacyIdentity.name;
        }

        public IReadOnlyList<SkillEffectResult> Execute(
            SkillDefinition oldIdentity,SkillEffectContext context,
            SkillEffectTiming timing)
        {
            if(!CanRoute(oldIdentity))
                throw new InvalidOperationException("Native status definition missing.");
            var graph=Catalog[oldIdentity.SkillId];
            var diagnostic=oldIdentity.EffectEntries!=null&&
                oldIdentity.EffectEntries.Count>0
                ?oldIdentity.EffectEntries[0]?.Definition:null;
            var current=ToEvent(timing);
            if(current!=graph.Trigger||
                graph.RestrictToRoll&&
                (context==null||context.RollNumber!=graph.RollNumber))
                return new[]{SkillEffectResult.NotScheduled(diagnostic,timing)};
            var action=context?.Action;
            if(action==null||action.ActionId<=0||context.Owner==null)
                return new[]{SkillEffectResult.Failed(diagnostic,timing,context,
                    "Missing action or owner for native status.")};
            int level=context.BattleContext?.SkillUpgrades?.GetLevel(graph.StableId)??0;
            long owner=R4LiveSlotQuery.ActorRuntimeId(context.Owner);
            var evt=new AbilityEventContext(current,action.ActionId,
                action.ActionId,owner,context.RollIndex,
                action.DuelEffectsEligible,context.IsOneSided);
            bool committed=engine.Execute(graph,action,context.Owner,
                context.Target,context.TargetPart,evt,level);
            return new[]{committed
                ?SkillEffectResult.Applied(diagnostic,timing,context)
                :SkillEffectResult.Failed(diagnostic,timing,context,
                    "Native status command not committed.")};
        }
        private static AbilityEventKind ToEvent(SkillEffectTiming timing)
        {
            switch(timing)
            {
                case SkillEffectTiming.OnExecute:return AbilityEventKind.RollUsing;
                case SkillEffectTiming.OnExchangeWin:return AbilityEventKind.ExchangeWon;
                case SkillEffectTiming.OnExchangeLose:return AbilityEventKind.ExchangeLost;
                case SkillEffectTiming.OnClashEnd:return AbilityEventKind.ClashCompleted;
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