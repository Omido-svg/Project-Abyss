using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Exactly one authoritative character mechanic receives each committed
    /// native ChangeResource command. Never routes through legacy effects.
    /// </summary>
    public sealed class NativeUniqueGaugeBackend : ICombatCommandBackend
    {
        private readonly Dictionary<string,(Character owner,UniqueGaugeKind kind)> bound=
            new Dictionary<string,(Character,UniqueGaugeKind)>(StringComparer.Ordinal);

        public bool CanApply(CombatCommandKind kind)=>
            kind==CombatCommandKind.ChangeResource;

        public void Register(CombatCommandEnvelope command,Character owner,
            UniqueGaugeKind kind)
        {
            if(owner==null || owner.IsDead ||
               command.Kind!=CombatCommandKind.ChangeResource ||
               command.Target.ActorRuntimeId!=R4LiveSlotQuery.ActorRuntimeId(owner) ||
               !string.IsNullOrEmpty(command.Target.PartId) ||
               command.RequestedAmount<=0 || bound.ContainsKey(command.CommandId))
                throw new InvalidOperationException("Invalid native unique gauge binding");
            if(kind==UniqueGaugeKind.OlafMadness &&
                owner.GetMechanic<OlafMadnessMechanic>()==null ||
               kind==UniqueGaugeKind.YujinSense &&
                owner.GetMechanic<YujinMechanic>()==null)
                throw new InvalidOperationException("Required character mechanic unavailable");
            bound.Add(command.CommandId,(owner,kind));
        }

        public CombatCommandReceipt Apply(CombatCommandEnvelope command)
        {
            if(!bound.TryGetValue(command.CommandId,out var payload))
                return CombatCommandReceipt.Reject("Unbound unique gauge command");
            bound.Remove(command.CommandId);
            var owner=payload.owner;
            if(owner==null||owner.IsDead||
               command.Target.ActorRuntimeId!=R4LiveSlotQuery.ActorRuntimeId(owner)||
               command.RequestedAmount<=0)
                return CombatCommandReceipt.Reject("Unique gauge owner no longer valid");

            int before,after;
            switch(payload.kind)
            {
                case UniqueGaugeKind.OlafMadness:
                {
                    var m=owner.GetMechanic<OlafMadnessMechanic>();
                    if(m==null)return CombatCommandReceipt.Reject("Olaf mechanic missing");
                    before=m.CurrentMadness;
                    m.AddMadness(command.RequestedAmount);
                    after=m.CurrentMadness;
                    break;
                }
                case UniqueGaugeKind.YujinSense:
                {
                    var m=owner.GetMechanic<YujinMechanic>();
                    if(m==null)return CombatCommandReceipt.Reject("Yujin mechanic missing");
                    before=m.Sense;
                    m.GrantSense(command.RequestedAmount);
                    after=m.Sense;
                    break;
                }
                default:return CombatCommandReceipt.Reject("Unknown unique gauge");
            }
            // The original authoring effect successfully executes even at
            // gauge cap (no numeric delta). Preserve that result contract.
            return new CombatCommandReceipt(CombatCommandOutcome.Applied,
                Math.Max(0,after-before),0,true,
                "Native "+payload.kind+" "+before+" -> "+after);
        }

        public void Clear()=>bound.Clear();
    }

    public sealed class NativeUniqueGaugeEngine : IDisposable
    {
        private sealed class Match : IAbilityTriggerMatcher
        {
            private readonly AbilityEventKind kind;
            public Match(AbilityEventKind kind)=>this.kind=kind;
            public bool Matches(in AbilityEventContext evt)=>evt.Kind==kind;
        }
        private sealed class OwnerSelector : ICombatTargetSelector
        {
            private readonly CombatTargetRef target;
            public OwnerSelector(Character owner)=>
                target=new CombatTargetRef(R4LiveSlotQuery.ActorRuntimeId(owner),"","");
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext _)=>
                new[]{target};
        }
        private readonly AbilityRunner runner=new AbilityRunner();
        private readonly CombatCommandApplyService apply=new CombatCommandApplyService();
        private readonly NativeUniqueGaugeBackend backend=new NativeUniqueGaugeBackend();
        private readonly Dictionary<(long,string),AbilityInstance> instances=
            new Dictionary<(long,string),AbilityInstance>();
        private bool disposed;
        public int Attempts {get;private set;}
        public int Commits {get;private set;}

        public bool Execute(R7NativeUniqueGaugeDefinition graph,
            BattleAction action,Character owner,AbilityEventContext evt,int level)
        {
            if(disposed||graph==null||!graph.IsValid||
               action==null||action.ActionId<=0||owner==null||
               graph.Trigger!=evt.Kind||
               graph.RestrictToRoll&&evt.RollUseIndex+1!=graph.RollNumber)
                return false;
            int amount=graph.AmountForLevel(level);
            if(amount<=0)return false;
            var key=(action.ActionId,graph.SourceAssetGuid);
            if(!instances.TryGetValue(key,out var instance))
            {
                instance=new AbilityInstance(
                    new AbilityDefinitionRef(graph.StableId,graph.SourceAssetGuid),
                    new AbilityIdentity(graph.StableId,
                        R4LiveSlotQuery.ActorRuntimeId(owner),
                        action.Slot?.SlotId??string.Empty,0));
                instances.Add(key,instance);
            }
            if(instance.Identity.OwnerActorRuntimeId!=
                R4LiveSlotQuery.ActorRuntimeId(owner))
                throw new InvalidOperationException("Native gauge identity collision");
            var plan=runner.Plan(instance,evt,new Match(graph.Trigger),null,
                new OwnerSelector(owner),new IEffectOperation[]{
                    new ChangeResourceOperation(amount)
                });
            if(plan.Commands.Count!=1||plan.Result.AppliedCommands!=0)return false;
            var id="NativeGauge:"+graph.SourceAssetGuid+":"+
                evt.ActionId+":"+evt.Kind+":"+evt.RollUseIndex+":"+evt.CauseId;
            var commands=R5PlannedCommandBridge.Materialize(
                instance,evt,plan,id,0);
            backend.Register(commands[0],owner,graph.Kind);
            Attempts++;
            var receipt=apply.Apply(instance,evt,commands,backend);
            bool done=receipt.CommittedCount==1&&!receipt.HasUnknownCommit;
            if(done)Commits++;
            return done;
        }

        public void Dispose()
        {
            if(disposed)return;
            disposed=true;instances.Clear();backend.Clear();apply.ResetForBattle();
        }
    }
}
