using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Native-only resource effect planner and applier. Takes authored native
    /// SO, action and actor, never SkillDefinition/SkillEffectEntry/overrides.
    /// CombatCommandApplyService is the sole mutation boundary.
    /// </summary>
    public sealed class R7NativeResourceAbilityEngine : IDisposable
    {
        private sealed class Matcher : IAbilityTriggerMatcher
        {
            private readonly AbilityEventKind trigger;
            public Matcher(AbilityEventKind trigger)=>this.trigger=trigger;
            public bool Matches(in AbilityEventContext evt)=>evt.Kind==trigger;
        }
        private sealed class OwnerTarget : ICombatTargetSelector
        {
            private readonly CombatTargetRef target;
            public OwnerTarget(Character character)
            {
                target=new CombatTargetRef(
                    R4LiveSlotQuery.ActorRuntimeId(character),"","");
            }
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext _) =>
                new[]{target};
        }

        private readonly AbilityRunner planner=new AbilityRunner();
        private readonly CombatCommandApplyService apply=new CombatCommandApplyService();
        private readonly R7NativeResourceBackend backend=new R7NativeResourceBackend();
        private readonly Dictionary<(long,string),AbilityInstance> instances=
            new Dictionary<(long,string),AbilityInstance>();
        private bool disposed;
        public int Attempts {get;private set;}
        public int Commits {get;private set;}

        public bool Execute(R7NativeAbilityDefinition data,BattleAction action,
            Character owner,AbilityEventContext evt,int upgradeLevel)
        {
            if(disposed||data==null||!data.IsValid||
                action==null||owner==null||action.ActionId<=0||
                data.Trigger!=evt.Kind||
                (data.RestrictToRoll&&evt.RollUseIndex+1!=data.RollNumber))
                return false;
            int amount=data.AmountForLevel(upgradeLevel);
            if(amount<=0)return false;
            var key=(action.ActionId,data.StableId);
            if(!instances.TryGetValue(key,out var instance))
            {
                var identity=new AbilityIdentity(data.StableId,
                    R4LiveSlotQuery.ActorRuntimeId(owner),
                    action.Slot?.SlotId??string.Empty,0);
                instance=new AbilityInstance(
                    new AbilityDefinitionRef(data.StableId,data.SourceAssetGuid),
                    identity);
                instances.Add(key,instance);
            }
            if(instance.Identity.OwnerActorRuntimeId!=
                R4LiveSlotQuery.ActorRuntimeId(owner))
                throw new InvalidOperationException("Native ability action/actor identity conflict");
            var planned=planner.Plan(instance,evt,new Matcher(data.Trigger),
                null,new OwnerTarget(owner),
                new IEffectOperation[]{new ChangeResourceOperation(amount)});
            if(planned.Commands.Count!=1||planned.Result.AppliedCommands!=0)
                return false;
            var commandId="NativeAbility:"+data.SourceAssetGuid+":"+
                evt.ActionId+":"+evt.Kind+":"+evt.RollUseIndex+":"+evt.CauseId;
            var commands=R5PlannedCommandBridge.Materialize(
                instance,evt,planned,commandId,0);
            backend.Register(commands[0],owner,data.Operation);
            Attempts++;
            var receipt=apply.Apply(instance,evt,commands,backend);
            bool committed=receipt.CommittedCount==1&&!receipt.HasUnknownCommit;
            if(committed)Commits++;
            return committed;
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            instances.Clear();backend.Clear();apply.ResetForBattle();
        }
    }
}