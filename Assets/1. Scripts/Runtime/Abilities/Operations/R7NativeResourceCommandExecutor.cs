using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    public enum R7NativeResourceKind
    {
        Block,
        Prestige,
        TurnClashPower
    }

    /// <summary>
    /// Actual native R7 command backend, using Character's authoritative
    /// resource APIs. Does NOT call SkillEffectDefinition.Apply.
    /// This is deliberately narrower than the generic R5 custom-resource API.
    /// </summary>
    public sealed class R7NativeResourceBackend : ICombatCommandBackend
    {
        private sealed class Payload
        {
            public Character Owner;
            public R7NativeResourceKind Kind;
        }
        private readonly Dictionary<string,Payload> bound =
            new Dictionary<string,Payload>(StringComparer.Ordinal);

        public bool CanApply(CombatCommandKind kind) =>
            kind==CombatCommandKind.ChangeResource;

        public void Register(CombatCommandEnvelope command,
            Character owner,R7NativeResourceKind kind)
        {
            if(owner==null || owner.IsDead ||
                command.Kind!=CombatCommandKind.ChangeResource ||
                command.RequestedAmount<=0 ||
                command.Target.ActorRuntimeId!=R4LiveSlotQuery.ActorRuntimeId(owner) ||
                !string.IsNullOrEmpty(command.Target.PartId) ||
                string.IsNullOrWhiteSpace(command.CommandId) ||
                bound.ContainsKey(command.CommandId))
                throw new InvalidOperationException("Invalid native resource binding.");
            bound.Add(command.CommandId,new Payload { Owner=owner,Kind=kind });
        }

        public CombatCommandReceipt Apply(CombatCommandEnvelope request)
        {
            if(!bound.TryGetValue(request.CommandId,out var payload))
                return CombatCommandReceipt.Reject("No native resource command registered.");
            // Exactly one dispatch attempt, even if a specialist throws.
            bound.Remove(request.CommandId);
            var owner=payload.Owner;
            if(owner==null || owner.IsDead ||
               request.Kind!=CombatCommandKind.ChangeResource ||
               request.RequestedAmount<=0 ||
               request.Target.ActorRuntimeId!=R4LiveSlotQuery.ActorRuntimeId(owner) ||
               !string.IsNullOrEmpty(request.Target.PartId))
                return CombatCommandReceipt.Reject("Owner or target became invalid.");
            int before;
            int after;
            switch(payload.Kind)
            {
                case R7NativeResourceKind.Block:
                    before=owner.RuntimeStatus?.currentBlock??0;
                    owner.AddBlock(request.RequestedAmount);
                    after=owner.RuntimeStatus?.currentBlock??0;
                    break;
                case R7NativeResourceKind.Prestige:
                    before=owner.RuntimeStatus?.currentPrestige??0;
                    owner.AddPrestige(request.RequestedAmount);
                    after=owner.RuntimeStatus?.currentPrestige??0;
                    break;
                case R7NativeResourceKind.TurnClashPower:
                    before=owner.TurnClashPowerBonus;
                    owner.AddTurnClashPowerBonus(request.RequestedAmount);
                    after=owner.TurnClashPowerBonus;
                    break;
                default:
                    return CombatCommandReceipt.Reject("Unsupported native resource family.");
            }
            int applied=Math.Max(0,after-before);
            return new CombatCommandReceipt(
                applied>0 ? CombatCommandOutcome.Applied : CombatCommandOutcome.Rejected,
                applied,0,applied>0,
                "R7 native "+payload.Kind+"; before="+before+",after="+after);
        }

        public void Clear() => bound.Clear();
    }

    /// <summary>
    /// Generates a typed, causal ChangeResource command from a real authored
    /// resource effect. Called only after original Entry and Definition gates
    /// have passed. Any non-resource proxy is left on the exclusive legacy
    /// specialist bridge; no speculative partial native apply.
    /// </summary>
    public sealed class R7NativeResourceCommandExecutor
    {
        private sealed class FixedOwnerSelector : ICombatTargetSelector
        {
            private readonly CombatTargetRef owner;
            public FixedOwnerSelector(Character character)
            {
                owner=new CombatTargetRef(
                    R4LiveSlotQuery.ActorRuntimeId(character),"","");
            }
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext context)=>
                new[]{owner};
        }
        private sealed class Match : IAbilityTriggerMatcher
        {
            public bool Matches(in AbilityEventContext _) => true;
        }

        private readonly CombatCommandApplyService service=
            new CombatCommandApplyService();
        private readonly R7NativeResourceBackend backend=
            new R7NativeResourceBackend();
        private readonly AbilityRunner runner=new AbilityRunner();
        private readonly Match matcher=new Match();

        public int NativeAttemptCount {get;private set;}
        public int NativeCommittedCount {get;private set;}

        public static bool IsNative(SkillEffectDefinition definition) =>
            definition is TempBalanceSkillProxyEffectDefinition temp &&
            (temp.Operation==TempBalanceSkillProxyOperation.GainBlock ||
             temp.Operation==TempBalanceSkillProxyOperation.GainPrestige ||
             temp.Operation==TempBalanceSkillProxyOperation.TurnClashPowerBonus);

        public bool Apply(AbilityInstance ability,
            AbilityEventContext cause,
            SkillEffectContext context,
            SkillEffectOverrides overrides,
            TempBalanceSkillProxyEffectDefinition effect,
            int entryIndex,
            out int plannedCount)
        {
            plannedCount=0;
            if(ability==null || context?.Owner==null || effect==null ||
                !IsNative(effect) || context.Action==null || cause.ActionId<=0)
                return false;
            int amount=overrides?.OverrideAmount==true
                ?overrides.Amount:effect.BaseAmount;
            amount=Math.Max(0,amount);
            if(amount<=0)
                return false;
            R7NativeResourceKind kind=
                effect.Operation==TempBalanceSkillProxyOperation.GainBlock
                    ?R7NativeResourceKind.Block:
                effect.Operation==TempBalanceSkillProxyOperation.GainPrestige
                    ?R7NativeResourceKind.Prestige:
                    R7NativeResourceKind.TurnClashPower;
            var plan=runner.Plan(ability,cause,matcher,null,
                new FixedOwnerSelector(context.Owner),
                new IEffectOperation[]{new ChangeResourceOperation(amount)});
            plannedCount=plan.Commands.Count;
            if(plannedCount!=1 || plan.Result.AppliedCommands!=0)
                return false;
            string id=ability.Identity.DefinitionId+":native:"+
                cause.ActionId+":"+entryIndex+":"+cause.Kind+":"+cause.RollUseIndex;
            var commands=R5PlannedCommandBridge.Materialize(ability,cause,plan,id,0);
            backend.Register(commands[0],context.Owner,kind);
            NativeAttemptCount++;
            var applied=service.Apply(ability,cause,commands,backend);
            bool committed=applied.CommittedCount==1 && !applied.HasUnknownCommit;
            if(committed)NativeCommittedCount++;
            return committed;
        }
        public void Clear()
        {
            backend.Clear();
            service.ResetForBattle();
        }
    }
}