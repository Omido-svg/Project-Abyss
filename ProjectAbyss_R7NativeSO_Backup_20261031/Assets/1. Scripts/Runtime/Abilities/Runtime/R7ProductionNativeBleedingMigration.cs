using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Battle-local DEFAULT ON native bleeding for nine verified single-entry
    /// skill assets. Duplicate stable IDs and legacy/mixed skill assets
    /// are deliberately not eligible. No ScriptableObject mutations.
    /// </summary>
    public sealed class R7ProductionNativeBleedingMigration : IDisposable
    {
        private sealed class AssetRule
        {
            public string Name;
            public string Guid;
            public SkillEffectTiming Timing;
        }
        private static readonly Dictionary<string,AssetRule> Whitelist =
            new Dictionary<string,AssetRule>(StringComparer.Ordinal)
            {
                {"olaf.duel.catch_off_guard",new AssetRule {Name="Olaf_D_13",Guid="a8b375b34a241784aa37578f9daf28cd",Timing=SkillEffectTiming.OnExchangeWin}},
                {"olaf.duel.no_retreat",new AssetRule {Name="Olaf_D_08",Guid="6cbaebefc3fa7744b8a5e45d09de0c67",Timing=SkillEffectTiming.OnExchangeWin}},
                {"olaf.duel.single_combat",new AssetRule {Name="Olaf_D_21",Guid="ec186728b972c2f40a7c7ae15aaa18f9",Timing=SkillEffectTiming.OnExchangeWin}},
                {"olaf.duel.single_cut",new AssetRule {Name="Olaf_D_03",Guid="8216d9b1279ffca448824112b9ed131f",Timing=SkillEffectTiming.OnExchangeWin}},
                {"olaf.normal.desperate_blow",new AssetRule {Name="Olaf_N_09",Guid="f8b7e47d31402ae49b2c05ce019b7c94",Timing=SkillEffectTiming.OnExchangeWin}},
                {"olaf.normal.flurry",new AssetRule {Name="Olaf_N_07",Guid="c4c19befd70a20d4591d5000990287b2",Timing=SkillEffectTiming.OnExchangeWin}},
                {"olaf.normal.smash",new AssetRule {Name="Olaf_N_05",Guid="0ccd4e460d9ad4142a73432b46c320c0",Timing=SkillEffectTiming.OnExchangeWin}},
                {"olaf.normal.split",new AssetRule {Name="Olaf_N_03",Guid="01e6b2093e8a8224f96bd89a897e2a08",Timing=SkillEffectTiming.OnExchangeWin}},
                {"olaf.prestige.rune_carving",new AssetRule {Name="Olaf_R_01",Guid="d944f90c5a346bd46896d80f70a22260",Timing=SkillEffectTiming.OnExecute}},
            };

        private sealed class Instance
        {
            public SkillDefinition Definition;
            public AbilityInstance Ability;
        }
        private readonly BattleContext battle;
        private readonly Dictionary<(long,string),Instance> instances=
            new Dictionary<(long,string),Instance>();
        private readonly R7NativeBleedingCommandExecutor executor;
        private bool disposed;
        public bool Enabled {get;set;}=true;
        public static int ApprovedSkillCount=>Whitelist.Count;
        public int NativeAttempts=>executor.Attempts;
        public int NativeCommits=>executor.Committed;

        public R7ProductionNativeBleedingMigration(BattleContext battle)
        {
            this.battle=battle??throw new ArgumentNullException(nameof(battle));
            executor=new R7NativeBleedingCommandExecutor(battle);
        }

        public bool CanRoute(SkillDefinition data)
        {
            if(disposed||!Enabled||data==null||
                !Whitelist.TryGetValue(data.SkillId,out var rule)||
                !string.Equals(data.name,rule.Name,StringComparison.Ordinal)||
                data.EffectEntries?.Count!=1||
                data.Effects?.Any(x=>x!=null)==true)
                return false;
            var entry=data.EffectEntries[0];
            if(!(entry?.Definition is TempBalanceSkillProxyEffectDefinition p)||
                p.Operation!=TempBalanceSkillProxyOperation.TargetBleeding||
                entry.EffectiveTiming!=rule.Timing)
                return false;
            if(data.Rolls!=null && data.Rolls.Any(r=>r!=null&&(
                r.EffectEntries?.Any(x=>x?.Definition!=null)==true ||
                r.OnWinEffectEntries?.Any(x=>x?.Definition!=null)==true ||
                r.OnLoseEffectEntries?.Any(x=>x?.Definition!=null)==true)))
                return false;
            return true;
        }

        public IReadOnlyList<SkillEffectResult> Execute(
            SkillDefinition data,SkillEffectContext context,SkillEffectTiming timing)
        {
            if(!CanRoute(data))
                throw new InvalidOperationException("R7 bleeding routing forbidden.");
            var entry=data.EffectEntries[0];
            var action=context?.Action;
            if(action==null||action.ActionId<=0||context.Owner==null)
                return new[]{SkillEffectResult.Failed(entry.Definition,timing,context,
                    "Missing executable action for status command.")};

            long owner=R4LiveSlotQuery.ActorRuntimeId(context.Owner);
            string slot=action.Slot?.SlotId??string.Empty;
            var key=(action.ActionId,data.SkillId);
            if(!instances.TryGetValue(key,out var instance))
            {
                var identity=new AbilityIdentity(data.SkillId,owner,slot,0);
                var ability=new AbilityInstance(
                    new AbilityDefinitionRef(data.SkillId,Whitelist[data.SkillId].Guid),
                    identity);
                instance=new Instance{Definition=data,Ability=ability};
                instances.Add(key,instance);
            }
            if(!ReferenceEquals(instance.Definition,data) ||
               instance.Ability.Identity.OwnerActorRuntimeId!=owner)
                throw new InvalidOperationException("Bleeding action identity conflict.");
            AbilityEventKind kind=timing==SkillEffectTiming.OnExchangeWin
                ?AbilityEventKind.ExchangeWon:
                timing==SkillEffectTiming.OnExecute
                ?AbilityEventKind.RollUsing:
                AbilityEventKind.AttackResolutionStarting;
            var evt=new AbilityEventContext(kind,action.ActionId,action.ActionId,
                owner,context.RollIndex,action.DuelEffectsEligible,context.IsOneSided);
            var result=entry.TryApply(context,timing,(selected,overrides)=>
                executor.Apply(instance.Ability,evt,selected,overrides,
                    (TempBalanceSkillProxyEffectDefinition)entry.Definition,0));
            return result==null?Array.Empty<SkillEffectResult>():new[]{result};
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;Enabled=false;instances.Clear();executor.Clear();
        }
    }
}