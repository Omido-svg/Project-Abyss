using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// The last Preparation flat setup effect is planned and executed entirely
    /// from native SO using two typed R5 ChangeResource commands. The original
    /// PreparationFlatSetupEffect.Apply is not invoked.
    /// </summary>
    public sealed class NativePreparationSetupEngine
    {
        private sealed class Match : IAbilityTriggerMatcher
        {
            public bool Matches(in AbilityEventContext e)=>
                e.Kind==AbilityEventKind.RollUsing;
        }
        private sealed class FixedTarget : ICombatTargetSelector
        {
            private readonly CombatTargetRef target;
            public FixedTarget(Character c)=>target=new CombatTargetRef(
                R4LiveSlotQuery.ActorRuntimeId(c),"","");
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext _)=>
                new[]{target};
        }
        private readonly AbilityRunner runner=new AbilityRunner();
        private readonly NativeResourceBackend backend=new NativeResourceBackend();
        private readonly Match match=new Match();
        public int CommandCommits {get;private set;}
        public int Attempts {get;private set;}

        public bool Apply(NativePreparationSetupDefinition data,
            SkillEffectContext context,AbilityInstance instance,
            AbilityEventContext cause,CombatCommandApplyService ledger)
        {
            if(data==null||!data.IsValid||instance==null||ledger==null||
               context?.Action==null||context.Owner==null||
               cause.Kind!=data.Trigger) return false;
            var owner=data.GiveToSelectedTarget?context.Target:context.Owner;
            if(owner==null||owner.IsDead)return false;
            bool weakened=PreparationEffectUtility.IsWeakenedLegSource(context);
            int offset=weakened?data.WeakenedLegPenalty:0;
            int block=Math.Max(0,data.BlockGain-offset);
            int clash=Math.Max(0,data.TurnClashPowerBonus-offset);
            if(block==0&&clash==0)return true; // original still reports Applied
            var plannedOps=new List<IEffectOperation>();
            var kinds=new List<NativeResourceKind>();
            if(block>0)
            {
                plannedOps.Add(new ChangeResourceOperation(block));
                kinds.Add(NativeResourceKind.Block);
            }
            if(clash>0)
            {
                plannedOps.Add(new ChangeResourceOperation(clash));
                kinds.Add(NativeResourceKind.TurnClashPower);
            }
            var plan=runner.Plan(instance,cause,match,null,
                new FixedTarget(owner),plannedOps);
            if(plan.Commands.Count!=kinds.Count || plan.Result.AppliedCommands!=0)
                return false;
            var commands=R5PlannedCommandBridge.Materialize(
                instance,cause,plan,
                "NativePrepSetup:"+data.SourceAssetGuid+":"+cause.ActionId+
                    ":"+cause.Kind+":"+cause.RollUseIndex,
                0);
            for(int i=0;i<commands.Count;i++)
                backend.Register(commands[i],owner,kinds[i]);
            Attempts++;
            var receipt=ledger.Apply(instance,cause,commands,backend);
            if(receipt.HasUnknownCommit)return false;
            CommandCommits+=receipt.CommittedCount;
            return receipt.CommittedCount==commands.Count;
        }
        public void Clear()=>backend.Clear();
    }
}
