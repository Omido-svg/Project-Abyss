using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Single authored Preparation skill: two independent legacy status
    /// entries become native R5 commands while its flat-setup entry stays
    /// on the original specialist. Includes exact PlanningUndoJournal state.
    /// </summary>
    public sealed class R7ProductionPreparationStatusMigration : IDisposable
    {
        public const string SkillId="356e72e97ac84a3a8757d1a188237f43";
        private const string AssetName="전투 태세(도사림)";
        private const string AssetGuid="f9c5728c648630849a9ba0afe641d02a";

        private sealed class Match : IAbilityTriggerMatcher
        {
            public bool Matches(in AbilityEventContext _) => true;
        }
        private sealed class Target : ICombatTargetSelector
        {
            private readonly CombatTargetRef target;
            public Target(Character owner,BodyPart part) =>
                target=new CombatTargetRef(R4LiveSlotQuery.ActorRuntimeId(owner),
                    part?.PartId??string.Empty,"");
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext _) =>
                new[]{target};
        }
        private sealed class AddStatusOperation : IEffectOperation
        {
            private readonly int value;
            private readonly bool hasPart;
            public AddStatusOperation(int value,bool hasPart)
            {
                this.value=value;
                this.hasPart=hasPart;
            }
            public void Plan(IReadOnlyAbilityContext _,
                IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
            {
                foreach(var target in targets)
                    if(value>0&&target.ActorRuntimeId>0)
                        sink.Enqueue(new CombatCommandIntent(
                            hasPart?nameof(CombatCommandKind.ApplyPartStatus):
                                nameof(CombatCommandKind.ApplyCharacterStatus),
                            target,value));
            }
        }
        private readonly BattleContext battle;
        // ActionManager intentionally preserves ActionId across slot replacement.
        // Deduplication must therefore be scoped to the actual ActionSlot,
        // NOT a global battle ActionId; only the rolled-back slot is reset.
        private readonly Dictionary<ActionSlot,CombatCommandApplyService> ledgers=
            new Dictionary<ActionSlot,CombatCommandApplyService>();
        private readonly AbilityRunner runner=new AbilityRunner();
        private readonly Match matcher=new Match();
        private readonly Dictionary<(ActionSlot,int),AbilityInstance> identities=
            new Dictionary<(ActionSlot,int),AbilityInstance>();
        private bool disposed;
        public bool Enabled {get;set;}=true;
        public int NativeAttempts {get;private set;}
        public int NativeCommits {get;private set;}
        public int UndoRecords {get;private set;}
        public R7ProductionPreparationStatusMigration(BattleContext battle)
        {
            this.battle=battle??throw new ArgumentNullException(nameof(battle));
        }
        public bool CanRoute(SkillDefinition d)
        {
            if(disposed||!Enabled||d==null||d.SkillId!=SkillId||
               d.name!=AssetName||d.ActionType!=ActionType.Preparation||
               d.EffectEntries?.Count!=3)return false;
            for(int i=0;i<2;i++)
            {
                var entry=d.EffectEntries[i];
                if(!(entry?.Definition is AddBodyPartStatusEffect status)||
                   status.AuthoringSchema!=StatusEffectAuthoringSchema.Legacy||
                   status.ApplicationTiming!=StatusApplicationTiming.Immediate||
                   status.StatusEffectId!=(i==0?StatusEffectId.Bleeding:StatusEffectId.Burn)||
                   entry.EffectiveTiming!=SkillEffectTiming.OnExecute)
                    return false;
            }
            return d.EffectEntries[2]?.Definition is PreparationFlatSetupEffect &&
                   d.EffectEntries[2].EffectiveTiming==SkillEffectTiming.OnExecute;
        }

        public IReadOnlyList<SkillEffectResult> Execute(
            SkillDefinition d,SkillEffectContext context,SkillEffectTiming timing)
        {
            if(!CanRoute(d))
                throw new InvalidOperationException("Preparation asset is not approved.");
            var results=new List<SkillEffectResult>(3);
            var action=context?.Action;
            if(action==null||context.Owner==null||action.ActionId<=0)
                throw new InvalidOperationException("Preparation action requires stable ID.");
            for(int i=0;i<d.EffectEntries.Count;i++)
            {
                var entry=d.EffectEntries[i];
                SkillEffectResult result;
                if(i==2)
                    result=entry.TryApply(context,timing); // unchanged setup effect
                else
                {
                    int index=i;
                    var key=(action.Slot,index);
                    if(!identities.TryGetValue(key,out var ability))
                    {
                        long actor=R4LiveSlotQuery.ActorRuntimeId(context.Owner);
                        ability=new AbilityInstance(
                            new AbilityDefinitionRef(SkillId,AssetGuid),
                            new AbilityIdentity(SkillId,actor,
                                action.Slot?.SlotId??string.Empty,index));
                        identities.Add(key,ability);
                    }
                    var cause=new AbilityEventContext(
                        AbilityEventKind.RollUsing,action.ActionId,
                        action.ActionId,R4LiveSlotQuery.ActorRuntimeId(context.Owner),
                        context.RollIndex,action.DuelEffectsEligible,context.IsOneSided);
                    result=entry.TryApply(context,timing,(selected,overrides)=>
                        ApplyNative(ability,cause,selected,overrides,
                            (AddBodyPartStatusEffect)entry.Definition,index));
                }
                if(result!=null)results.Add(result);
            }
            return results;
        }

        private bool ApplyNative(AbilityInstance ability,AbilityEventContext cause,
            SkillEffectContext ctx,SkillEffectOverrides overrides,
            AddBodyPartStatusEffect definition,int index)
        {
            if(ctx?.Resolver==null||ctx.Owner==null||ctx.Target==null||
               ctx.Action==null||ctx.Target.IsDead ||
               cause.ActionId<=0)return false;
            var anchor=CombatStatusAnchor.Resolve(ctx.Target,ctx.TargetPart);
            if(!anchor.IsValid)return false;
            int value=definition.ResolveAuthoredValue(overrides);
            int duration=definition.ResolveAuthoredDuration(overrides);
            var status=StatusEffectFactory.CreateForAuthoring(
                definition.AuthoringSchema,definition.StatusEffectId,
                value,duration,definition.RegenerationHealAmount,
                definition.RegenerationChannel);
            if(status==null)return false;
            var existing=(anchor.IsPartAnchor
                ?(IReadOnlyList<StatusEffect>)anchor.Part.StatusEffects:
                  anchor.Character.StatusEffects)?.FirstOrDefault(s=>
                    s!=null&&s.CanMergeWith(status));
            int previousStacks=existing?.Stack??0;
            int previousDuration=existing is DeferredStatusEffect defer
                ?defer.PendingDuration:existing?.Duration??0;

            var planned=runner.Plan(ability,cause,matcher,null,
                new Target(anchor.Character,anchor.IsPartAnchor?anchor.Part:null),
                new IEffectOperation[]{
                    new AddStatusOperation(value,anchor.IsPartAnchor)});
            if(planned.Commands.Count!=1||planned.Result.AppliedCommands!=0)
                return false;
            string commandId="R7-PREP-STATUS:"+AssetGuid+":"+
                cause.ActionId+":"+index+":"+cause.Kind+":"+cause.RollUseIndex;
            var commands=R5PlannedCommandBridge.Materialize(
                ability,cause,planned,commandId,0);
            var backend=new R5CombatSpecialistBackend(battle);
            if(anchor.IsPartAnchor)
                backend.RegisterPartStatus(commands[0],
                    EffectRequest.BodyPartStatus(ctx.Owner,anchor.Character,
                        anchor.Part,status,ctx.Action,ctx.RollIndex,ctx.Timing,true));
            else
                backend.RegisterCharacterStatus(commands[0],
                    EffectRequest.CharacterStatus(ctx.Owner,anchor.Character,
                        status,ctx.Action,ctx.RollIndex,ctx.Timing,true));
            NativeAttempts++;
            var slot=ctx.Action.Slot;
            if(slot==null)return false;
            if(!ledgers.TryGetValue(slot,out var ledger))
            {
                ledger=new CombatCommandApplyService();
                ledgers.Add(slot,ledger);
            }
            var receipt=ledger.Apply(ability,cause,commands,backend);
            if(receipt.CommittedCount!=1||receipt.HasUnknownCommit)
                return false;
            NativeCommits++;

            if(ctx.Action.ActionType==ActionType.Preparation)
            {
                var undo=ctx.Action.Slot?.PlanningUndo;
                if(undo!=null)
                {
                    // Preserve the EXACT original journal cardinality:
                    // one undo record per status. Slot-scoped dedupe cleanup
                    // runs inside the same record, not as an extra undo step.
                    int undoIndex=index;
                    Action clearSlot=()=>
                    {
                        identities.Remove((slot,undoIndex));
                        if(ledgers.Remove(slot,out var oldLedger))
                            oldLedger.ResetForBattle();
                    };
                    if(existing!=null)
                        undo.Record(()=>
                        {
                            existing.RestoreStateForPlanning(
                                previousStacks,previousDuration);
                            clearSlot();
                        });
                    else
                    {
                        var owner=anchor.Character;
                        var part=anchor.IsPartAnchor?anchor.Part:null;
                        undo.Record(()=>
                        {
                            if(owner!=null)
                            {
                                if(part!=null)
                                    owner.RemovePartStatus(part,status,
                                        StatusEffectRemoveReason.Cleared);
                                else
                                    owner.RemoveStatus(status,
                                        StatusEffectRemoveReason.Cleared);
                            }
                            clearSlot();
                        });
                    }
                    UndoRecords++;
                }
            }
            return true;
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;Enabled=false;
            identities.Clear();
            foreach(var ledger in ledgers.Values)ledger.ResetForBattle();
            ledgers.Clear();
        }
    }
}