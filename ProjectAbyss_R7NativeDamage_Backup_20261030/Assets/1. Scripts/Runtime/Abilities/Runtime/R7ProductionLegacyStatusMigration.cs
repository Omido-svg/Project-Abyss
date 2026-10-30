using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Seven authored non-Preparation enemy/Olaf skills, with a single
    /// immediate legacy Bleeding entry, routed through a native R5 command.
    /// Mixed cards retain their original ApplyRolledPartDamageEffect entry;
    /// only the status entry changes execution engine. Serialized legacy
    /// Effects is intentionally ignored, matching EnumerateEffectEntries.
    /// </summary>
    public sealed class R7ProductionLegacyStatusMigration : IDisposable
    {
        private sealed class Rule
        {
            public string Name;
            public string Guid;
            public int EntryCount;
        }
        private static readonly Dictionary<string,Rule> Catalog=
            new Dictionary<string,Rule>(StringComparer.Ordinal)
            {
                {"5fb3b00e68684389a86c92f3e0345061",new Rule {Name="정예병의 압박(결투)",Guid="4b80d9ad43d0de14bb655a20d16b1cb4",EntryCount=1}},
                {"5a239ec7d4944c458ac5329511091871",new Rule {Name="정예병의 참격(일반공격)",Guid="6a7df3e678fffd4439fd494c51209239",EntryCount=1}},
                {"c09f804f95004e70a9864e0d02ae4509",new Rule {Name="처형자의 일격(위세)",Guid="5ac5af8c409583342b1bd34c2572f671",EntryCount=2}},
                {"313842626a60416f97ac13a24ca171e3",new Rule {Name="난폭한 공격(결투)",Guid="defb1cbfdaadbd2478772e699b7a47aa",EntryCount=1}},
                {"f8c31ca52c544f93b8d961acab0c9611",new Rule {Name="물어뜯기(일반공격)",Guid="03ee92c875632be45a2c019cd1bc8cf8",EntryCount=1}},
                {"35a5b1e98c8547218682cc24566c91c0",new Rule {Name="피의 포식 (위세)",Guid="4f3c8894b4617e14fb6b1001afa17417",EntryCount=2}},
                {"cf4031e1aabf4207b52248c0d821b85a",new Rule {Name="광전사의 결투(결투)",Guid="9307bc19f87268e49b814a05778f430c",EntryCount=1}},
            };
        private readonly BattleContext battle;
        private readonly R7NativeLegacyStatusExecutor native;
        private readonly Dictionary<(long,string,int),AbilityInstance> instances=
            new Dictionary<(long,string,int),AbilityInstance>();
        private bool disposed;
        public bool Enabled {get;set;}=true;
        public static int ApprovedSkillCount=>Catalog.Count;
        public int NativeAttempts=>native.Attempts;
        public int NativeCommits=>native.Commits;
        public R7ProductionLegacyStatusMigration(BattleContext battle)
        {
            this.battle=battle??throw new ArgumentNullException(nameof(battle));
            native=new R7NativeLegacyStatusExecutor(battle);
        }
        public bool CanRoute(SkillDefinition data)
        {
            if(disposed||!Enabled||data==null||
                !Catalog.TryGetValue(data.SkillId,out var rule)||
                data.name!=rule.Name||data.ActionType==ActionType.Preparation||
                data.EffectEntries?.Count!=rule.EntryCount||
                !data.HasEffectEntries)
                return false;
            int nativeCount=0;
            for(int i=0;i<data.EffectEntries.Count;i++)
            {
                var entry=data.EffectEntries[i];
                if(entry?.Definition==null ||
                   entry.EffectiveTiming!=SkillEffectTiming.OnExecute)
                    return false;
                if(entry.Definition is AddBodyPartStatusEffect authored)
                {
                    if(!R7NativeLegacyStatusExecutor.CanConvert(authored))
                        return false;
                    nativeCount++;
                }
                else if(!(entry.Definition is ApplyRolledPartDamageEffect))
                    return false;
            }
            if(nativeCount!=1)return false;
            if(data.Rolls!=null&&data.Rolls.Any(r=>r!=null&&
                (r.EffectEntries?.Any(x=>x?.Definition!=null)==true||
                 r.OnWinEffectEntries?.Any(x=>x?.Definition!=null)==true||
                 r.OnLoseEffectEntries?.Any(x=>x?.Definition!=null)==true)))
                return false;
            return true;
        }
        public IReadOnlyList<SkillEffectResult> Execute(SkillDefinition data,
            SkillEffectContext context,SkillEffectTiming timing)
        {
            if(!CanRoute(data))
                throw new InvalidOperationException("Legacy status card is not approved.");
            var results=new List<SkillEffectResult>();
            long actionId=context?.Action?.ActionId??0;
            if(actionId<=0||context.Owner==null)
                throw new InvalidOperationException("Native status needs real action ID.");
            long owner=R4LiveSlotQuery.ActorRuntimeId(context.Owner);
            int index=0;
            foreach(var entry in data.EnumerateEffectEntries())
            {
                if(entry?.Definition is AddBodyPartStatusEffect legacy)
                {
                    var key=(actionId,data.SkillId,index);
                    if(!instances.TryGetValue(key,out var ability))
                    {
                        ability=new AbilityInstance(
                            new AbilityDefinitionRef(data.SkillId,
                                Catalog[data.SkillId].Guid),
                            new AbilityIdentity(data.SkillId,owner,
                                context.Action.Slot?.SlotId??string.Empty,index));
                        instances.Add(key,ability);
                    }
                    if(ability.Identity.OwnerActorRuntimeId!=owner)
                        throw new InvalidOperationException("Reused status action/owner ID.");
                    var cause=new AbilityEventContext(
                        AbilityEventKind.RollUsing,actionId,actionId,owner,
                        context.RollIndex,context.Action.DuelEffectsEligible,
                        context.IsOneSided);
                    int captured=index;
                    var result=entry.TryApply(context,timing,
                        (selected,overrides)=>native.Execute(ability,cause,
                            selected,overrides,legacy,captured));
                    if(result!=null)results.Add(result);
                }
                else
                {
                    // Damage entry is not converted and executes exactly once
                    // in its original ordering and SkillEffectContext.
                    var result=entry?.TryApply(context,timing);
                    if(result!=null)results.Add(result);
                }
                index++;
            }
            return results;
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;Enabled=false;
            instances.Clear();native.Clear();
        }
    }
}