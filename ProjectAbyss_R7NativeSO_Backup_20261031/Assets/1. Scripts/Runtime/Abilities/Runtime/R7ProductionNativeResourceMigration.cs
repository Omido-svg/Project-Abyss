using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R7 first PRODUCTION migration cohort. Exactly 15 UNIQUE stable SkillIds
    /// with ONE authored TempBalance resource entry are eligible. It is a
    /// battle-local service, installed by BattleRuntimeFactory and default ON.
    /// All other skills continue through the original SkillEffectDispatcher.
    /// Never invokes TempBalanceSkillProxyEffectDefinition.Apply on routed cards.
    /// </summary>
    public sealed class R7ProductionNativeResourceMigration : IDisposable
    {
        private static readonly Dictionary<string,TempBalanceSkillProxyOperation> Allow =
            new Dictionary<string,TempBalanceSkillProxyOperation>(StringComparer.Ordinal)
            {
                {"common.preparation.crouch",TempBalanceSkillProxyOperation.GainBlock},
                {"common.preparation.glare",TempBalanceSkillProxyOperation.TurnClashPowerBonus},
                {"olaf.duel.payback_all",TempBalanceSkillProxyOperation.GainPrestige},
                {"olaf.normal.shield_wall",TempBalanceSkillProxyOperation.GainBlock},
                {"olaf.normal.endure",TempBalanceSkillProxyOperation.GainBlock},
                {"olaf.normal.parry",TempBalanceSkillProxyOperation.GainBlock},
                {"olaf.normal.rise_again",TempBalanceSkillProxyOperation.GainBlock},
                {"olaf.normal.cover",TempBalanceSkillProxyOperation.GainBlock},
                {"olaf.normal.first_strike",TempBalanceSkillProxyOperation.GainPrestige},
                {"olaf.preparation.breath_bought_with_blood",TempBalanceSkillProxyOperation.GainPrestige},
                {"olaf.preparation.calm_madness",TempBalanceSkillProxyOperation.GainBlock},
                {"olaf.preparation.erase_wound",TempBalanceSkillProxyOperation.GainBlock},
                {"olaf.preparation.devour_stagger",TempBalanceSkillProxyOperation.GainPrestige},
                {"olaf.prestige.ragnarok",TempBalanceSkillProxyOperation.GainBlock},
                {"yujin.duel.block_retreat",TempBalanceSkillProxyOperation.GainPrestige}
            };

        // GUID snapshot from the verified 2026-10-30 Unity AssetDatabase;
        // runtime does not depend on UnityEditor/AssetDatabase APIs.
        private static readonly Dictionary<string,string> AssetGuids =
            new Dictionary<string,string>(StringComparer.Ordinal)
            {
                {"common.preparation.crouch","89874fcdf7b691748b5601ff040d175b"},
                {"common.preparation.glare","1464d46d5b893494d8513272fbcc463d"},
                {"olaf.duel.payback_all","e0b497706d40345489ed72603f09098c"},
                {"olaf.normal.cover","6b081b4a77b26e04a8d89ff2c4daeed9"},
                {"olaf.normal.endure","8058cfe6fb011f34294f3e3abd8d203c"},
                {"olaf.normal.first_strike","78ae264f7be5e81478fc11a8de548904"},
                {"olaf.normal.parry","a9f8ecac40d420541a48c599861e758f"},
                {"olaf.normal.rise_again","a49339629c1382f41936cea19629fe41"},
                {"olaf.normal.shield_wall","81f9a22a9097f324eb6a55014054e5ba"},
                {"olaf.preparation.breath_bought_with_blood","3d1ed9490a24613499ef57d2638343d2"},
                {"olaf.preparation.calm_madness","b5db1d965d47c1f4194171f654a26d50"},
                {"olaf.preparation.devour_stagger","858496a7d4d0b7742b15c3f86703c801"},
                {"olaf.preparation.erase_wound","0807b31f4b2fcda46bc9037e2b5af04f"},
                {"olaf.prestige.ragnarok","dfedb4807e7b5b34da92332c574fe9b1"},
                {"yujin.duel.block_retreat","381e5e5433da37348b2f95d00692fb82"},
            };

        // Verify the authored asset name and trigger as a runtime
        // safeguard against an unrelated asset reusing a stable SkillId.
        private static readonly Dictionary<string,string> ExpectedAssetNames=
            new Dictionary<string,string>(StringComparer.Ordinal)
            {
                {"common.preparation.crouch","Common_Crouch"},
                {"common.preparation.glare","Common_Glare"},
                {"olaf.duel.payback_all","Olaf_D_20"},
                {"olaf.normal.cover","Olaf_N_10"},
                {"olaf.normal.endure","Olaf_N_04"},
                {"olaf.normal.first_strike","Olaf_N_12"},
                {"olaf.normal.parry","Olaf_N_06"},
                {"olaf.normal.rise_again","Olaf_N_08"},
                {"olaf.normal.shield_wall","Olaf_N_02"},
                {"olaf.preparation.breath_bought_with_blood","Olaf_P_03"},
                {"olaf.preparation.calm_madness","Olaf_P_04"},
                {"olaf.preparation.devour_stagger","Olaf_P_06"},
                {"olaf.preparation.erase_wound","Olaf_P_05"},
                {"olaf.prestige.ragnarok","Olaf_R_03"},
                {"yujin.duel.block_retreat","Yujin_D_09"},
            };
        private static readonly Dictionary<string,SkillEffectTiming> ExpectedTiming=
            new Dictionary<string,SkillEffectTiming>(StringComparer.Ordinal)
            {
                {"common.preparation.crouch",SkillEffectTiming.OnExecute},
                {"common.preparation.glare",SkillEffectTiming.OnExecute},
                {"olaf.duel.payback_all",SkillEffectTiming.OnExchangeWin},
                {"olaf.normal.cover",SkillEffectTiming.OnExchangeWin},
                {"olaf.normal.endure",SkillEffectTiming.OnExchangeWin},
                {"olaf.normal.first_strike",SkillEffectTiming.OnExchangeWin},
                {"olaf.normal.parry",SkillEffectTiming.OnExchangeWin},
                {"olaf.normal.rise_again",SkillEffectTiming.OnExchangeWin},
                {"olaf.normal.shield_wall",SkillEffectTiming.OnExchangeWin},
                {"olaf.preparation.breath_bought_with_blood",SkillEffectTiming.OnExecute},
                {"olaf.preparation.calm_madness",SkillEffectTiming.OnExecute},
                {"olaf.preparation.devour_stagger",SkillEffectTiming.OnExecute},
                {"olaf.preparation.erase_wound",SkillEffectTiming.OnExecute},
                {"olaf.prestige.ragnarok",SkillEffectTiming.OnExecute},
                {"yujin.duel.block_retreat",SkillEffectTiming.OnExchangeWin},
            };

        private sealed class RuntimeEntry
        {
            public readonly SkillDefinition Definition;
            public readonly AbilityInstance Ability;
            public RuntimeEntry(SkillDefinition definition,AbilityInstance ability)
            {
                Definition=definition;Ability=ability;
            }
        }

        private readonly Dictionary<(long,string),RuntimeEntry> runtime=
            new Dictionary<(long,string),RuntimeEntry>();
        private readonly R7NativeResourceCommandExecutor native=
            new R7NativeResourceCommandExecutor();
        private bool disposed;
        public bool Enabled {get;set;}=true;
        public static int ApprovedSkillCount=>Allow.Count;
        public int ActualNativeAttempts=>native.NativeAttemptCount;
        public int ActualNativeCommits=>native.NativeCommittedCount;
        public bool IsDisposed=>disposed;

        public bool CanRoute(SkillDefinition data)
        {
            if(disposed||!Enabled||data==null||data.EffectEntries==null||
                data.EffectEntries.Count!=1||
                !Allow.TryGetValue(data.SkillId,out var expected))
                return false;
            var entry=data.EffectEntries[0];
            if(!ExpectedAssetNames.TryGetValue(data.SkillId,out var authoredName) ||
               !string.Equals(data.name,authoredName,StringComparison.Ordinal) ||
               !ExpectedTiming.TryGetValue(data.SkillId,out var authoredTiming) ||
               entry.EffectiveTiming!=authoredTiming)
                return false;
            if(!(entry?.Definition is TempBalanceSkillProxyEffectDefinition effect) ||
                effect.Operation!=expected ||
                !R7NativeResourceCommandExecutor.IsNative(effect) ||
                (data.Effects!=null&&data.Effects.Any(x=>x!=null)))
                return false;
            if(data.Rolls!=null && data.Rolls.Any(r=>r!=null &&
                (r.EffectEntries?.Any(x=>x?.Definition!=null)==true ||
                 r.OnWinEffectEntries?.Any(x=>x?.Definition!=null)==true ||
                 r.OnLoseEffectEntries?.Any(x=>x?.Definition!=null)==true)))
                return false;
            return true;
        }

        public IReadOnlyList<SkillEffectResult> Execute(SkillDefinition data,
            SkillEffectContext context,SkillEffectTiming timing)
        {
            if(!CanRoute(data))
                throw new InvalidOperationException("R7 production resource cohort rejected asset.");
            var entry=data.EffectEntries[0];
            var action=context?.Action;
            if(action==null||action.ActionId<=0||context.Owner==null)
                return new[]{SkillEffectResult.Failed(entry.Definition,timing,context,
                    "Native resource command requires an executable action.")};

            long owner=R4LiveSlotQuery.ActorRuntimeId(context.Owner);
            string slot=action.Slot?.SlotId??string.Empty;
            var key=(action.ActionId,data.SkillId);
            if(!runtime.TryGetValue(key,out var tracked))
            {
                // The permanent stable ID is the source contract for this
                // whitelist. No AssetDatabase/UnityEditor exists at runtime.
                var identity=new AbilityIdentity(data.SkillId,owner,slot,0);
                var instance=new AbilityInstance(
                    new AbilityDefinitionRef(data.SkillId,AssetGuids[data.SkillId]),
                    identity);
                tracked=new RuntimeEntry(data,instance);
                runtime.Add(key,tracked);
            }
            if(!ReferenceEquals(tracked.Definition,data) ||
               tracked.Ability.Identity.OwnerActorRuntimeId!=owner)
                throw new InvalidOperationException("Action ID reused with another asset or owner.");
            var cause=new AbilityEventContext(MapTiming(timing),action.ActionId,
                action.ActionId,owner,context.RollIndex,
                action.DuelEffectsEligible,context.IsOneSided);
            var result=entry.TryApply(context,timing,(selected,overrides)=>
                native.Apply(tracked.Ability,cause,selected,overrides,
                    (TempBalanceSkillProxyEffectDefinition)entry.Definition,
                    0,out var _));
            return result==null
                ?Array.Empty<SkillEffectResult>():new[]{result};
        }

        private static AbilityEventKind MapTiming(SkillEffectTiming timing)
        {
            switch(timing)
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
            disposed=true;
            Enabled=false;
            native.Clear();
            runtime.Clear();
        }
    }
}