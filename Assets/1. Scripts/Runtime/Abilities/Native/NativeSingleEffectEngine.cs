using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>One native SO -> one real R5 command. Never calls legacy effects.</summary>
    public sealed class NativeSingleEffectEngine : IDisposable
    {
        private sealed class Match : IAbilityTriggerMatcher
        {
            private readonly AbilityEventKind trigger;
            public Match(AbilityEventKind trigger){this.trigger=trigger;}
            public bool Matches(in AbilityEventContext evt)=>evt.Kind==trigger;
        }
        private sealed class Target : ICombatTargetSelector
        {
            private readonly CombatTargetRef reference;
            public Target(Character actor,BodyPart part)
            {
                reference=new CombatTargetRef(R4LiveSlotQuery.ActorRuntimeId(actor),
                    part?.PartId??string.Empty,"");
            }
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext _) =>
                new[]{reference};
        }
        private sealed class Operation : IEffectOperation
        {
            private readonly bool status,part;
            private readonly int amount;
            public Operation(bool status,bool part,int amount)
            {this.status=status;this.part=part;this.amount=amount;}
            public void Plan(IReadOnlyAbilityContext context,
                IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
            {
                if(context==null||targets==null||sink==null)return;
                foreach(var t in targets)
                    if(t.ActorRuntimeId>0&&amount>0)
                        sink.Enqueue(new CombatCommandIntent(
                            status?(part?nameof(CombatCommandKind.ApplyPartStatus):
                                nameof(CombatCommandKind.ApplyCharacterStatus)):
                                nameof(CombatCommandKind.ChangeResource),
                            t,amount));
            }
        }

        private readonly BattleContext battle;
        private readonly AbilityRunner runner=new AbilityRunner();
        private readonly CombatCommandApplyService ledger=new CombatCommandApplyService();
        private readonly Dictionary<(ActionSlot,string),AbilityInstance> identities=
            new Dictionary<(ActionSlot,string),AbilityInstance>();
        private bool disposed;
        public int Attempts {get;private set;}
        public int Commits {get;private set;}

        public NativeSingleEffectEngine(BattleContext battle)
        {this.battle=battle??throw new ArgumentNullException(nameof(battle));}

        public bool Execute(NativeSingleEffectDefinition graph,
            SkillEffectContext context,AbilityEventContext evt)
        {
            var action=context?.Action;
            if(disposed||graph==null||!graph.IsValid||action?.Slot==null||
                action.ActionId<=0||context.Owner==null||context.Owner.IsDead||
                evt.Kind!=graph.Trigger ||
                graph.RestrictToRoll&&evt.RollUseIndex+1!=graph.RollNumber)
                return false;

            bool status=graph.Kind==SingleEffectKind.ApplyCanonicalStatus;
            Character target=status&&graph.Target==NativeSingleEffectTarget.CurrentTarget
                ?context.Target:context.Owner;
            BodyPart part=status&&graph.Target==NativeSingleEffectTarget.CurrentTarget
                ?context.TargetPart:null;
            if(target==null||target.IsDead||
                part!=null&&(part.Owner!=target||part.IsBroken))return false;

            var key=(action.Slot,graph.SourceAssetGuid);
            if(!identities.TryGetValue(key,out var ability))
            {
                ability=new AbilityInstance(
                    new AbilityDefinitionRef(graph.StableId,graph.SourceAssetGuid),
                    new AbilityIdentity(graph.StableId,
                        R4LiveSlotQuery.ActorRuntimeId(context.Owner),
                        action.Slot.SlotId??string.Empty,0));
                identities.Add(key,ability);
            }
            if(ability.Identity.OwnerActorRuntimeId!=
               R4LiveSlotQuery.ActorRuntimeId(context.Owner))
                throw new InvalidOperationException("R7-3 action identity conflict");

            var plan=runner.Plan(ability,evt,new Match(graph.Trigger),null,
                new Target(target,part),new IEffectOperation[]{
                    new Operation(status,part!=null,graph.Amount)});
            if(plan.Commands.Count!=1||plan.Result.AppliedCommands!=0)
                return false;
            string batchId="R7-3:"+graph.SourceAssetGuid+":"+evt.ActionId+
                ":"+evt.CauseId+":"+evt.Kind+":"+evt.RollUseIndex;
            var commands=R5PlannedCommandBridge.Materialize(
                ability,evt,plan,batchId,0);
            ICombatCommandBackend backend;
            if(status)
            {
                StatusEffect effect=graph.NextTurn
                    ?new DeferredStatusEffect(graph.Status,graph.Amount,graph.Duration)
                    :StatusEffectFactory.CreateCanonical0922(
                        graph.Status,graph.Amount,graph.Duration);
                if(effect==null)return false;
                var service=new R5CombatSpecialistBackend(battle);
                var req=part==null
                    ?EffectRequest.CharacterStatus(context.Owner,target,effect,
                        action,context.RollIndex,context.Timing,true)
                    :EffectRequest.BodyPartStatus(context.Owner,target,part,effect,
                        action,context.RollIndex,context.Timing,true);
                if(part==null)service.RegisterCharacterStatus(commands[0],req);
                else service.RegisterPartStatus(commands[0],req);
                backend=service;
            }
            else
            {
                var service=new NativeResourceBackend();
                var kind=graph.Kind==SingleEffectKind.GainBlock
                    ?NativeResourceKind.Block
                    :graph.Kind==SingleEffectKind.GainPrestige
                        ?NativeResourceKind.Prestige
                        :NativeResourceKind.TurnClashPower;
                service.Register(commands[0],target,kind);
                backend=service;
            }
            Attempts++;
            var result=ledger.Apply(ability,evt,commands,backend);
            bool committed=result.CommittedCount==1&&!result.HasUnknownCommit;
            if(committed)Commits++;
            return committed;
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            identities.Clear();
            ledger.ResetForBattle();
        }
    }
}